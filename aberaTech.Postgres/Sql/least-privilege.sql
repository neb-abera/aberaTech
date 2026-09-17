-- Grants for the role that serves requests: rows, and nothing else.
--
-- Run once per database (scheduling, then fitness), connected to that database
-- as its owner, after the owner has applied migrations at least once. Safe to
-- run again. Plain SQL with no psql meta-commands, so the integration test
-- (DatabaseLeastPrivilegeTests) executes this very file; the two role names
-- arrive as session settings:
--
--   PGOPTIONS="-c abera.runtime_role=<runtime> -c abera.owner_role=<owner>" \
--     psql "host=... dbname=scheduling user=<owner>" -v ON_ERROR_STOP=1 \
--     -f aberaTech.Postgres/Sql/least-privilege.sql
--
-- It does not create roles. How a role comes to exist differs by place — a
-- password locally, pgaadauth_create_principal for an Entra managed identity
-- in Azure — and README.md beside this file covers both.
--
-- What the runtime role gets:
--   CONNECT on this database, USAGE on schema public,
--   SELECT/INSERT/UPDATE/DELETE on every table, now and in future,
--   USAGE/SELECT on every sequence, now and in future,
--   read-only on the migration history, so it can tell the schema is current.
-- What it does not get: CREATE on the schema or the database, TEMPORARY,
-- TRUNCATE, REFERENCES, TRIGGER, or ownership of anything — so no CREATE,
-- ALTER or DROP.
DO $grants$
DECLARE
    runtime_role text := current_setting('abera.runtime_role');
    owner_role   text := current_setting('abera.owner_role');
    db           text := current_database();
BEGIN
    IF runtime_role = owner_role THEN
        RAISE EXCEPTION 'The runtime role and the owner role must differ.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = runtime_role) THEN
        RAISE EXCEPTION 'Role % does not exist; create it first (see README.md).', runtime_role;
    END IF;

    -- PUBLIC's defaults are where an unprivileged role quietly gets more than
    -- it was given: TEMPORARY on every database, and before Postgres 15 CREATE
    -- on schema public. CONNECT is deliberately left with PUBLIC: who may log
    -- in is decided by the server's authentication, and revoking it here could
    -- lock out an administrator who is not this database's owner.
    EXECUTE format('REVOKE TEMPORARY ON DATABASE %I FROM PUBLIC', db);
    EXECUTE 'REVOKE CREATE ON SCHEMA public FROM PUBLIC';

    EXECUTE format('REVOKE ALL ON DATABASE %I FROM %I', db, runtime_role);
    EXECUTE format('REVOKE ALL ON SCHEMA public FROM %I', runtime_role);
    EXECUTE format('REVOKE ALL ON ALL TABLES IN SCHEMA public FROM %I', runtime_role);
    EXECUTE format('REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM %I', runtime_role);

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', db, runtime_role);
    EXECUTE format('GRANT USAGE ON SCHEMA public TO %I', runtime_role);
    EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO %I', runtime_role);
    EXECUTE format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO %I', runtime_role);

    -- Tables a later migration creates. Default privileges belong to the role
    -- that creates the objects, which is why the owner is named: they apply
    -- only to what that role creates from here on.
    EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
        owner_role, runtime_role);
    EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO %I',
        owner_role, runtime_role);

    -- The migration history is the owner's record. The runtime reads it to
    -- refuse a schema that is behind the code, and must not be able to forge
    -- "already applied".
    IF to_regclass('public."__EFMigrationsHistory"') IS NOT NULL THEN
        EXECUTE format('REVOKE INSERT, UPDATE, DELETE ON public."__EFMigrationsHistory" FROM %I', runtime_role);
    END IF;
END
$grants$;
