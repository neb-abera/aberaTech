# Database least privilege

Today one identity does everything: the container app's managed identity
connects to `scheduling` and `fitness`, applies EF migrations at start-up, and
then serves anonymous traffic with the same connection. It therefore owns every
table, and a SQL injection anywhere in the app would be one with `DROP TABLE`.

The code supports splitting that in two, and changes nothing until you do:

| Piece | What it is |
|---|---|
| `dotnet aberaTech.Server.dll migrate` | Applies pending migrations to every configured database as whoever the connection strings name, then exits. Same image, no web server. Exit code 1 on failure. |
| `Database:MigrateOnStart` (`Database__MigrateOnStart`) | `true` by default (today's behaviour). `false` makes the server refuse to boot against a schema with pending migrations, instead of applying them. |
| `least-privilege.sql` | Grants a runtime role `SELECT/INSERT/UPDATE/DELETE` and sequence usage, now and for future tables, and nothing else. |

`DatabaseLeastPrivilegeTests` runs all three against the compose Postgres
(`make dbtest`): migrate as an owner, apply this script, serve as the runtime
role, and prove the runtime role is refused `CREATE`, `ALTER`, `DROP`,
`TRUNCATE`, temp tables and writes to the migration history.

## Trying it locally

```bash
make db
docker compose exec -T db psql -U scheduling -d postgres \
  -c "CREATE ROLE abera_runtime LOGIN PASSWORD 'runtime'"
make up            # migrates as `scheduling`, the owner, as it always has
for database in scheduling fitness; do
  docker compose exec -T -e PGOPTIONS="-c abera.runtime_role=abera_runtime -c abera.owner_role=scheduling" \
    db psql -U scheduling -d "$database" -v ON_ERROR_STOP=1 -f - \
    < aberaTech.Postgres/Sql/least-privilege.sql
done
```

Then run the app with `Username=abera_runtime;Password=runtime` in both
connection strings and `Database__MigrateOnStart=false`.

## Production switch-over (owner-run)

Nothing here is automated, because every step needs rights the repository
does not and should not hold. Until the last step the app keeps running exactly
as it does now, and each step is reversible.

Names below: `abera-postgres` is the server. **App identity** is the container
app's existing system-assigned managed identity (it owns the tables today,
because it created them). **Migrator identity** is new.

1. **Decide who the owner is.** The least disruptive choice is to leave the
   tables with a dedicated owner role and move the app *off* it:
   create a user-assigned managed identity `abera-migrator`, and as the Entra
   administrator, connected to the `postgres` database:

   ```sql
   SELECT * FROM pgaadauth_create_principal('abera-migrator', false, false);
   ```

2. **Hand the schema to the migrator**, in each of `scheduling` and `fitness`,
   connected as the current owner (the app identity's role) or the
   administrator. `REASSIGN OWNED` moves tables, sequences and the migration
   history in one statement. The database itself is altered separately.

   ```sql
   GRANT "abera-migrator" TO CURRENT_USER;      -- needed to give ownership away
   REASSIGN OWNED BY "<app identity role>" TO "abera-migrator";
   ALTER DATABASE scheduling OWNER TO "abera-migrator";   -- and fitness
   ```

   `scheduling` uses the `btree_gist` extension, already installed. Nothing
   about it changes. A future migration that adds an extension needs it
   allow-listed in `azure.extensions`, as now.

3. **Apply the grants**, once per database, connected as `abera-migrator` (or
   the administrator with that role granted), with the app identity as the
   runtime role:

   ```bash
   export PGPASSWORD="$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv)"
   for database in scheduling fitness; do
     PGOPTIONS="-c abera.runtime_role=<app identity role> -c abera.owner_role=abera-migrator" \
       psql "host=abera-postgres.postgres.database.azure.com dbname=$database user=<you> sslmode=require" \
       -v ON_ERROR_STOP=1 -f aberaTech.Postgres/Sql/least-privilege.sql
   done
   ```

   At this point the app identity can no longer run DDL, so **a deploy
   carrying a new migration would fail to start** until step 4 exists. Do 3–5
   together, between feature merges.

4. **Give migrations somewhere to run.** A Container Apps job on the same
   environment and image, with `abera-migrator` assigned, `args: ["migrate"]`,
   `Database__UseEntraAuth=true`, `AZURE_CLIENT_ID` set to the migrator's
   client id, and both connection strings with `Username=abera-migrator`. Then
   the deploy workflow starts it before updating the app. The sketch, for the
   `deploy` job in `.github/workflows/aberatechserver-app-202412211749.yml`,
   ahead of "Deploy to Azure Container App". It is not added yet, because the
   job and identity it names do not exist:

   ```yaml
   - name: Apply database migrations as the migrator identity
     uses: azure/CLI@<pinned sha, as the steps around it>
     with:
       inlineScript: |
         set -euo pipefail
         az config set extension.use_dynamic_install=yes_without_prompt
         az containerapp job update --name abera-migrate \
           --resource-group "${{ env.CONTAINER_APP_RESOURCE_GROUP_NAME }}" \
           --image "${{ env.CONTAINER_REGISTRY_LOGIN_SERVER }}/${{ env.PROJECT_NAME_FOR_DOCKER }}:${{ github.sha }}"
         execution=$(az containerapp job start --name abera-migrate \
           --resource-group "${{ env.CONTAINER_APP_RESOURCE_GROUP_NAME }}" --query name -o tsv)
         for _ in $(seq 1 40); do
           status=$(az containerapp job execution show --name abera-migrate \
             --resource-group "${{ env.CONTAINER_APP_RESOURCE_GROUP_NAME }}" \
             --job-execution-name "$execution" --query properties.status -o tsv)
           case "$status" in
             Succeeded) exit 0 ;;
             Failed | Stopped | Degraded) echo "::error::migration job ${status}"; exit 1 ;;
           esac
           sleep 15
         done
         echo "::error::migration job did not finish"; exit 1
   ```

   The deploy identity needs `Microsoft.App/jobs/start/action` and write on
   that one job. It still never holds a database credential.

5. **Flip the switch.** Set `Database__MigrateOnStart=false` on the container
   app. From here a revision whose migrations were not applied fails to start
   and the previous revision keeps serving, which the deploy workflow's
   "Wait for the new revision to run" step reports.

Rolling back is the reverse of 5, then `GRANT "abera-migrator" TO "<app
identity role>"` so the app can migrate again.

Migrations must stay backwards compatible by one release while this is in
place: the job migrates before the new revision takes traffic, so the old
revision serves against the new schema for a moment.
