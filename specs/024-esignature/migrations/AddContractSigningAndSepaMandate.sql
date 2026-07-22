START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    ALTER TABLE tenant_template.contracts ADD "SepaAuthorisedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    ALTER TABLE tenant_template.contracts ADD "SepaIbanEncrypted" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    ALTER TABLE tenant_template.contracts ADD "SepaIbanLast4" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    ALTER TABLE tenant_template.contracts ADD "SepaMandateReference" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    ALTER TABLE tenant_template.contracts ADD "SignatureData" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    ALTER TABLE tenant_template.contracts ADD "SignatureType" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    ALTER TABLE tenant_template.contracts ADD "SignedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    ALTER TABLE tenant_template.contracts ADD "SignedByIp" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    ALTER TABLE tenant_template.contracts ADD "SigningToken" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    ALTER TABLE tenant_template.contracts ADD "SigningTokenExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    CREATE UNIQUE INDEX "IX_contracts_SepaMandateReference" ON tenant_template.contracts ("SepaMandateReference") WHERE "SepaMandateReference" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    CREATE UNIQUE INDEX "IX_contracts_SigningToken" ON tenant_template.contracts ("SigningToken") WHERE "SigningToken" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260721204218_AddContractSigningAndSepaMandate') THEN
    INSERT INTO tenant_template."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260721204218_AddContractSigningAndSepaMandate', '10.0.10');
    END IF;
END $EF$;
COMMIT;

