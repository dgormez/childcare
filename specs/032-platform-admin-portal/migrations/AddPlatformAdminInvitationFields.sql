CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702195919_InitialPublicSchema') THEN
    CREATE TABLE invitations (
        "Id" uuid NOT NULL,
        "Email" character varying(254) NOT NULL,
        "TokenHash" bytea NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_invitations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702195919_InitialPublicSchema') THEN
    CREATE TABLE tenants (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Slug" character varying(200) NOT NULL,
        "SchemaName" character varying(63) NOT NULL,
        "Plan" character varying(20) NOT NULL,
        "ProvisioningStatus" character varying(20) NOT NULL,
        "CreatedFromInvitationId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_tenants" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_tenants_plan" CHECK ("Plan" IN ('trial','starter','pro')),
        CONSTRAINT "CK_tenants_provisioning_status" CHECK ("ProvisioningStatus" IN ('provisioning','ready','failed'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702195919_InitialPublicSchema') THEN
    CREATE UNIQUE INDEX "IX_tenants_CreatedFromInvitationId" ON tenants ("CreatedFromInvitationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702195919_InitialPublicSchema') THEN
    CREATE UNIQUE INDEX "IX_tenants_SchemaName" ON tenants ("SchemaName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702195919_InitialPublicSchema') THEN
    CREATE UNIQUE INDEX "IX_tenants_Slug" ON tenants ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260702195919_InitialPublicSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260702195919_InitialPublicSchema', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713191108_AddVaccineTypeCatalog') THEN
    CREATE TABLE vaccine_types (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Category" character varying(30),
        "SortOrder" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_vaccine_types" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713191108_AddVaccineTypeCatalog') THEN
    CREATE INDEX "IX_vaccine_types_Category_SortOrder" ON vaccine_types ("Category", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713191108_AddVaccineTypeCatalog') THEN
    CREATE INDEX "IX_vaccine_types_IsActive" ON vaccine_types ("IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713191108_AddVaccineTypeCatalog') THEN
    INSERT INTO vaccine_types ("Id", "Name", "Category", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES ('b1e6f0a0-0001-4a00-8000-000000000001', 'DTPa-IPV-Hib-HepB', 'basisvaccinatieschema', 1, TRUE, TIMESTAMPTZ '2026-07-13T00:00:00Z', TIMESTAMPTZ '2026-07-13T00:00:00Z');
    INSERT INTO vaccine_types ("Id", "Name", "Category", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES ('b1e6f0a0-0001-4a00-8000-000000000002', 'Pneumokokken (PCV)', 'basisvaccinatieschema', 2, TRUE, TIMESTAMPTZ '2026-07-13T00:00:00Z', TIMESTAMPTZ '2026-07-13T00:00:00Z');
    INSERT INTO vaccine_types ("Id", "Name", "Category", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES ('b1e6f0a0-0001-4a00-8000-000000000003', 'BMR (bof, mazelen, rodehond)', 'basisvaccinatieschema', 3, TRUE, TIMESTAMPTZ '2026-07-13T00:00:00Z', TIMESTAMPTZ '2026-07-13T00:00:00Z');
    INSERT INTO vaccine_types ("Id", "Name", "Category", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES ('b1e6f0a0-0001-4a00-8000-000000000004', 'MenACWY', 'basisvaccinatieschema', 4, TRUE, TIMESTAMPTZ '2026-07-13T00:00:00Z', TIMESTAMPTZ '2026-07-13T00:00:00Z');
    INSERT INTO vaccine_types ("Id", "Name", "Category", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES ('b1e6f0a0-0001-4a00-8000-000000000005', 'HPV', 'basisvaccinatieschema', 5, TRUE, TIMESTAMPTZ '2026-07-13T00:00:00Z', TIMESTAMPTZ '2026-07-13T00:00:00Z');
    INSERT INTO vaccine_types ("Id", "Name", "Category", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES ('b1e6f0a0-0001-4a00-8000-000000000006', 'RSV (zuigelingen)', 'aanbevolen_niet_gratis', 1, TRUE, TIMESTAMPTZ '2026-07-13T00:00:00Z', TIMESTAMPTZ '2026-07-13T00:00:00Z');
    INSERT INTO vaccine_types ("Id", "Name", "Category", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES ('b1e6f0a0-0001-4a00-8000-000000000007', 'MenB', 'aanbevolen_niet_gratis', 2, TRUE, TIMESTAMPTZ '2026-07-13T00:00:00Z', TIMESTAMPTZ '2026-07-13T00:00:00Z');
    INSERT INTO vaccine_types ("Id", "Name", "Category", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES ('b1e6f0a0-0001-4a00-8000-000000000008', 'Hepatitis A', 'aanbevolen_niet_gratis', 3, TRUE, TIMESTAMPTZ '2026-07-13T00:00:00Z', TIMESTAMPTZ '2026-07-13T00:00:00Z');
    INSERT INTO vaccine_types ("Id", "Name", "Category", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES ('b1e6f0a0-0001-4a00-8000-000000000009', 'Waterpokken (varicella)', 'aanbevolen_niet_gratis', 4, TRUE, TIMESTAMPTZ '2026-07-13T00:00:00Z', TIMESTAMPTZ '2026-07-13T00:00:00Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713191108_AddVaccineTypeCatalog') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713191108_AddVaccineTypeCatalog', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713211236_AddVaccineTypeDeactivationAudit') THEN
    ALTER TABLE vaccine_types ADD "DeactivatedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713211236_AddVaccineTypeDeactivationAudit') THEN
    ALTER TABLE vaccine_types ADD "DeactivatedByEmail" character varying(254);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713211236_AddVaccineTypeDeactivationAudit') THEN
    ALTER TABLE vaccine_types ADD "DeactivatedByUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713211236_AddVaccineTypeDeactivationAudit') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713211236_AddVaccineTypeDeactivationAudit', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715121504_AddTenantKboNumber') THEN
    ALTER TABLE tenants ADD "KboNumber" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260715121504_AddTenantKboNumber') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260715121504_AddTenantKboNumber', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716075934_AddPaymentProviderConnectionsAndPayments') THEN
    CREATE TABLE payment_provider_connections (
        "Id" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Provider" character varying(20) NOT NULL,
        "ProviderAccountId" character varying(200) NOT NULL,
        "ProviderAccountLabel" character varying(200) NOT NULL,
        "EncryptedAccessToken" text NOT NULL,
        "EncryptedRefreshToken" text NOT NULL,
        "TokenExpiresAt" timestamp with time zone NOT NULL,
        "Status" character varying(20) NOT NULL,
        "ConnectedAt" timestamp with time zone NOT NULL,
        "DisconnectedAt" timestamp with time zone,
        CONSTRAINT "PK_payment_provider_connections" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716075934_AddPaymentProviderConnectionsAndPayments') THEN
    CREATE TABLE payments (
        "Id" uuid NOT NULL,
        "PaymentReference" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "InvoiceId" uuid NOT NULL,
        "ProviderPaymentId" character varying(100),
        "Status" character varying(20) NOT NULL,
        "AmountCents" integer NOT NULL,
        "FeeCents" integer,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_payments" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716075934_AddPaymentProviderConnectionsAndPayments') THEN
    CREATE UNIQUE INDEX "IX_payment_provider_connections_TenantId" ON payment_provider_connections ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716075934_AddPaymentProviderConnectionsAndPayments') THEN
    CREATE UNIQUE INDEX "IX_payments_PaymentReference" ON payments ("PaymentReference");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716075934_AddPaymentProviderConnectionsAndPayments') THEN
    CREATE INDEX "IX_payments_TenantId_InvoiceId_Status" ON payments ("TenantId", "InvoiceId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716075934_AddPaymentProviderConnectionsAndPayments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260716075934_AddPaymentProviderConnectionsAndPayments', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716185654_AddDevelopmentalMilestoneCatalog') THEN
    CREATE TABLE developmental_domains (
        "Id" uuid NOT NULL,
        "Code" character varying(30) NOT NULL,
        "NameNl" character varying(100) NOT NULL,
        "NameFr" character varying(100) NOT NULL,
        "NameEn" character varying(100) NOT NULL,
        "SortOrder" integer NOT NULL,
        CONSTRAINT "PK_developmental_domains" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716185654_AddDevelopmentalMilestoneCatalog') THEN
    CREATE TABLE developmental_milestones (
        "Id" uuid NOT NULL,
        "DomainId" uuid NOT NULL,
        "AgeFromMonths" integer NOT NULL,
        "AgeToMonths" integer NOT NULL,
        "DescriptionNl" text NOT NULL,
        "DescriptionFr" text NOT NULL,
        "DescriptionEn" text NOT NULL,
        "SortOrder" integer NOT NULL,
        CONSTRAINT "PK_developmental_milestones" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_developmental_milestones_developmental_domains_DomainId" FOREIGN KEY ("DomainId") REFERENCES developmental_domains ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716185654_AddDevelopmentalMilestoneCatalog') THEN
    CREATE UNIQUE INDEX "IX_developmental_domains_Code" ON developmental_domains ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716185654_AddDevelopmentalMilestoneCatalog') THEN
    CREATE INDEX "IX_developmental_milestones_AgeFromMonths_AgeToMonths" ON developmental_milestones ("AgeFromMonths", "AgeToMonths");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716185654_AddDevelopmentalMilestoneCatalog') THEN
    CREATE INDEX "IX_developmental_milestones_DomainId_SortOrder" ON developmental_milestones ("DomainId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716185654_AddDevelopmentalMilestoneCatalog') THEN
    INSERT INTO developmental_domains ("Id", "Code", "NameNl", "NameFr", "NameEn", "SortOrder")
    VALUES ('d1000000-0000-4a00-8000-000000000000', 'motor_gross', 'Grove motoriek', 'Motricité globale', 'Gross motor', 1);
    INSERT INTO developmental_domains ("Id", "Code", "NameNl", "NameFr", "NameEn", "SortOrder")
    VALUES ('d2000000-0000-4a00-8000-000000000000', 'motor_fine', 'Fijne motoriek', 'Motricité fine', 'Fine motor', 2);
    INSERT INTO developmental_domains ("Id", "Code", "NameNl", "NameFr", "NameEn", "SortOrder")
    VALUES ('d3000000-0000-4a00-8000-000000000000', 'language', 'Taal', 'Langage', 'Language', 3);
    INSERT INTO developmental_domains ("Id", "Code", "NameNl", "NameFr", "NameEn", "SortOrder")
    VALUES ('d4000000-0000-4a00-8000-000000000000', 'cognitive', 'Cognitie', 'Cognition', 'Cognitive', 4);
    INSERT INTO developmental_domains ("Id", "Code", "NameNl", "NameFr", "NameEn", "SortOrder")
    VALUES ('d5000000-0000-4a00-8000-000000000000', 'social', 'Sociaal', 'Social', 'Social', 5);
    INSERT INTO developmental_domains ("Id", "Code", "NameNl", "NameFr", "NameEn", "SortOrder")
    VALUES ('d6000000-0000-4a00-8000-000000000000', 'emotional', 'Emotioneel', 'Émotionnel', 'Emotional', 6);
    INSERT INTO developmental_domains ("Id", "Code", "NameNl", "NameFr", "NameEn", "SortOrder")
    VALUES ('d7000000-0000-4a00-8000-000000000000', 'self_care', 'Zelfredzaamheid', 'Autonomie', 'Self-care', 7);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716185654_AddDevelopmentalMilestoneCatalog') THEN
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e1000001-0000-4a00-8000-000000000000', 'd1000000-0000-4a00-8000-000000000000', 0, 3, 'Til het hoofdje even op in buikligging', 'Soulève brièvement la tête en position ventrale', 'Briefly lifts head while lying on tummy', 1);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e1000002-0000-4a00-8000-000000000000', 'd1000000-0000-4a00-8000-000000000000', 4, 6, 'Rolt van buik naar rug (en omgekeerd)', 'Se retourne du ventre vers le dos (et inversement)', 'Rolls from tummy to back (and back again)', 2);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e1000003-0000-4a00-8000-000000000000', 'd1000000-0000-4a00-8000-000000000000', 7, 12, 'Kruipt zelfstandig vooruit', 'Rampe de façon autonome', 'Crawls independently', 3);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e1000004-0000-4a00-8000-000000000000', 'd1000000-0000-4a00-8000-000000000000', 13, 18, 'Loopt zelfstandig enkele stappen', 'Marche seul sur quelques pas', 'Walks independently for a few steps', 4);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e1000005-0000-4a00-8000-000000000000', 'd1000000-0000-4a00-8000-000000000000', 19, 24, 'Klimt op en van laag meubilair', 'Monte et descend d''un meuble bas', 'Climbs onto and off low furniture', 5);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e1000006-0000-4a00-8000-000000000000', 'd1000000-0000-4a00-8000-000000000000', 25, 36, 'Springt met beide voeten tegelijk van de grond', 'Saute à pieds joints', 'Jumps with both feet off the ground', 6);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e2000001-0000-4a00-8000-000000000000', 'd2000000-0000-4a00-8000-000000000000', 0, 3, 'Opent en sluit de handjes bewust', 'Ouvre et ferme les mains consciemment', 'Opens and closes hands purposefully', 1);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e2000002-0000-4a00-8000-000000000000', 'd2000000-0000-4a00-8000-000000000000', 4, 6, 'Grijpt bewust naar een voorwerp', 'Attrape un objet de façon intentionnelle', 'Reaches for and grasps an object intentionally', 2);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e2000003-0000-4a00-8000-000000000000', 'd2000000-0000-4a00-8000-000000000000', 7, 12, 'Gebruikt duim en wijsvinger (pincetgreep) om kleine dingen op te pakken', 'Utilise le pouce et l''index (prise en pince) pour ramasser de petits objets', 'Uses thumb and forefinger (pincer grasp) to pick up small items', 3);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e2000004-0000-4a00-8000-000000000000', 'd2000000-0000-4a00-8000-000000000000', 13, 18, 'Bouwt een torentje van 2 blokjes', 'Construit une tour de 2 cubes', 'Builds a tower of 2 blocks', 4);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e2000005-0000-4a00-8000-000000000000', 'd2000000-0000-4a00-8000-000000000000', 19, 24, 'Bladert zelfstandig (dikke) bladzijden om in een boek', 'Tourne seul les pages (épaisses) d''un livre', 'Turns (thick) pages of a book independently', 5);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e2000006-0000-4a00-8000-000000000000', 'd2000000-0000-4a00-8000-000000000000', 25, 36, 'Tekent een verticale en horizontale lijn na', 'Reproduit une ligne verticale et horizontale', 'Copies a vertical and horizontal line', 6);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e3000001-0000-4a00-8000-000000000000', 'd3000000-0000-4a00-8000-000000000000', 0, 3, 'Reageert op geluid door te stoppen of om te draaien', 'Réagit à un son en s''arrêtant ou en se tournant', 'Reacts to sound by pausing or turning', 1);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e3000002-0000-4a00-8000-000000000000', 'd3000000-0000-4a00-8000-000000000000', 4, 6, 'Brabbelt met verschillende klanken', 'Babille avec différents sons', 'Babbles using different sounds', 2);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e3000003-0000-4a00-8000-000000000000', 'd3000000-0000-4a00-8000-000000000000', 7, 12, 'Zegt de eerste woordjes met betekenis (bv. ''mama'', ''papa'')', 'Dit ses premiers mots avec du sens (p.ex. « maman », « papa »)', 'Says first meaningful words (e.g. ''mama'', ''dada'')', 3);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e3000004-0000-4a00-8000-000000000000', 'd3000000-0000-4a00-8000-000000000000', 13, 18, 'Begrijpt en volgt een eenvoudige opdracht (''geef de bal'')', 'Comprend et suit une consigne simple (« donne le ballon »)', 'Understands and follows a simple instruction (''give the ball'')', 4);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e3000005-0000-4a00-8000-000000000000', 'd3000000-0000-4a00-8000-000000000000', 19, 24, 'Combineert twee woorden tot een zinnetje (''mama weg'')', 'Combine deux mots en une petite phrase (« maman partie »)', 'Combines two words into a short phrase (''mommy gone'')', 5);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e3000006-0000-4a00-8000-000000000000', 'd3000000-0000-4a00-8000-000000000000', 25, 36, 'Vertelt in korte zinnen van 3-4 woorden', 'Raconte en phrases courtes de 3 à 4 mots', 'Talks in short 3-4 word sentences', 6);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e4000001-0000-4a00-8000-000000000000', 'd4000000-0000-4a00-8000-000000000000', 0, 3, 'Volgt een bewegend voorwerp met de ogen', 'Suit un objet en mouvement des yeux', 'Follows a moving object with the eyes', 1);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e4000002-0000-4a00-8000-000000000000', 'd4000000-0000-4a00-8000-000000000000', 4, 6, 'Zoekt naar de bron van een geluid', 'Cherche la source d''un son', 'Searches for the source of a sound', 2);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e4000003-0000-4a00-8000-000000000000', 'd4000000-0000-4a00-8000-000000000000', 7, 12, 'Begrijpt objectpermanentie (zoekt een verstopt voorwerp)', 'Comprend la permanence de l''objet (cherche un objet caché)', 'Understands object permanence (looks for a hidden object)', 3);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e4000004-0000-4a00-8000-000000000000', 'd4000000-0000-4a00-8000-000000000000', 13, 18, 'Gebruikt een voorwerp op de bedoelde manier (bv. drinkt uit bekertje)', 'Utilise un objet de la manière prévue (p.ex. boit dans un gobelet)', 'Uses an object the way it''s intended (e.g. drinks from a cup)', 4);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e4000005-0000-4a00-8000-000000000000', 'd4000000-0000-4a00-8000-000000000000', 19, 24, 'Doet eenvoudig doen-alsof-spel na (bv. poppetje eten geven)', 'Imite un jeu de faire-semblant simple (p.ex. donner à manger à une poupée)', 'Imitates simple pretend play (e.g. feeding a doll)', 5);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e4000006-0000-4a00-8000-000000000000', 'd4000000-0000-4a00-8000-000000000000', 25, 36, 'Sorteert voorwerpen op één kenmerk (kleur of vorm)', 'Trie des objets selon une caractéristique (couleur ou forme)', 'Sorts objects by one feature (colour or shape)', 6);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e5000001-0000-4a00-8000-000000000000', 'd5000000-0000-4a00-8000-000000000000', 0, 3, 'Kijkt naar en volgt gezichten', 'Regarde et suit les visages', 'Looks at and tracks faces', 1);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e5000002-0000-4a00-8000-000000000000', 'd5000000-0000-4a00-8000-000000000000', 4, 6, 'Lacht spontaan naar een bekend gezicht', 'Sourit spontanément à un visage familier', 'Smiles spontaneously at a familiar face', 2);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e5000003-0000-4a00-8000-000000000000', 'd5000000-0000-4a00-8000-000000000000', 7, 12, 'Speelt kiekeboe mee', 'Participe au jeu de coucou', 'Participates in peekaboo', 3);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e5000004-0000-4a00-8000-000000000000', 'd5000000-0000-4a00-8000-000000000000', 13, 18, 'Speelt naast andere kinderen (parallel spel)', 'Joue à côté d''autres enfants (jeu parallèle)', 'Plays alongside other children (parallel play)', 4);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e5000005-0000-4a00-8000-000000000000', 'd5000000-0000-4a00-8000-000000000000', 19, 24, 'Toont interesse in wat een ander kind doet', 'Montre de l''intérêt pour ce que fait un autre enfant', 'Shows interest in what another child is doing', 5);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e5000006-0000-4a00-8000-000000000000', 'd5000000-0000-4a00-8000-000000000000', 25, 36, 'Speelt kort samen met een ander kind in een gedeeld spel', 'Joue brièvement avec un autre enfant dans un jeu partagé', 'Briefly plays together with another child in shared play', 6);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e6000001-0000-4a00-8000-000000000000', 'd6000000-0000-4a00-8000-000000000000', 0, 3, 'Kalmeert bij troost van een vertrouwd persoon', 'Se calme lorsqu''il est réconforté par une personne familière', 'Calms down when comforted by a familiar person', 1);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e6000002-0000-4a00-8000-000000000000', 'd6000000-0000-4a00-8000-000000000000', 4, 6, 'Toont duidelijk plezier (lachen, kirren)', 'Montre clairement du plaisir (rire, gazouiller)', 'Clearly shows enjoyment (laughing, cooing)', 2);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e6000003-0000-4a00-8000-000000000000', 'd6000000-0000-4a00-8000-000000000000', 7, 12, 'Vertoont vreemdenangst en hechting aan vertrouwde volwassene', 'Manifeste une peur de l''étranger et un attachement à un adulte familier', 'Shows stranger anxiety and attachment to a familiar adult', 3);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e6000004-0000-4a00-8000-000000000000', 'd6000000-0000-4a00-8000-000000000000', 13, 18, 'Zoekt actief nabijheid van een vertrouwde volwassene bij onrust', 'Recherche activement la proximité d''un adulte familier en cas de détresse', 'Actively seeks closeness to a familiar adult when distressed', 4);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e6000005-0000-4a00-8000-000000000000', 'd6000000-0000-4a00-8000-000000000000', 19, 24, 'Toont een breder gamma aan emoties (frustratie, trots, jaloezie)', 'Montre une palette plus large d''émotions (frustration, fierté, jalousie)', 'Shows a wider range of emotions (frustration, pride, jealousy)', 5);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e6000006-0000-4a00-8000-000000000000', 'd6000000-0000-4a00-8000-000000000000', 25, 36, 'Benoemt een eigen emotie met een woord (''boos'', ''blij'')', 'Nomme sa propre émotion avec un mot (« fâché », « content »)', 'Names their own emotion with a word (''angry'', ''happy'')', 6);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e7000001-0000-4a00-8000-000000000000', 'd7000000-0000-4a00-8000-000000000000', 0, 3, 'Toont hongersignalen vóór het huilen (bv. zuigbewegingen)', 'Montre des signaux de faim avant de pleurer (p.ex. mouvements de succion)', 'Shows hunger cues before crying (e.g. sucking motions)', 1);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e7000002-0000-4a00-8000-000000000000', 'd7000000-0000-4a00-8000-000000000000', 4, 6, 'Houdt zelf de zuigfles of borst vast tijdens het drinken', 'Tient lui-même le biberon ou le sein pendant qu''il boit', 'Holds the bottle or breast themselves while feeding', 2);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e7000003-0000-4a00-8000-000000000000', 'd7000000-0000-4a00-8000-000000000000', 7, 12, 'Eet zelfstandig vingervoedsel', 'Mange seul des aliments à manger avec les doigts', 'Feeds themselves finger foods independently', 3);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e7000004-0000-4a00-8000-000000000000', 'd7000000-0000-4a00-8000-000000000000', 13, 18, 'Probeert zelf met lepel te eten', 'Essaie de manger seul à la cuillère', 'Tries to eat with a spoon independently', 4);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e7000005-0000-4a00-8000-000000000000', 'd7000000-0000-4a00-8000-000000000000', 19, 24, 'Helpt actief mee bij het aan- en uitkleden (bv. arm door mouw steken)', 'Aide activement à s''habiller et se déshabiller (p.ex. passer le bras dans la manche)', 'Actively helps with dressing/undressing (e.g. pushing an arm through a sleeve)', 5);
    INSERT INTO developmental_milestones ("Id", "DomainId", "AgeFromMonths", "AgeToMonths", "DescriptionNl", "DescriptionFr", "DescriptionEn", "SortOrder")
    VALUES ('e7000006-0000-4a00-8000-000000000000', 'd7000000-0000-4a00-8000-000000000000', 25, 36, 'Wast en droogt zelfstandig de handen', 'Se lave et s''essuie les mains de façon autonome', 'Washes and dries their own hands independently', 6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260716185654_AddDevelopmentalMilestoneCatalog') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260716185654_AddDevelopmentalMilestoneCatalog', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719034203_AddDataProtectionKeys') THEN
    CREATE TABLE "DataProtectionKeys" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "FriendlyName" text,
        "Xml" text,
        CONSTRAINT "PK_DataProtectionKeys" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719034203_AddDataProtectionKeys') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260719034203_AddDataProtectionKeys', '10.0.10');
    END IF;
END $EF$;
COMMIT;

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

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723185118_AddPlatformAdminInvitationFields') THEN
    ALTER TABLE invitations ADD "CreatedByEmail" character varying(254);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723185118_AddPlatformAdminInvitationFields') THEN
    ALTER TABLE invitations ADD "CreatedByUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723185118_AddPlatformAdminInvitationFields') THEN
    ALTER TABLE invitations ADD "Locale" character varying(2) NOT NULL DEFAULT 'nl';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723185118_AddPlatformAdminInvitationFields') THEN
    ALTER TABLE invitations ADD "OrganisationNameNote" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723185118_AddPlatformAdminInvitationFields') THEN
    ALTER TABLE invitations ADD "RevokedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723185118_AddPlatformAdminInvitationFields') THEN
    ALTER TABLE invitations ADD "RevokedByEmail" character varying(254);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723185118_AddPlatformAdminInvitationFields') THEN
    ALTER TABLE invitations ADD "RevokedByUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723185118_AddPlatformAdminInvitationFields') THEN
    ALTER TABLE invitations ADD CONSTRAINT "CK_invitations_locale" CHECK ("Locale" IN ('nl','fr','en'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260723185118_AddPlatformAdminInvitationFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260723185118_AddPlatformAdminInvitationFields', '10.0.10');
    END IF;
END $EF$;
COMMIT;

