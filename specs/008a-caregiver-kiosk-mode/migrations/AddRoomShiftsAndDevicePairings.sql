START TRANSACTION;
ALTER TABLE tenant_template.staff_profiles ADD "PinFailedAttempts" integer NOT NULL DEFAULT 0;

ALTER TABLE tenant_template.staff_profiles ADD "PinFirstFailedAttemptAt" timestamp with time zone;

ALTER TABLE tenant_template.staff_profiles ADD "PinHash" character varying(100);

ALTER TABLE tenant_template.staff_profiles ADD "PinLockedUntil" timestamp with time zone;

CREATE TABLE tenant_template.device_pairings (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "LocationId" uuid NOT NULL,
    "GroupId" uuid NOT NULL,
    "DirectorOverridePinHash" text NOT NULL,
    "TokenIssuedAt" timestamp with time zone NOT NULL,
    "TokenVersion" integer NOT NULL,
    "RevokedAt" timestamp with time zone,
    "PairedByTenantUserId" uuid NOT NULL,
    "OverridePinFailedAttempts" integer NOT NULL,
    "OverridePinFirstFailedAttemptAt" timestamp with time zone,
    "OverridePinLockedUntil" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_device_pairings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_device_pairings_groups_GroupId" FOREIGN KEY ("GroupId") REFERENCES tenant_template.groups ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_device_pairings_locations_LocationId" FOREIGN KEY ("LocationId") REFERENCES tenant_template.locations ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_device_pairings_users_PairedByTenantUserId" FOREIGN KEY ("PairedByTenantUserId") REFERENCES tenant_template.users ("Id") ON DELETE CASCADE
);

CREATE TABLE tenant_template.room_shifts (
    "Id" uuid NOT NULL,
    "StaffProfileId" uuid NOT NULL,
    "LocationId" uuid NOT NULL,
    "GroupId" uuid NOT NULL,
    "DevicePairingId" uuid NOT NULL,
    "CheckedInAt" timestamp with time zone NOT NULL,
    "CheckedOutAt" timestamp with time zone,
    "ClosedReason" character varying(20),
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_room_shifts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_room_shifts_device_pairings_DevicePairingId" FOREIGN KEY ("DevicePairingId") REFERENCES tenant_template.device_pairings ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_room_shifts_groups_GroupId" FOREIGN KEY ("GroupId") REFERENCES tenant_template.groups ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_room_shifts_locations_LocationId" FOREIGN KEY ("LocationId") REFERENCES tenant_template.locations ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_room_shifts_staff_profiles_StaffProfileId" FOREIGN KEY ("StaffProfileId") REFERENCES tenant_template.staff_profiles ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_device_pairings_GroupId" ON tenant_template.device_pairings ("GroupId");

CREATE INDEX "IX_device_pairings_LocationId" ON tenant_template.device_pairings ("LocationId");

CREATE INDEX "IX_device_pairings_PairedByTenantUserId" ON tenant_template.device_pairings ("PairedByTenantUserId");

CREATE INDEX "IX_device_pairings_RevokedAt" ON tenant_template.device_pairings ("RevokedAt");

CREATE INDEX "IX_room_shifts_CheckedOutAt_LocationId_GroupId" ON tenant_template.room_shifts ("CheckedOutAt", "LocationId", "GroupId");

CREATE INDEX "IX_room_shifts_CheckedOutAt_StaffProfileId" ON tenant_template.room_shifts ("CheckedOutAt", "StaffProfileId");

CREATE INDEX "IX_room_shifts_DevicePairingId" ON tenant_template.room_shifts ("DevicePairingId");

CREATE INDEX "IX_room_shifts_GroupId" ON tenant_template.room_shifts ("GroupId");

CREATE INDEX "IX_room_shifts_LocationId" ON tenant_template.room_shifts ("LocationId");

CREATE INDEX "IX_room_shifts_StaffProfileId" ON tenant_template.room_shifts ("StaffProfileId");

INSERT INTO tenant_template."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260708073818_AddRoomShiftsAndDevicePairings', '10.0.8');

COMMIT;

