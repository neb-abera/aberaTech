#!/usr/bin/env bash
#
# deploy-grader.sh <image>: put the Rust grader (grader/README.md) on <image>
# in the site's Container Apps environment, and print its internal address.
#
# The first run creates the app: internal ingress only, so nothing outside the
# environment reaches it, 0 to 1 replicas, 0.25 vCPU and 0.5 GiB. It pulls
# from the registry with its own system-assigned identity, which `az
# containerapp create` grants AcrPull. Every later run moves it to <image>.
# Then it waits for the newest revision to be provisioned on <image>.
#
# The deploy workflow passes the printed address to the site as
# Training__GraderUrl. Cost basis: abera-lhci at the same size and
# scale-to-zero, invoiced $0.09 in September 2026 and $0.14 in October 2026.
#
#   scripts/deploy-grader.sh --self-test   runs this script against a stubbed
#   az and checks the create path, the update path, and a failed revision.

set -euo pipefail

APP="${GRADER_APP:-abera-grader}"
GROUP="${GRADER_RESOURCE_GROUP:-aberatechserver-app-202412211749ResourceGroup}"
ENVIRONMENT="${GRADER_ENVIRONMENT:-aberaTechServer-env-20241221174945}"
ATTEMPTS="${GRADER_WAIT_ATTEMPTS:-40}"
PAUSE="${GRADER_WAIT_SECONDS:-15}"

deploy() {
  local image="$1" registry="${1%%/*}" attempt name state image_now fqdn
  if az containerapp show --name "$APP" --resource-group "$GROUP" --query name --output tsv > /dev/null 2>&1; then
    echo "updating $APP to $image" >&2
    az containerapp update --name "$APP" --resource-group "$GROUP" --image "$image" --output none
  else
    echo "creating $APP on $image" >&2
    az containerapp create --name "$APP" --resource-group "$GROUP" \
      --environment "$ENVIRONMENT" --image "$image" \
      --registry-server "$registry" --registry-identity system --system-assigned \
      --ingress internal --target-port 8080 --transport http \
      --min-replicas 0 --max-replicas 1 --cpu 0.25 --memory 0.5Gi \
      --output none
  fi

  for attempt in $(seq 1 "$ATTEMPTS"); do
    name=$(az containerapp revision list --name "$APP" --resource-group "$GROUP" \
      --query "sort_by([?properties.active], &properties.createdTime)[-1].name" --output tsv 2> /dev/null || true)
    state=""
    image_now=""
    if [ -n "$name" ]; then
      state=$(az containerapp revision show --name "$APP" --resource-group "$GROUP" --revision "$name" \
        --query properties.provisioningState --output tsv 2> /dev/null || true)
      image_now=$(az containerapp revision show --name "$APP" --resource-group "$GROUP" --revision "$name" \
        --query "properties.template.containers[0].image" --output tsv 2> /dev/null || true)
    fi
    echo "attempt $attempt: ${name:-no revision} provisioning=${state:-?} image=${image_now:-?}" >&2
    if [ "$state" = Failed ]; then
      echo "::error::Grader revision $name failed to provision." >&2
      return 1
    fi
    if [ "$state" = Provisioned ] && [ "$image_now" = "$image" ]; then
      fqdn=$(az containerapp show --name "$APP" --resource-group "$GROUP" \
        --query properties.configuration.ingress.fqdn --output tsv)
      case "$fqdn" in
        *.internal.*) printf 'https://%s\n' "$fqdn" ;;
        *)
          echo "::error::$APP answers at '$fqdn', which is not an internal address. Its ingress must stay internal." >&2
          return 1
          ;;
      esac
      return 0
    fi
    sleep "$PAUSE"
  done
  echo "::error::The grader revision was not provisioned on $image in time." >&2
  return 1
}

self_test() {
  local dir failed=0 out code
  dir=$(mktemp -d)
  # shellcheck disable=SC2064 # expand now: the directory name is fixed
  trap "rm -rf '$dir'" EXIT
  mkdir "$dir/bin"
  # az: every call is logged. STUB_EXISTS=1 makes the app exist.
  # STUB_STATE is the revision's provisioning state. STUB_FQDN is its address.
  cat > "$dir/bin/az" <<'STUB'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$STUB_LOG"
case "$1 $2" in
  "containerapp show")
    if [[ " $* " == *" properties.configuration.ingress.fqdn "* ]]; then echo "$STUB_FQDN"; exit 0; fi
    [ "${STUB_EXISTS:-0}" = 1 ] || exit 3
    echo abera-grader ;;
  "containerapp revision")
    if [ "$3" = list ]; then echo abera-grader--r1; exit 0; fi
    if [[ " $* " == *" properties.provisioningState "* ]]; then echo "$STUB_STATE"; exit 0; fi
    echo "example.azurecr.io/abera-grader:abc" ;;
esac
exit 0
STUB
  chmod +x "$dir/bin/az"
  run() {
    : > "$dir/log"
    code=0
    out=$(env PATH="$dir/bin:$PATH" STUB_LOG="$dir/log" GRADER_WAIT_ATTEMPTS=2 GRADER_WAIT_SECONDS=0 "$@" \
      "$0" example.azurecr.io/abera-grader:abc 2> /dev/null) || code=$?
  }
  check() { if "${@:2}"; then echo "self-test: ok: $1"; else echo "self-test FAILED: $1" >&2; cat "$dir/log" >&2; failed=1; fi; }

  run STUB_EXISTS=0 STUB_STATE=Provisioned STUB_FQDN=abera-grader.internal.example.io
  check "a missing app is created and its internal address printed (exit $code)" [ "$code" -eq 0 ]
  check "the address is https and internal" [ "$out" = https://abera-grader.internal.example.io ]
  check "created with internal ingress, 0 to 1 replicas, 0.25 vCPU, and 0.5 GiB" \
    grep -q -- '--ingress internal --target-port 8080 --transport http --min-replicas 0 --max-replicas 1 --cpu 0.25 --memory 0.5Gi' "$dir/log"
  check "in the site's environment, pulling with its own identity" \
    grep -q -- '--environment aberaTechServer-env-20241221174945 --image example.azurecr.io/abera-grader:abc --registry-server example.azurecr.io --registry-identity system' "$dir/log"

  run STUB_EXISTS=1 STUB_STATE=Provisioned STUB_FQDN=abera-grader.internal.example.io
  check "an existing app is updated, not created (exit $code)" [ "$code" -eq 0 ]
  check "with the image alone" grep -q -- '^containerapp update --name abera-grader --resource-group .* --image example.azurecr.io/abera-grader:abc' "$dir/log"
  check "and nothing is created" bash -c "! grep -q 'containerapp create' '$dir/log'"

  run STUB_EXISTS=1 STUB_STATE=Failed STUB_FQDN=abera-grader.internal.example.io
  check "a failed revision fails the deploy (exit $code)" [ "$code" -eq 1 ]

  run STUB_EXISTS=1 STUB_STATE=Provisioning STUB_FQDN=abera-grader.internal.example.io
  check "a revision that never provisions fails the deploy (exit $code)" [ "$code" -eq 1 ]

  run STUB_EXISTS=1 STUB_STATE=Provisioned STUB_FQDN=abera-grader.example.io
  check "an address outside the environment fails the deploy (exit $code)" [ "$code" -eq 1 ]

  return "$failed"
}

if [ "${1:-}" = --self-test ]; then
  self_test
  exit $?
fi
if [ $# -ne 1 ]; then
  echo "usage: $0 <registry>/<image>:<tag> | --self-test" >&2
  exit 2
fi
deploy "$1"
