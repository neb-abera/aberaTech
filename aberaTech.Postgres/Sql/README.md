# Database least privilege

Two identities touch the `scheduling` and `fitness` databases on
`abera-postgres`. Neither holds a password.

| Identity | Postgres role | Rights | Used by |
|---|---|---|---|
| `abera-migrator`, a user-assigned managed identity in the app's resource group | `abera-migrator` | Owns every table, sequence and the migration history. `USAGE, CREATE` on schema `public` | The `migrate` job of the deploy workflow, and nothing else |
| The container app's system-assigned identity | `aberatechserver-app-202412211749` | `SELECT, INSERT, UPDATE, DELETE` on tables, `USAGE, SELECT` on sequences, `SELECT` on the migration history. No `CREATE`, no `TEMPORARY`, no ownership | Serving requests |

`abera-migrator` holds no Azure role. It trusts one thing: a GitHub token for
this repository's `master` branch, through two federated credentials, one per
subject format (`repo:neb-abera/aberaTech:ref:refs/heads/master` and
`repo:neb-abera@29741322/aberaTech@906788580:ref:refs/heads/master`). Its
client id is the repository variable `DATABASE_MIGRATOR_CLIENT_ID`.

## How a deploy migrates

The deploy workflow runs `build`, then `migrate`, then `deploy`.

1. `migrate` pulls the image `build` pushed, signs in to Azure as
   `abera-migrator`, and runs `scripts/migrate-production.sh`. The script
   takes an Entra token for Postgres and runs the image twice:
   `dotnet aberaTech.Server.dll migrate list` names the pending migrations,
   then `migrate` applies them. The token reaches the container in a mode 600
   env file and is masked in the log.
2. `deploy` starts a revision of the same image. Outside Development
   `Database:MigrateOnStart` is `false`, so the server only checks the
   history. It refuses to boot against a schema that is behind it, and the
   previous revision keeps serving.

A failed `migrate` stops the run before `deploy`. The revision that is
serving keeps its schema, because nothing was applied or the migration's own
transaction rolled back.

A migration must work with the revision before it for one release. That
revision serves on the new schema until the new one is ready.

## The pieces in code

| Piece | What it is |
|---|---|
| `dotnet aberaTech.Server.dll migrate` | Applies pending migrations to every configured database as whoever the connection strings name, then exits. Exit code 1 on failure |
| `dotnet aberaTech.Server.dll migrate list` | Names the pending migrations per database and applies nothing. It does not create the history table |
| `Database:MigrateOnStart` | `false` by default. `true` in `appsettings.Development.json`, so `make up` and `make dev` migrate as the compose owner |
| `least-privilege.sql` | Grants the runtime role rows and sequences, now and for tables the owner creates later, and read-only history |

`DatabaseLeastPrivilegeTests` runs all of it against the compose Postgres
(`make dbtest`). It migrates as an owner, applies `least-privilege.sql`,
serves as the runtime role, and proves that role is refused `CREATE TABLE`,
`CREATE SCHEMA`, `ALTER TABLE`, `DROP TABLE`, `TRUNCATE`, temp tables and
writes to the migration history. `Listing_pending_migrations_changes_nothing`
proves `migrate list` writes nothing.

## Trying it locally

```bash
make db
docker compose exec -T db psql -U scheduling -d postgres \
  -c "CREATE ROLE abera_runtime LOGIN PASSWORD 'runtime'"
make up            # Development: migrates as `scheduling`, the owner
for database in scheduling fitness; do
  docker compose exec -T -e PGOPTIONS="-c abera.runtime_role=abera_runtime -c abera.owner_role=scheduling" \
    db psql -U scheduling -d "$database" -v ON_ERROR_STOP=1 -f - \
    < aberaTech.Postgres/Sql/least-privilege.sql
done
```

Then run the app with `Username=abera_runtime;Password=runtime` in both
connection strings and `Database__MigrateOnStart=false`.

## How production was set up

On 2026-09-26, as the Entra administrator. The admin connects with
`az account get-access-token --resource-type oss-rdbms` as the password.

```bash
RG=aberatechserver-app-202412211749ResourceGroup
az identity create -g "$RG" -n abera-migrator -l eastus
az identity federated-credential create -g "$RG" --identity-name abera-migrator \
  -n github-master --issuer https://token.actions.githubusercontent.com \
  --subject repo:neb-abera/aberaTech:ref:refs/heads/master --audiences api://AzureADTokenExchange
az identity federated-credential create -g "$RG" --identity-name abera-migrator \
  -n github-master-immutable --issuer https://token.actions.githubusercontent.com \
  --subject repo:neb-abera@29741322/aberaTech@906788580:ref:refs/heads/master --audiences api://AzureADTokenExchange
gh variable set DATABASE_MIGRATOR_CLIENT_ID -R neb-abera/aberaTech -b "$(az identity show -g "$RG" -n abera-migrator --query clientId -o tsv)"
```

In the `postgres` database:

```sql
SELECT * FROM pgaadauth_create_principal_with_oid('abera-migrator', '<principal id>', 'service', false, false);
```

In each of `scheduling` and `fitness`, in one transaction, so the app never
loses access to a table:

```sql
BEGIN;
GRANT "aberatechserver-app-202412211749" TO CURRENT_USER WITH INHERIT TRUE, SET TRUE;
GRANT "abera-migrator" TO CURRENT_USER WITH INHERIT TRUE, SET TRUE;
GRANT USAGE, CREATE ON SCHEMA public TO "abera-migrator";
REASSIGN OWNED BY "aberatechserver-app-202412211749" TO "abera-migrator";
GRANT "abera-migrator" TO "aberatechserver-app-202412211749";  -- until the migrate job was green
SET abera.runtime_role = 'aberatechserver-app-202412211749';
SET abera.owner_role = 'abera-migrator';
\i aberaTech.Postgres/Sql/least-privilege.sql
COMMIT;
```

After the first deploy whose `migrate` job was green:

```sql
REVOKE "abera-migrator" FROM "aberatechserver-app-202412211749";
```

## Rolling back

Each step has its reverse. Run them newest first.

| To undo | Run |
|---|---|
| The app's loss of DDL | `GRANT "abera-migrator" TO "aberatechserver-app-202412211749";` |
| Migrations outside the app | `az containerapp update -g "$RG" -n aberatechserver-app-202412211749 --set-env-vars Database__MigrateOnStart=true`, with the grant above |
| The `migrate` job | Revert the workflow change. The app then needs both lines above |
| The handover | In each database, `REASSIGN OWNED BY "abera-migrator" TO "aberatechserver-app-202412211749";` then `GRANT CREATE ON SCHEMA public TO "aberatechserver-app-202412211749";` |

None of these touches a row.
