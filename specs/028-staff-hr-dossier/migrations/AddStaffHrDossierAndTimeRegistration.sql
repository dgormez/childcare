START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260723084508_AddStaffHrDossierAndTimeRegistration') THEN
    ALTER TABLE tenant_template.staff_profiles ADD "TimeEntryFunctions" text[] NOT NULL DEFAULT ('{}');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260723084508_AddStaffHrDossierAndTimeRegistration') THEN
    CREATE TABLE tenant_template.staff_documents (
        "Id" uuid NOT NULL,
        "StaffProfileId" uuid NOT NULL,
        "DocumentType" character varying(30) NOT NULL,
        "Title" character varying(200) NOT NULL,
        "ObjectPath" character varying(500) NOT NULL,
        "ValidFrom" date,
        "ValidUntil" date,
        "CreatedBy" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "DeletedAt" timestamp with time zone,
        "DeletedBy" uuid,
        CONSTRAINT "PK_staff_documents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_staff_documents_staff_profiles_StaffProfileId" FOREIGN KEY ("StaffProfileId") REFERENCES tenant_template.staff_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260723084508_AddStaffHrDossierAndTimeRegistration') THEN
    CREATE TABLE tenant_template.staff_time_entries (
        "Id" uuid NOT NULL,
        "StaffProfileId" uuid NOT NULL,
        "LocationId" uuid NOT NULL,
        "GroupId" uuid,
        "ClockedInAt" timestamp with time zone NOT NULL,
        "ClockedOutAt" timestamp with time zone,
        "Function" character varying(30) NOT NULL,
        "Notes" text,
        "UnlockedAt" timestamp with time zone,
        "UnlockedBy" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_staff_time_entries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_staff_time_entries_groups_GroupId" FOREIGN KEY ("GroupId") REFERENCES tenant_template.groups ("Id"),
        CONSTRAINT "FK_staff_time_entries_locations_LocationId" FOREIGN KEY ("LocationId") REFERENCES tenant_template.locations ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_staff_time_entries_staff_profiles_StaffProfileId" FOREIGN KEY ("StaffProfileId") REFERENCES tenant_template.staff_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260723084508_AddStaffHrDossierAndTimeRegistration') THEN
    CREATE INDEX "IX_staff_documents_DocumentType_ValidUntil" ON tenant_template.staff_documents ("DocumentType", "ValidUntil");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260723084508_AddStaffHrDossierAndTimeRegistration') THEN
    CREATE INDEX "IX_staff_documents_StaffProfileId" ON tenant_template.staff_documents ("StaffProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260723084508_AddStaffHrDossierAndTimeRegistration') THEN
    CREATE INDEX "IX_staff_time_entries_GroupId" ON tenant_template.staff_time_entries ("GroupId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260723084508_AddStaffHrDossierAndTimeRegistration') THEN
    CREATE INDEX "IX_staff_time_entries_LocationId_ClockedInAt" ON tenant_template.staff_time_entries ("LocationId", "ClockedInAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260723084508_AddStaffHrDossierAndTimeRegistration') THEN
    CREATE INDEX "IX_staff_time_entries_StaffProfileId_ClockedOutAt" ON tenant_template.staff_time_entries ("StaffProfileId", "ClockedOutAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260723084508_AddStaffHrDossierAndTimeRegistration') THEN
    INSERT INTO tenant_template."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260723084508_AddStaffHrDossierAndTimeRegistration', '10.0.10');
    END IF;
END $EF$;
COMMIT;

