#!/usr/bin/env bash
#
# provision-lhci-server.sh — create or update the Lighthouse CI server on
# Azure, and the GitHub secrets that reach it. docs/lighthouse.md explains
# the design. Safe to run again: every step checks what exists first.
#
#   scripts/provision-lhci-server.sh
#
# Needs `az` signed in to the subscription and `gh` signed in with admin
# on the repository. Prints no secret. The first run, and a run that
# changes them, also needs in the environment:
#
#   GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET  the OAuth client "Lighthouse CI
#       server" in Google Auth Platform, redirect URI
#       <server>/.auth/login/google/callback
#   LHCI_ALLOWED_EMAILS  the Google accounts that may read the server
#
# A later run keeps the values the app holds. Creates:
#
#   - storage account aberatechlhci, Azure Files share `lhci` (SQLite file),
#     7-day share soft delete
#   - the share on the Container Apps environment as `lhci`
#   - identity abera-lhci with AcrPull on the registry (pulls the image)
#   - image lhci-server:<tree hash of tools/lhci-server>, built by ACR
#   - container app abera-lhci: 0.25 vCPU, 0.5 GiB, 0 to 1 replicas, with
#     Google sign-in for people and basic auth for the workflows
#   - identity abera-lhci-backup, signed in to by a master run of the
#     nightly workflow, allowed to snapshot the share and nothing else
#   - LHCI projects `aberaTech CI` and `abera.tech production`
#   - Actions secrets LHCI_BASIC_AUTH_PASSWORD, LHCI_BUILD_TOKEN,
#     LHCI_PRODUCTION_BUILD_TOKEN, LHCI_ADMIN_TOKEN,
#     LHCI_PRODUCTION_ADMIN_TOKEN. The first two also in the Dependabot
#     store, since a Dependabot pull request runs the Lighthouse job too.
#   - Actions variable LHCI_BACKUP_CLIENT_ID
#
# The server's address is not a secret. The workflows name it in
# LHCI_SERVER_URL, since Dependabot runs cannot read Actions variables.
set -euo pipefail
cd "$(dirname "$0")/.."

REPO=neb-abera/aberaTech
RG=aberatechserver-app-202412211749ResourceGroup
ENVIRONMENT=aberaTechServer-env-20241221174945
LOCATION=eastus
ACR=aberatechserver20241221175455
STORAGE=aberatechlhci
SHARE=lhci
APP=abera-lhci
PULL_IDENTITY=abera-lhci
BACKUP_IDENTITY=abera-lhci-backup
BACKUP_ROLE="LHCI share snapshots"

say() { printf '%s\n' "$*" >&2; }
work=$(mktemp -d)
chmod 700 "$work"
trap 'rm -rf "$work"' EXIT

say "storage account $STORAGE"
if ! az storage account show -g "$RG" -n "$STORAGE" -o none 2>/dev/null; then
  az storage account create -g "$RG" -n "$STORAGE" -l "$LOCATION" \
    --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 \
    --https-only true --allow-blob-public-access false -o none
fi
# SMB 3.1.1 with AES-GCM only. Soft delete keeps a deleted share, and its
# snapshots, for 7 days.
az storage account file-service-properties update -g "$RG" -n "$STORAGE" \
  --versions "SMB3.1.1" --channel-encryption "AES-128-GCM;AES-256-GCM" \
  --enable-delete-retention true --delete-retention-days 7 -o none
if ! az storage share-rm show -g "$RG" --storage-account "$STORAGE" -n "$SHARE" -o none 2>/dev/null; then
  az storage share-rm create -g "$RG" --storage-account "$STORAGE" -n "$SHARE" \
    --quota 5 --access-tier TransactionOptimized -o none
fi

say "share mounted on the environment"
key=$(az storage account keys list -g "$RG" -n "$STORAGE" --query '[0].value' -o tsv)
az containerapp env storage set -g "$RG" -n "$ENVIRONMENT" --storage-name "$SHARE" \
  --azure-file-account-name "$STORAGE" --azure-file-account-key "$key" \
  --azure-file-share-name "$SHARE" --access-mode ReadWrite -o none
unset key

say "pull identity $PULL_IDENTITY"
az identity show -g "$RG" -n "$PULL_IDENTITY" -o none 2>/dev/null ||
  az identity create -g "$RG" -n "$PULL_IDENTITY" -l "$LOCATION" -o none
pull_id=$(az identity show -g "$RG" -n "$PULL_IDENTITY" --query id -o tsv)
pull_principal=$(az identity show -g "$RG" -n "$PULL_IDENTITY" --query principalId -o tsv)
acr_id=$(az acr show -n "$ACR" --query id -o tsv)
acr_server=$(az acr show -n "$ACR" --query loginServer -o tsv)
if [ -z "$(az role assignment list --assignee "$pull_principal" --scope "$acr_id" --role AcrPull --query '[].id' -o tsv)" ]; then
  az role assignment create --assignee-object-id "$pull_principal" \
    --assignee-principal-type ServicePrincipal --role AcrPull --scope "$acr_id" -o none
