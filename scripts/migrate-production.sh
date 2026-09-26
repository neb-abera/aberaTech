#!/usr/bin/env bash
#
# migrate-production.sh: run the image's migrate verb against the production
# databases as the Postgres role abera-migrator, with the Entra token of
# whoever `az` is signed in as. In the deploy workflow that is the
# abera-migrator managed identity, through its GitHub federated credential.
#
#   scripts/migrate-production.sh <image> list    name pending migrations, apply nothing
#   scripts/migrate-production.sh <image> apply   apply them
#
# The token is the password. It lasts about an hour and reaches the container
# through an env file that is mode 600 and deleted on exit, so it is never on
# a command line. The container's exit code is this script's exit code.
#
# POSTGRES_HOST and MIGRATOR_ROLE override the server and the role. The
# pull request that added this ran it once with a role that does not exist,
# to prove a refused sign-in fails the step.
set -euo pipefail

usage="usage: migrate-production.sh <image> list|apply"
image="${1:?$usage}"
mode="${2:?$usage}"
host="${POSTGRES_HOST:-abera-postgres.postgres.database.azure.com}"
role="${MIGRATOR_ROLE:-abera-migrator}"

case "$mode" in
  list) args=(migrate list) ;;
  apply) args=(migrate) ;;
  *)
    echo "error: ${usage}" >&2
    exit 2
    ;;
esac

token="$(az account get-access-token --resource-type oss-rdbms --query accessToken --output tsv)"
if [ -z "$token" ]; then
  echo "error: az returned no Postgres access token" >&2
  exit 1
fi
if [ -n "${GITHUB_ACTIONS:-}" ]; then
  echo "::add-mask::${token}"
fi

env_file="$(mktemp)"
trap 'rm -f "$env_file"' EXIT
chmod 600 "$env_file"
{
  for database in scheduling fitness; do
    key="$(printf '%s' "${database:0:1}" | tr '[:lower:]' '[:upper:]')${database:1}"
    printf 'ConnectionStrings__%s=Host=%s;Port=5432;Database=%s;Username=%s;Password=%s;SslMode=Require\n' \
      "$key" "$host" "$database" "$role" "$token"
  done
  # The token above is the credential. Entra mode would ask the container for
  # a managed identity it does not have.
  printf 'Database__UseEntraAuth=false\n'
} > "$env_file"

echo "migrate ${mode}: ${host} as ${role}, image ${image}"
docker run --rm --env-file "$env_file" "$image" "${args[@]}"
