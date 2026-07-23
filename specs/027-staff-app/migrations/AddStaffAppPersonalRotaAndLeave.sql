START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    ALTER TABLE tenant_template.staff_schedules ADD "CoverStaffId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    ALTER TABLE tenant_template.staff_schedules ADD "CreatedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    ALTER TABLE tenant_template.staff_schedules ADD "IsPublished" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    ALTER TABLE tenant_template.staff_schedules ADD "Notes" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    ALTER TABLE tenant_template.staff_schedules ADD "PublishedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    ALTER TABLE tenant_template.staff_schedules ADD "Status" character varying(20) NOT NULL DEFAULT 'scheduled';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    UPDATE "tenant_template"."staff_schedules"
    SET "Status" = CASE WHEN "IsAbsent" THEN 'absent' ELSE 'scheduled' END;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    ALTER TABLE tenant_template.staff_schedules DROP COLUMN "IsAbsent";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    ALTER TABLE tenant_template.staff_profiles ADD "ContractedDays" text[] NOT NULL DEFAULT ('{}');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    ALTER TABLE tenant_template.staff_profiles ADD "PushToken" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    CREATE TABLE tenant_template.staff_leave_requests (
        "Id" uuid NOT NULL,
        "StaffProfileId" uuid NOT NULL,
        "Type" character varying(20) NOT NULL,
        "DateFrom" date NOT NULL,
        "DateTo" date NOT NULL,
        "Notes" character varying(2000),
        "Status" character varying(20) NOT NULL DEFAULT 'pending',
        "DecidedBy" uuid,
        "DecidedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_staff_leave_requests" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_staff_leave_requests_staff_profiles_StaffProfileId" FOREIGN KEY ("StaffProfileId") REFERENCES tenant_template.staff_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    CREATE INDEX "IX_staff_leave_requests_StaffProfileId_CreatedAt" ON tenant_template.staff_leave_requests ("StaffProfileId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    CREATE INDEX "IX_staff_leave_requests_Status" ON tenant_template.staff_leave_requests ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM tenant_template."__EFMigrationsHistory" WHERE "MigrationId" = '20260722185942_AddStaffAppPersonalRotaAndLeave') THEN
    INSERT INTO tenant_template."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260722185942_AddStaffAppPersonalRotaAndLeave', '10.0.10');
    END IF;
END $EF$;
COMMIT;