fi

tag=$(git rev-parse --short=12 HEAD:tools/lhci-server)
image="$acr_server/lhci-server:$tag"
say "image $image"
if [ -z "$(az acr repository show-tags -n "$ACR" --repository lhci-server --query "[?@=='$tag']" -o tsv 2>/dev/null)" ]; then
  az acr build -r "$ACR" -t "lhci-server:$tag" --platform linux/amd64 \
    -f tools/lhci-server/Dockerfile tools/lhci-server >&2
fi

env_id=$(az containerapp env show -g "$RG" -n "$ENVIRONMENT" --query id -o tsv)
if az containerapp show -g "$RG" -n "$APP" -o none 2>/dev/null; then
  password=$(az containerapp secret show -g "$RG" -n "$APP" --secret-name basic-auth-password --query value -o tsv)
  new_password=false
  kept() { az containerapp secret show -g "$RG" -n "$APP" --secret-name "$1" --query value -o tsv 2>/dev/null || true; }
  : "${GOOGLE_CLIENT_SECRET:=$(kept google-client-secret)}"
  : "${LHCI_ALLOWED_EMAILS:=$(kept allowed-emails)}"
  : "${GOOGLE_CLIENT_ID:=$(az containerapp auth google show -g "$RG" -n "$APP" --query registration.clientId -o tsv 2>/dev/null || true)}"
else
  password=$(openssl rand -base64 48 | tr -d '/+=\n' | cut -c1-48)
  new_password=true
fi
for name in GOOGLE_CLIENT_ID GOOGLE_CLIENT_SECRET LHCI_ALLOWED_EMAILS; do
  [ -n "${!name:-}" ] || { say "$name is not set and the app holds none"; exit 1; }
done

# The whole app in one document, so a second run converges on it.
# mountOptions: nobrl, because SQLite's byte-range locks fail on SMB without
# it, and the owner and modes of the node user (uid 1000), since the app
# runs as that user. cooldownPeriod 30 rather than the default 300: a
# replica is billed until it scales in, and the cost bound in
# docs/lighthouse.md counts 110 s a wake.
umask 077
cat > "$work/app.yaml" <<YAML
location: $LOCATION
identity:
  type: UserAssigned
  userAssignedIdentities:
    $pull_id: {}
properties:
  managedEnvironmentId: $env_id
  workloadProfileName: Consumption
  configuration:
    activeRevisionsMode: Single
    ingress:
      external: true
      targetPort: 9001
      transport: auto
      allowInsecure: false
    registries:
      - server: $acr_server
        identity: $pull_id
    secrets:
      - name: basic-auth-password
        value: $password
      - name: google-client-secret
        value: $GOOGLE_CLIENT_SECRET
      - name: allowed-emails
        value: $LHCI_ALLOWED_EMAILS
  template:
    containers:
      - name: lhci-server
        image: $image
        resources:
          cpu: 0.25
          memory: 0.5Gi
        env:
          - name: LHCI_BASIC_AUTH_PASSWORD
            secretRef: basic-auth-password
          - name: LHCI_ALLOWED_EMAILS
            secretRef: allowed-emails
          - name: LHCI_DATABASE_PATH
            value: /data/lhci.db
        volumeMounts:
          - volumeName: lhci
            mountPath: /data
        probes:
          - type: Liveness
            httpGet:
              path: /healthz
              port: 9001
          - type: Readiness
            httpGet:
              path: /healthz
              port: 9001
    scale:
      minReplicas: 0
      maxReplicas: 1
      cooldownPeriod: 30
      rules:
        - name: http
          http:
            metadata:
              concurrentRequests: "10"
    volumes:
      - name: lhci
        storageType: AzureFile
        storageName: $SHARE
        mountOptions: uid=1000,gid=1000,dir_mode=0750,file_mode=0640,nobrl
YAML
say "container app $APP"
if [ "$new_password" = true ]; then
  az containerapp create -g "$RG" -n "$APP" --yaml "$work/app.yaml" -o none
else
  az containerapp update -g "$RG" -n "$APP" --yaml "$work/app.yaml" -o none
fi
rm -f "$work/app.yaml"

# Google sign-in by the platform. AllowAnonymous, because the workflows
# upload with basic auth and no Google session: tools/lhci-server/gate.mjs
# lets them in by the password and a person in by the signed-in address.
say "Google sign-in"
az containerapp auth google update -g "$RG" -n "$APP" --client-id "$GOOGLE_CLIENT_ID" \
  --client-secret-name google-client-secret --yes -o none
az containerapp auth update -g "$RG" -n "$APP" --enabled true \
  --unauthenticated-client-action AllowAnonymous --require-https true -o none
url="https://$(az containerapp show -g "$RG" -n "$APP" --query properties.configuration.ingress.fqdn -o tsv)"

