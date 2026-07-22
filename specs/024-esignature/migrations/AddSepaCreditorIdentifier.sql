START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721204237_AddSepaCreditorIdentifier') THEN
    ALTER TABLE tenants ADD "SepaCreditorIdentifier" character varying(35);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721204237_AddSepaCreditorIdentifier') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260721204237_AddSepaCreditorIdentifier', '10.0.10');
    END IF;
END $EF$;
COMMIT;

