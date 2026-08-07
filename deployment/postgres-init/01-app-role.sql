-- Creates the runtime role the API connects as. It is deliberately NOT a
-- superuser and NOT the table owner: PostgreSQL superusers bypass row-level
-- security, so connecting the app as one would silently disable the RLS
-- tenant-isolation layer. Migrations run as the owning role (schoolerp);
-- the API runs as schoolerp_app.
CREATE ROLE schoolerp_app LOGIN PASSWORD 'schoolerp_app_dev_only' NOSUPERUSER NOCREATEDB NOCREATEROLE;

GRANT CONNECT ON DATABASE schoolerp TO schoolerp_app;
GRANT USAGE ON SCHEMA public TO schoolerp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO schoolerp_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO schoolerp_app;

-- Tables created by future migrations automatically get the same grants.
ALTER DEFAULT PRIVILEGES FOR ROLE schoolerp IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO schoolerp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE schoolerp IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO schoolerp_app;