say "waiting for $url/healthz"
for _ in $(seq 1 60); do
  curl -sf "$url/healthz" > /dev/null && break
  sleep 5
done
curl -sf "$url/healthz" > /dev/null

# printf into a mode-600 file, so no secret is on a command line.
printf 'user = "lhci:%s"\n' "$password" > "$work/curlrc"
api() { curl -sf --config "$work/curlrc" -H 'content-type: application/json' "$@"; }
secret() { printf '%s' "$2" | gh secret set "$1" --repo "$REPO" --app "${3:-actions}"; }

if [ "$new_password" = true ]; then
  secret LHCI_BASIC_AUTH_PASSWORD "$password"
  secret LHCI_BASIC_AUTH_PASSWORD "$password" dependabot
fi

# project <name> <build token secret> <admin token secret>. The server
# makes the slug from the name: aberatech-ci and abera.tech-production.
project() {
  if api "$url/v1/projects" | jq -e --arg n "$1" 'any(.[]; .name == $n)' > /dev/null; then
    say "project $1 exists"
    return
  fi
  say "project $1"
  api -X POST "$url/v1/projects" \
    -d "{\"name\":\"$1\",\"externalUrl\":\"https://github.com/$REPO\",\"baseBranch\":\"master\"}" \
    > "$work/project.json"
  secret "$2" "$(jq -r .token "$work/project.json")"
  secret "$3" "$(jq -r .adminToken "$work/project.json")"
  if [ "$2" = LHCI_BUILD_TOKEN ]; then
    secret "$2" "$(jq -r .token "$work/project.json")" dependabot
  fi
  rm -f "$work/project.json"
}
project "aberaTech CI" LHCI_BUILD_TOKEN LHCI_ADMIN_TOKEN
project "abera.tech production" LHCI_PRODUCTION_BUILD_TOKEN LHCI_PRODUCTION_ADMIN_TOKEN
unset password

say "backup identity $BACKUP_IDENTITY"
az identity show -g "$RG" -n "$BACKUP_IDENTITY" -o none 2>/dev/null ||
  az identity create -g "$RG" -n "$BACKUP_IDENTITY" -l "$LOCATION" -o none
backup_client=$(az identity show -g "$RG" -n "$BACKUP_IDENTITY" --query clientId -o tsv)
backup_principal=$(az identity show -g "$RG" -n "$BACKUP_IDENTITY" --query principalId -o tsv)
# Both subject formats GitHub issues: the name form and the immutable id form.
owner_id=$(gh api "repos/$REPO" --jq .owner.id)
repo_id=$(gh api "repos/$REPO" --jq .id)
for pair in "github-master:repo:$REPO:ref:refs/heads/master" \
  "github-master-immutable:repo:neb-abera@$owner_id/aberaTech@$repo_id:ref:refs/heads/master"; do
  name=${pair%%:*}
  subject=${pair#*:}
  az identity federated-credential show -g "$RG" --identity-name "$BACKUP_IDENTITY" -n "$name" -o none 2>/dev/null ||
    az identity federated-credential create -g "$RG" --identity-name "$BACKUP_IDENTITY" -n "$name" \
      --issuer https://token.actions.githubusercontent.com --subject "$subject" \
      --audiences api://AzureADTokenExchange -o none
done
storage_id=$(az storage account show -g "$RG" -n "$STORAGE" --query id -o tsv)
if [ -z "$(az role definition list --custom-role-only true --name "$BACKUP_ROLE" --query '[].id' -o tsv)" ]; then
  cat > "$work/role.json" <<JSON
{
  "Name": "$BACKUP_ROLE",
  "Description": "Create, list and delete snapshots of the Lighthouse CI share.",
  "Actions": [
    "Microsoft.Storage/storageAccounts/read",
    "Microsoft.Storage/storageAccounts/fileServices/read",
    "Microsoft.Storage/storageAccounts/fileServices/shares/read",
    "Microsoft.Storage/storageAccounts/fileServices/shares/write",
    "Microsoft.Storage/storageAccounts/fileServices/shares/delete"
  ],
  "AssignableScopes": ["$storage_id"]
}
JSON
  az role definition create --role-definition "$work/role.json" -o none
fi
# A new role definition takes a minute or two to replicate.
for _ in $(seq 1 12); do
  [ -z "$(az role assignment list --assignee "$backup_principal" --scope "$storage_id" --query '[].id' -o tsv)" ] || break
  az role assignment create --assignee-object-id "$backup_principal" \
    --assignee-principal-type ServicePrincipal --role "$BACKUP_ROLE" --scope "$storage_id" -o none 2>/dev/null ||
    sleep 15
done
[ -n "$(az role assignment list --assignee "$backup_principal" --scope "$storage_id" --query '[].id' -o tsv)" ]
gh variable set LHCI_BACKUP_CLIENT_ID --repo "$REPO" --body "$backup_client"

say "done: $url"
