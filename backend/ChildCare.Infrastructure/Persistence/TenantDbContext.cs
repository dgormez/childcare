using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChildCare.Infrastructure.Persistence;

/// <summary>
/// Connects to a single tenant's schema. The schema name is fixed at construction time —
/// this context is used by TenantProvisioningService to create/migrate a brand-new tenant
/// schema (research.md R6), and by ITenantDbContextResolver/TenantMiddleware for per-request
/// resolution (feature 002). Implements ITenantDbContext so ChildCare.Application code can
/// depend on the port instead of this concrete Infrastructure type (research.md R1).
/// </summary>
public class TenantDbContext(DbContextOptions<TenantDbContext> options, string schemaName) : DbContext(options), ITenantDbContext
{
    // Matches TenantDbContextFactory/TenantProvisioningService's design-time placeholder — see
    // MigrateAsync's doc comment for why this has to be substituted at runtime.
    private const string PlaceholderSchema = "tenant_template";

    public string SchemaName { get; } = schemaName;

    public DbSet<TenantUser> Users => Set<TenantUser>();

    public DbSet<TenantUserRefreshToken> RefreshTokens => Set<TenantUserRefreshToken>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();

    public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();

    public DbSet<StaffLocationEligibility> StaffLocationEligibility => Set<StaffLocationEligibility>();

    public DbSet<Child> Children => Set<Child>();

    public DbSet<Contact> Contacts => Set<Contact>();

    public DbSet<ChildContact> ChildContacts => Set<ChildContact>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<ChildGroupAssignment> ChildGroupAssignments => Set<ChildGroupAssignment>();

    public DbSet<VaccineRecord> VaccineRecords => Set<VaccineRecord>();

    public DbSet<TenantCustomVaccineEntry> TenantCustomVaccineEntries => Set<TenantCustomVaccineEntry>();

    public DbSet<HealthRecord> HealthRecords => Set<HealthRecord>();

    public DbSet<Contract> Contracts => Set<Contract>();

    public DbSet<RoomShift> RoomShifts => Set<RoomShift>();

    public DbSet<DevicePairing> DevicePairings => Set<DevicePairing>();

    public DbSet<ChildEvent> ChildEvents => Set<ChildEvent>();

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    public DbSet<KdvClosureDay> KdvClosureDays => Set<KdvClosureDay>();

    public DbSet<ClosureNotificationDelivery> ClosureNotificationDeliveries => Set<ClosureNotificationDelivery>();

    public DbSet<ParentClosureMessage> ParentClosureMessages => Set<ParentClosureMessage>();

    public DbSet<StaffSchedule> StaffSchedules => Set<StaffSchedule>();

    public DbSet<WaitingListEntry> WaitingListEntries => Set<WaitingListEntry>();

    public DbSet<DayReservation> DayReservations => Set<DayReservation>();

    public DbSet<ParentInvitation> ParentInvitations => Set<ParentInvitation>();

    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();

    public DbSet<MessageThreadParticipant> MessageThreadParticipants => Set<MessageThreadParticipant>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<AnnouncementRecipient> AnnouncementRecipients => Set<AnnouncementRecipient>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<GroupActivity> GroupActivities => Set<GroupActivity>();

    public DbSet<GroupActivityPhoto> GroupActivityPhotos => Set<GroupActivityPhoto>();

    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();

    public DbSet<MealPreference> MealPreferences => Set<MealPreference>();

    public DbSet<MonthlyMenu> MonthlyMenus => Set<MonthlyMenu>();

    public DbSet<MonthlyMenuDay> MonthlyMenuDays => Set<MonthlyMenuDay>();

    public DbSet<MealPreferenceChangeRequest> MealPreferenceChangeRequests => Set<MealPreferenceChangeRequest>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Applies any pending migrations to this schema. Deliberately does NOT call the ordinary
    /// Database.MigrateAsync() — discovered during implementation (tasks.md T032/research.md
    /// R8) that it cannot work here: EF Core bakes the design-time placeholder schema
    /// ("tenant_template") into each generated migration's SQL as a literal string, exactly the
    /// constraint TenantProvisioningService's own baseline-script generation already documents
    /// and works around (research.md R6). This method generates the *pending* script (from
    /// whatever this schema's own __EFMigrationsHistory last recorded, to latest) against that
    /// same placeholder, substitutes this schema's real name in, and executes the result
    /// directly — the same technique, extended to cover incremental rollouts, not just a brand
    /// new tenant's baseline.
    /// </summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var applied = await Database.GetAppliedMigrationsAsync(cancellationToken);
        var fromMigration = applied.LastOrDefault();

        var sql = GeneratePendingSql(fromMigration).Replace(PlaceholderSchema, SchemaName);
        if (string.IsNullOrWhiteSpace(sql))
            return;

        if (sql.Contains(PlaceholderSchema, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Pending migration SQL for '{SchemaName}' still references the placeholder schema " +
                $"'{PlaceholderSchema}' after substitution — refusing to run it.");

        // ExecuteSqlRawAsync treats its `sql` argument as a composite format string internally
        // (RawSqlCommandBuilder.Build) regardless of whether any parameters are supplied — a
        // migration whose generated DDL contains a literal '{' or '}' (e.g. Postgres's '{}'
        // empty-array default literal, first hit by feature 013j's AddMonthlyMenuVariants
        // migration) throws a FormatException instead of running. Braces must be escaped by
        // doubling, the standard .NET composite-format-string escape, before executing — found
        // by TenantMigrationRolloutTests/LegacyVaccinationMigrationTests actually failing, not by
        // inspection. TenantProvisioningService's baseline-script path is unaffected: it runs its
        // SQL through a raw NpgsqlCommand directly, never through this EF Core helper.
        var escapedSql = sql.Replace("{", "{{").Replace("}", "}}");
        await Database.ExecuteSqlRawAsync(escapedSql, cancellationToken);
    }

    public async Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken = default)
        => (await Database.GetPendingMigrationsAsync(cancellationToken)).Any();

    private static string GeneratePendingSql(string? fromMigration)
    {
        // SQL generation never opens a connection, so this connection string only needs to be
        // syntactically valid for the Npgsql provider to configure itself — mirrors
        // TenantProvisioningService.GenerateBaselineSql().
        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=script-generation-only",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PlaceholderSchema));
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, DynamicSchemaModelCacheKeyFactory>();

        using var context = new TenantDbContext(optionsBuilder.Options, PlaceholderSchema);
        var migrator = context.GetService<IMigrator>();
        return migrator.GenerateScript(fromMigration: fromMigration, options: MigrationsSqlGenerationOptions.Default);
    }

    // Extracted to a plain method — HasConversion's expression-tree lambda can't contain an
    // out-var declaration or a throw-expression directly (CS8198/CS8188).
    private static ChildEventType ParseChildEventType(string value) =>
        ChildEventTypeExtensions.TryParseWireString(value, out var parsed)
            ? parsed
            : throw new FormatException($"Unknown ChildEventType: {value}");

    private static GroupActivityType ParseGroupActivityType(string value) =>
        GroupActivityTypeExtensions.TryParseWireString(value, out var parsed)
            ? parsed
            : throw new FormatException($"Unknown GroupActivityType: {value}");

    private static IncidentInjuryType ParseIncidentInjuryType(string value) =>
        IncidentInjuryTypeExtensions.TryParseWireString(value, out var parsed)
            ? parsed
            : throw new FormatException($"Unknown IncidentInjuryType: {value}");

    private static ParentNotifiedHow ParseParentNotifiedHow(string value) =>
        ParentNotifiedHowExtensions.TryParseWireString(value, out var parsed)
            ? parsed
            : throw new FormatException($"Unknown ParentNotifiedHow: {value}");

    private static HealthRecordType ParseHealthRecordType(string value) =>
        HealthRecordTypeExtensions.TryParseWireString(value, out var parsed)
            ? parsed
            : throw new FormatException($"Unknown HealthRecordType: {value}");

    private static DietaryType ParseDietaryType(string value) =>
        DietaryTypeExtensions.TryParseWireString(value, out var parsed)
            ? parsed
            : throw new FormatException($"Unknown DietaryType: {value}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<TenantUser>(u =>
        {
            u.ToTable("users", tb =>
            {
                tb.HasCheckConstraint("CK_users_role", "\"Role\" IN ('director','staff','parent')");
            });
            u.HasKey(x => x.Id);
            u.HasIndex(x => x.Email).IsUnique();
            u.Property(x => x.Email).IsRequired().HasMaxLength(254);
            u.Property(x => x.PasswordHash).IsRequired();
            u.Property(x => x.Name).IsRequired().HasMaxLength(200);
            u.Property(x => x.Role)
             .HasConversion(
                 v => v.ToString().ToLowerInvariant(),
                 v => (UserRole)Enum.Parse(typeof(UserRole), v, ignoreCase: true))
             .HasMaxLength(20)
             .IsRequired();
            // Feature 013h — IsPlatformAdmin, default false, set only via grant-platform-admin.
            u.Property(x => x.IsPlatformAdmin).IsRequired().HasDefaultValue(false);
        });

        modelBuilder.Entity<TenantUserRefreshToken>(t =>
        {
            t.ToTable("refresh_tokens");
            t.HasKey(x => x.Id);
            t.HasIndex(x => x.Token).IsUnique();
            t.Property(x => x.Token).IsRequired().HasMaxLength(128);
            t.Property(x => x.ExpiresAt).IsRequired();
            t.HasOne<TenantUser>()
             .WithMany()
             .HasForeignKey(x => x.TenantUserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Location>(l =>
        {
            l.ToTable("locations", tb =>
            {
                tb.HasCheckConstraint("CK_locations_max_capacity", "\"MaxCapacity\" > 0");
            });
            l.HasKey(x => x.Id);
            l.Property(x => x.Name).IsRequired().HasMaxLength(200);
            l.Property(x => x.Address).IsRequired().HasMaxLength(500);
            l.Property(x => x.Phone).IsRequired().HasMaxLength(30);
            l.Property(x => x.Email).IsRequired().HasMaxLength(254);
            l.Property(x => x.NaamLocatie).HasMaxLength(200);
            l.Property(x => x.Dossiernummer).HasMaxLength(50);
            l.Property(x => x.Verantwoordelijke).HasMaxLength(200);
            l.Property(x => x.FlexPermission).IsRequired();
            l.Property(x => x.BoPermission).IsRequired();
            l.Property(x => x.ReservationAbsencesMode)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (ReservationRequestMode)Enum.Parse(typeof(ReservationRequestMode), v, ignoreCase: true))
              .HasMaxLength(20)
              .HasDefaultValue(ReservationRequestMode.Approval)
              .IsRequired();
            l.Property(x => x.ReservationExtrasMode)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (ReservationRequestMode)Enum.Parse(typeof(ReservationRequestMode), v, ignoreCase: true))
              .HasMaxLength(20)
              .HasDefaultValue(ReservationRequestMode.Approval)
              .IsRequired();
            l.Property(x => x.ReservationSwapsMode)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (ReservationRequestMode)Enum.Parse(typeof(ReservationRequestMode), v, ignoreCase: true))
              .HasMaxLength(20)
              .HasDefaultValue(ReservationRequestMode.Disabled)
              .IsRequired();
            l.Property(x => x.ReservationNoticeHours).HasDefaultValue(0).IsRequired();
            // Feature 013j — plain text[] of wire strings, no enum HasConversion (unlike
            // MealPreference.DietaryType above) — see Location.cs's field comment for why: a
            // second List<DietaryType>-shaped converter here collides with MonthlyMenu.Variant's
            // DietaryType? converter in a Npgsql provider bug. Same simple pattern
            // MealPreferenceChangeRequest.NewDietaryType already uses below.
            l.Property(x => x.MenuVariantPriorityOrder)
              .HasColumnType("text[]")
              .HasDefaultValueSql("'{}'");
            // Feature 014 — per-location invoicing details (spec.md Clarifications).
            l.Property(x => x.Erkenningsnummer).HasMaxLength(50);
            l.Property(x => x.BankAccountNumber).HasMaxLength(50);
            l.Property(x => x.InvoiceDueDays).HasDefaultValue(14).IsRequired();
            l.HasIndex(x => x.DeactivatedAt);
        });

        modelBuilder.Entity<StaffProfile>(s =>
        {
            s.ToTable("staff_profiles");
            s.HasKey(x => x.Id);
            s.HasIndex(x => x.TenantUserId).IsUnique();
            s.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
            s.Property(x => x.LastName).IsRequired().HasMaxLength(100);
            s.Property(x => x.Phone).IsRequired().HasMaxLength(30);
            s.Property(x => x.QualificationLevel)
             .HasConversion(
                 v => v == null ? null : v.ToString(),
                 v => v == null ? null : (QualificationLevel?)Enum.Parse(typeof(QualificationLevel), v, ignoreCase: true))
             .HasMaxLength(30);
            s.Property(x => x.ProfilePhotoObjectPath).HasMaxLength(500);
            s.Property(x => x.PinHash).HasMaxLength(100);
            s.HasIndex(x => x.DeactivatedAt);
            s.HasOne<TenantUser>().WithOne().HasForeignKey<StaffProfile>(x => x.TenantUserId);
        });

        modelBuilder.Entity<StaffInvitation>(i =>
        {
            i.ToTable("staff_invitations");
            i.HasKey(x => x.Id);
            i.Property(x => x.Email).IsRequired().HasMaxLength(254);
            i.HasIndex(x => x.Email);
            i.HasOne<StaffProfile>().WithMany().HasForeignKey(x => x.StaffProfileId);
        });

        modelBuilder.Entity<StaffLocationEligibility>(e =>
        {
            e.ToTable("staff_location_eligibility");
            e.HasKey(x => new { x.StaffProfileId, x.LocationId });
            e.HasOne<StaffProfile>().WithMany().HasForeignKey(x => x.StaffProfileId);
            e.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<Child>(c =>
        {
            c.ToTable("children");
            c.HasKey(x => x.Id);
            c.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
            c.Property(x => x.LastName).IsRequired().HasMaxLength(100);
            c.Property(x => x.DateOfBirth).IsRequired();
            c.Property(x => x.ProfilePhotoObjectPath).HasMaxLength(500);
            c.Property(x => x.Gender)
             .HasConversion(
                 v => v == null ? null : v.ToString(),
                 v => v == null ? null : (Gender?)Enum.Parse(typeof(Gender), v, ignoreCase: true))
             .HasMaxLength(20);
            c.Property(x => x.Nationality).HasMaxLength(100);
            c.Property(x => x.AllergiesDescription).HasMaxLength(2000);
            c.Property(x => x.AllergySeverity)
             .HasConversion(
                 v => v == null ? null : v.ToString(),
                 v => v == null ? null : (AllergySeverity?)Enum.Parse(typeof(AllergySeverity), v, ignoreCase: true))
             .HasMaxLength(20);
            c.Property(x => x.MedicalConditions).HasMaxLength(2000);
            c.Property(x => x.DietaryRestrictions).HasMaxLength(2000);
            c.Property(x => x.GpName).HasMaxLength(200);
            c.Property(x => x.GpPhone).HasMaxLength(30);
            c.Property(x => x.PediatricianName).HasMaxLength(200);
            c.Property(x => x.PediatricianPhone).HasMaxLength(30);
            c.Property(x => x.HealthInsuranceNumber).HasMaxLength(50);
            c.Property(x => x.Kindcode).HasMaxLength(20);
            c.HasIndex(x => x.DeactivatedAt);
        });

        modelBuilder.Entity<Contact>(c =>
        {
            c.ToTable("contacts");
            c.HasKey(x => x.Id);
            c.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
            c.Property(x => x.LastName).IsRequired().HasMaxLength(100);
            c.Property(x => x.Phone).IsRequired().HasMaxLength(30);
            c.Property(x => x.Email).HasMaxLength(254);
            c.Property(x => x.Locale).IsRequired().HasMaxLength(5);
            c.Property(x => x.PushToken).HasMaxLength(200);
            c.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.TenantUserId);
            // Postgres treats NULLs as distinct in a unique index, so this allows any number of
            // not-yet-invited contacts (TenantUserId = null) while still enforcing at most one
            // Contact per parent account (feature 013, research.md R1).
            c.HasIndex(x => x.TenantUserId).IsUnique();
        });

        modelBuilder.Entity<ChildContact>(cc =>
        {
            cc.ToTable("child_contacts");
            cc.HasKey(x => new { x.ChildId, x.ContactId });
            cc.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            cc.HasOne<Contact>().WithMany().HasForeignKey(x => x.ContactId);
        });

        modelBuilder.Entity<Group>(g =>
        {
            g.ToTable("groups");
            g.HasKey(x => x.Id);
            g.Property(x => x.Name).IsRequired().HasMaxLength(100);
            g.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
        });

        modelBuilder.Entity<ChildGroupAssignment>(a =>
        {
            a.ToTable("child_group_assignments");
            a.HasKey(x => x.Id);
            a.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            a.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
            a.HasIndex(x => new { x.ChildId, x.EndDate });
        });

        modelBuilder.Entity<VaccineRecord>(v =>
        {
            v.ToTable("vaccine_records", t =>
            {
                // Feature 013g, spec.md FR-004 — DB-enforced, not application-layer-only
                // (checklist finding D1).
                t.HasCheckConstraint("CK_vaccine_records_vaccine_reference_exclusive",
                    "\"VaccineTypeId\" IS NULL OR \"CustomVaccineEntryId\" IS NULL");
            });
            v.HasKey(x => x.Id);
            v.Property(x => x.VaccineName).IsRequired().HasMaxLength(200);
            v.Property(x => x.AdministeredBy).HasMaxLength(200);
            v.Property(x => x.Notes).HasMaxLength(2000);
            v.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            v.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.RecordedBy);
            // Feature 013g — no DB FK on VaccineTypeId (research.md R2: VaccineType lives in the
            // public schema and is soft-delete-only, so a cross-schema FK guards against a
            // failure mode that can't occur). CustomVaccineEntryId is a real, same-schema FK.
            v.HasIndex(x => x.VaccineTypeId);
            v.HasOne<TenantCustomVaccineEntry>().WithMany().HasForeignKey(x => x.CustomVaccineEntryId);
            v.HasIndex(x => x.ChildId);
            // Partial index (research.md R4) — supports the due-soon dashboard aggregate without
            // a full-table scan; excludes soft-deleted rows since they never contribute.
            v.HasIndex(x => x.NextDueDate).HasFilter("\"DeletedAt\" IS NULL");
        });

        modelBuilder.Entity<TenantCustomVaccineEntry>(c =>
        {
            c.ToTable("tenant_custom_vaccine_entries");
            c.HasKey(x => x.Id);
            c.Property(x => x.Name).IsRequired().HasMaxLength(200);
            c.Property(x => x.NormalizedName).IsRequired().HasMaxLength(200);
            // Case/whitespace/diacritic-insensitive dedupe (research.md R3, spec.md FR-007) —
            // enforced at the DB level so a concurrent-write race can't create a near-duplicate.
            c.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<HealthRecord>(hr =>
        {
            hr.ToTable("health_records");
            hr.HasKey(x => x.Id);
            hr.Property(x => x.RecordType)
              .HasConversion(v => v.ToWireString(), v => ParseHealthRecordType(v))
              .HasMaxLength(30)
              .IsRequired();
            hr.Property(x => x.Title).IsRequired().HasMaxLength(200);
            hr.Property(x => x.Description).IsRequired().HasMaxLength(2000);
            hr.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            hr.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.RecordedBy);
            hr.HasIndex(x => x.ChildId);
        });

        modelBuilder.Entity<Contract>(c =>
        {
            c.ToTable("contracts", tb =>
            {
                tb.HasCheckConstraint("CK_contracts_daily_rate_cents", "\"DailyRateCents\" > 0");
            });
            c.HasKey(x => x.Id);
            c.Property(x => x.DailyRateCents).IsRequired();
            c.Property(x => x.Status)
             .HasConversion(
                 v => v.ToString().ToLowerInvariant(),
                 v => (ContractStatus)Enum.Parse(typeof(ContractStatus), v, ignoreCase: true))
             .HasMaxLength(20)
             .IsRequired();
            c.Property(x => x.TariefCode).HasMaxLength(50);
            c.OwnsMany(x => x.ContractedDays, cd => cd.ToJson());
            c.OwnsOne(x => x.Consent, cs => cs.ToJson());
            c.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            c.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            c.HasOne<Contract>().WithMany()
             .HasForeignKey(x => x.PreviousContractId)
             .OnDelete(DeleteBehavior.Restrict);
            c.HasIndex(x => new { x.ChildId, x.Status });
            c.HasIndex(x => new { x.LocationId, x.Status });
        });

        modelBuilder.Entity<RoomShift>(rs =>
        {
            rs.ToTable("room_shifts");
            rs.HasKey(x => x.Id);
            rs.Property(x => x.ClosedReason).HasMaxLength(20);
            rs.HasOne<StaffProfile>().WithMany().HasForeignKey(x => x.StaffProfileId);
            rs.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            rs.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
            rs.HasOne<DevicePairing>().WithMany().HasForeignKey(x => x.DevicePairingId);
            // Powers "find this caregiver's open shift" (check-in/out) and "find every open
            // shift at this location/group" (roster, IShiftAttributionService) — both filter
            // on CheckedOutAt == null, so it leads the composite index (data-model.md).
            rs.HasIndex(x => new { x.CheckedOutAt, x.StaffProfileId });
            rs.HasIndex(x => new { x.CheckedOutAt, x.LocationId, x.GroupId });
        });

        modelBuilder.Entity<DevicePairing>(dp =>
        {
            dp.ToTable("device_pairings");
            dp.HasKey(x => x.Id);
            dp.Property(x => x.DirectorOverridePinHash).IsRequired();
            dp.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            dp.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
            dp.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.PairedByTenantUserId);
            dp.HasIndex(x => x.RevokedAt);
        });

        modelBuilder.Entity<ChildEvent>(ce =>
        {
            ce.ToTable("child_events");
            ce.HasKey(x => x.Id);
            ce.Property(x => x.EventType)
              .HasConversion(v => v.ToWireString(), v => ParseChildEventType(v))
              .HasMaxLength(30)
              .IsRequired();
            ce.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            // Native Npgsql array mapping (uuid[]) — simpler than a jsonb array for a flat list
            // of ids (research.md R1's jsonb rationale is about the polymorphic Payload, not
            // this field).
            ce.Property(x => x.RecordedBy).HasColumnType("uuid[]");
            ce.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            ce.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            ce.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
            ce.HasOne<DevicePairing>().WithMany().HasForeignKey(x => x.RecordedByDeviceId);
            // Primary timeline access pattern (data-model.md).
            ce.HasIndex(x => new { x.ChildId, x.OccurredAt });
            ce.HasIndex(x => new { x.ChildId, x.EventType, x.OccurredAt });
        });

        modelBuilder.Entity<IncidentReport>(ir =>
        {
            ir.ToTable("incident_reports");
            ir.HasKey(x => x.Id);
            ir.Property(x => x.Description).IsRequired();
            ir.Property(x => x.InjuryType)
              .HasConversion(v => v.ToWireString(), v => ParseIncidentInjuryType(v))
              .HasMaxLength(30)
              .IsRequired();
            ir.Property(x => x.ParentNotifiedHow)
              .HasConversion(
                  v => v == null ? null : v.Value.ToWireString(),
                  v => v == null ? null : ParseParentNotifiedHow(v))
              .HasMaxLength(20);
            // Native Npgsql array mapping (uuid[]) — same pattern as ChildEvent.RecordedBy
            // (research.md R1, data-model.md).
            ir.Property(x => x.ReportedBy).HasColumnType("uuid[]");
            ir.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            ir.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            // FR-017: the cross-KDV inspection view's filter/index dimensions.
            ir.HasIndex(x => new { x.LocationId, x.OccurredAt });
            ir.HasIndex(x => new { x.ChildId, x.OccurredAt });
        });

        modelBuilder.Entity<GroupActivity>(ga =>
        {
            ga.ToTable("group_activities");
            ga.HasKey(x => x.Id);
            ga.Property(x => x.ActivityType)
              .HasConversion(
                  v => v.ToWireString(),
                  v => ParseGroupActivityType(v))
              .HasMaxLength(20)
              .IsRequired();
            ga.Property(x => x.Title).HasMaxLength(200).IsRequired();
            ga.Property(x => x.Description).HasMaxLength(2000);
            // Native Npgsql array mapping (uuid[]) — same pattern as ChildEvent.RecordedBy
            // (research.md R1).
            ga.Property(x => x.RecordedBy).HasColumnType("uuid[]");
            ga.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
            ga.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            ga.HasOne<DevicePairing>().WithMany().HasForeignKey(x => x.RecordedByDeviceId);
            // Group timeline access pattern (research.md R4).
            ga.HasIndex(x => new { x.GroupId, x.OccurredAt });
        });

        modelBuilder.Entity<GroupActivityPhoto>(gap =>
        {
            gap.ToTable("group_activity_photos");
            gap.HasKey(x => x.Id);
            gap.Property(x => x.ObjectPath).IsRequired();
            gap.Property(x => x.ThumbnailObjectPath).IsRequired();
            gap.Property(x => x.Caption).HasMaxLength(500);
            gap.HasOne<GroupActivity>().WithMany().HasForeignKey(x => x.GroupActivityId).OnDelete(DeleteBehavior.Cascade);
            gap.HasIndex(x => x.GroupActivityId);
        });

        modelBuilder.Entity<AttendanceRecord>(ar =>
        {
            ar.ToTable("attendance_records");
            ar.HasKey(x => x.Id);
            ar.Property(x => x.Status)
              .HasConversion(
                  v => v.ToString().ToLowerInvariant(),
                  v => (AttendanceStatus)Enum.Parse(typeof(AttendanceStatus), v, ignoreCase: true))
              .HasMaxLength(20)
              .IsRequired();
            // Native Npgsql array mapping (uuid[]) — same pattern as ChildEvent.RecordedBy
            // (research.md R1).
            ar.Property(x => x.RecordedBy).HasColumnType("uuid[]");
            ar.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            ar.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            // FR-003: at most one record per child per location per day — the source of the
            // 409-on-duplicate conflict behavior (research.md R4).
            ar.HasIndex(x => new { x.ChildId, x.LocationId, x.Date }).IsUnique();
            // BKR present-count query's access pattern (research.md R2).
            ar.HasIndex(x => new { x.LocationId, x.Date, x.Status });
            ar.Property(x => x.PriorStateJson).HasColumnType("jsonb");
            ar.HasOne<KdvClosureDay>().WithMany().HasForeignKey(x => x.ClosureDayId);
        });

        modelBuilder.Entity<KdvClosureDay>(cd =>
        {
            cd.ToTable("kdv_closure_days");
            cd.HasKey(x => x.Id);
            cd.Property(x => x.Label).IsRequired().HasMaxLength(200);
            cd.Property(x => x.ClosureType)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (ClosureType)Enum.Parse(typeof(ClosureType), v, ignoreCase: true))
              .HasMaxLength(30)
              .IsRequired();
            cd.Property(x => x.Status)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (ClosureStatus)Enum.Parse(typeof(ClosureStatus), v, ignoreCase: true))
              .HasMaxLength(30)
              .IsRequired();
            cd.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            cd.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            cd.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
            cd.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
            cd.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict);
            cd.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.AttendanceGeneratedBy).OnDelete(DeleteBehavior.Restrict);
            cd.HasIndex(x => new { x.LocationId, x.Date }).IsUnique();
            cd.HasIndex(x => new { x.LocationId, x.Status, x.Date });
        });

        modelBuilder.Entity<ClosureNotificationDelivery>(d =>
        {
            d.ToTable("closure_notification_deliveries");
            d.HasKey(x => x.Id);
            d.Property(x => x.Kind)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (ClosureNotificationKind)Enum.Parse(typeof(ClosureNotificationKind), v, ignoreCase: true))
              .HasMaxLength(30)
              .IsRequired();
            d.Property(x => x.PushStatus)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (ClosureDeliveryStatus)Enum.Parse(typeof(ClosureDeliveryStatus), v, ignoreCase: true))
              .HasMaxLength(30)
              .IsRequired();
            d.Property(x => x.PushToken).HasMaxLength(200);
            d.Property(x => x.Error).HasMaxLength(500);
            d.HasOne<KdvClosureDay>().WithMany().HasForeignKey(x => x.ClosureDayId);
            d.HasOne<Contact>().WithMany().HasForeignKey(x => x.ContactId);
            d.HasOne<ParentClosureMessage>().WithMany().HasForeignKey(x => x.MessageId);
            d.HasIndex(x => new { x.ClosureDayId, x.ContactId, x.Kind }).IsUnique();
        });

        modelBuilder.Entity<ParentClosureMessage>(m =>
        {
            m.ToTable("parent_closure_messages");
            m.HasKey(x => x.Id);
            m.Property(x => x.Kind)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (ClosureNotificationKind)Enum.Parse(typeof(ClosureNotificationKind), v, ignoreCase: true))
              .HasMaxLength(30)
              .IsRequired();
            m.Property(x => x.TitleKey).IsRequired().HasMaxLength(120);
            m.Property(x => x.BodyKey).IsRequired().HasMaxLength(120);
            m.Property(x => x.ArgumentsJson).HasColumnType("jsonb").IsRequired();
            m.HasOne<Contact>().WithMany().HasForeignKey(x => x.ContactId);
            m.HasOne<KdvClosureDay>().WithMany().HasForeignKey(x => x.ClosureDayId);
            m.HasIndex(x => new { x.ClosureDayId, x.ContactId, x.Kind }).IsUnique();
        });

        modelBuilder.Entity<StaffSchedule>(ss =>
        {
            ss.ToTable("staff_schedules");
            ss.HasKey(x => x.Id);
            ss.Property(x => x.AbsenceReason)
              .HasConversion(
                  v => v == null ? null : v.ToString()!.ToLowerInvariant(),
                  v => v == null ? null : (AbsenceReason?)Enum.Parse(typeof(AbsenceReason), v, ignoreCase: true))
              .HasMaxLength(20);
            ss.HasOne<StaffProfile>().WithMany().HasForeignKey(x => x.StaffProfileId);
            ss.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            ss.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
            // BACKLOG.md's original UNIQUE(staff_id, date, start_time) — prevents exact-duplicate
            // entries; range-overlap is a validator concern (IAdvisoryLockService), not
            // expressible as a unique index (data-model.md).
            ss.HasIndex(x => new { x.StaffProfileId, x.Date, x.StartTime }).IsUnique();
            // Rota-builder week view and feature 010-adjacent projected on-duty lookups
            // (data-model.md, spec.md Technical Requirements).
            ss.HasIndex(x => new { x.LocationId, x.Date });
        });

        modelBuilder.Entity<WaitingListEntry>(w =>
        {
            w.ToTable("waiting_list_entries");
            w.HasKey(x => x.Id);
            w.Property(x => x.ChildFirstName).IsRequired().HasMaxLength(200);
            w.Property(x => x.ChildLastName).IsRequired().HasMaxLength(200);
            w.Property(x => x.ContactName).IsRequired().HasMaxLength(200);
            w.Property(x => x.ContactEmail).HasMaxLength(320);
            w.Property(x => x.ContactPhone).HasMaxLength(50);
            w.Property(x => x.Notes).HasMaxLength(2000);
            w.Property(x => x.Status)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (WaitingListStatus)Enum.Parse(typeof(WaitingListStatus), v, ignoreCase: true))
              .HasMaxLength(20)
              .IsRequired();
            w.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            w.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            // List/sort/filter query per location, filtered by status, ordered by priority
            // (data-model.md, plan.md Performance considerations).
            w.HasIndex(x => new { x.LocationId, x.Status, x.Priority });
        });

        modelBuilder.Entity<DayReservation>(dr =>
        {
            dr.ToTable("day_reservations");
            dr.HasKey(x => x.Id);
            dr.Property(x => x.Type)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (DayReservationType)Enum.Parse(typeof(DayReservationType), v, ignoreCase: true))
              .HasMaxLength(20)
              .IsRequired();
            dr.Property(x => x.Status)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (DayReservationStatus)Enum.Parse(typeof(DayReservationStatus), v, ignoreCase: true))
              .HasMaxLength(20)
              .IsRequired();
            dr.Property(x => x.Reason).HasMaxLength(2000);
            dr.Property(x => x.DirectorNotes).HasMaxLength(2000);
            dr.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            dr.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
            dr.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
            // Director queue's primary read pattern: newest-first, filtered to pending (FR-006).
            dr.HasIndex(x => new { x.Status, x.CreatedAt });
            // Parent's own-request-history read pattern (FR-019).
            dr.HasIndex(x => new { x.ChildId, x.CreatedAt });
        });

        modelBuilder.Entity<ParentInvitation>(pi =>
        {
            pi.ToTable("parent_invitations");
            pi.HasKey(x => x.Id);
            pi.Property(x => x.Email).IsRequired().HasMaxLength(254);
            pi.Property(x => x.TokenHash).IsRequired();
            pi.HasOne<Contact>().WithMany().HasForeignKey(x => x.ContactId);
            pi.HasIndex(x => x.TokenHash).IsUnique();
        });

        modelBuilder.Entity<MessageThread>(mt =>
        {
            mt.ToTable("message_threads");
            mt.HasKey(x => x.Id);
            mt.Property(x => x.Subject).IsRequired().HasMaxLength(200);
            mt.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            // Director/staff "most recently active first" list ordering (spec.md User Story 2).
            mt.HasIndex(x => x.LastActivityAt);
        });

        modelBuilder.Entity<MessageThreadParticipant>(mtp =>
        {
            mtp.ToTable("message_thread_participants");
            mtp.HasKey(x => new { x.ThreadId, x.TenantUserId });
            mtp.HasOne<MessageThread>().WithMany().HasForeignKey(x => x.ThreadId);
            mtp.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.TenantUserId);
            // Parent thread-list lookup: "every thread this TenantUserId participates in."
            mtp.HasIndex(x => x.TenantUserId);
        });

        modelBuilder.Entity<Message>(m =>
        {
            m.ToTable("messages");
            m.HasKey(x => x.Id);
            m.Property(x => x.Body).IsRequired().HasMaxLength(5000);
            m.HasOne<MessageThread>().WithMany().HasForeignKey(x => x.ThreadId);
            m.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.SenderId);
            m.HasIndex(x => new { x.ThreadId, x.SentAt });
        });

        modelBuilder.Entity<Announcement>(a =>
        {
            a.ToTable("announcements");
            a.HasKey(x => x.Id);
            a.Property(x => x.Subject).IsRequired().HasMaxLength(200);
            a.Property(x => x.Body).IsRequired().HasMaxLength(5000);
            a.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            a.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
            a.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.SentByTenantUserId);
            a.HasIndex(x => x.SentAt);
        });

        modelBuilder.Entity<AnnouncementRecipient>(ar =>
        {
            ar.ToTable("announcement_recipients");
            ar.HasKey(x => x.Id);
            ar.HasOne<Announcement>().WithMany().HasForeignKey(x => x.AnnouncementId);
            ar.HasOne<Contact>().WithMany().HasForeignKey(x => x.ContactId);
            // A contact appears at most once per announcement; also the parent-facing
            // "am I a recipient of this announcement" authorization lookup.
            ar.HasIndex(x => new { x.AnnouncementId, x.ContactId }).IsUnique();
        });

        modelBuilder.Entity<Notification>(n =>
        {
            n.ToTable("notifications");
            n.HasKey(x => x.Id);
            n.Property(x => x.Type)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (NotificationType)Enum.Parse(typeof(NotificationType), v, ignoreCase: true))
              .HasMaxLength(30)
              .IsRequired();
            n.Property(x => x.TitleKey).IsRequired().HasMaxLength(200);
            n.Property(x => x.BodyKey).IsRequired().HasMaxLength(200);
            n.Property(x => x.ArgumentsJson).IsRequired().HasColumnType("jsonb");
            n.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.TenantUserId);
            // SourceId is intentionally NOT an FK (polymorphic across three tables — data-model.md).
            // Notification-centre list query: most-recent-first per recipient.
            n.HasIndex(x => new { x.TenantUserId, x.CreatedAt });
        });

        modelBuilder.Entity<MealPreference>(mp =>
        {
            mp.ToTable("child_meal_preferences");
            mp.HasKey(x => x.Id);
            mp.Property(x => x.Texture)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (MealTexture)Enum.Parse(typeof(MealTexture), v, ignoreCase: true))
              .HasMaxLength(20)
              .IsRequired();
            mp.Property(x => x.PortionSize)
              .HasConversion(v => v.ToString().ToLowerInvariant(), v => (MealPortionSize)Enum.Parse(typeof(MealPortionSize), v, ignoreCase: true))
              .HasMaxLength(20)
              .IsRequired();
            // Native text[] column — DietaryType has no sub-fields (unlike Contract.ContractedDays'
            // owned-type JSON), so a converted flat array is simpler than an owned collection
            // (data-model.md). A ValueComparer is required for EF Core to detect in-place
            // mutations of the List<DietaryType> reference correctly.
            mp.Property(x => x.DietaryType)
              .HasConversion(
                  v => v.Select(d => d.ToWireString()).ToArray(),
                  v => v.Select(ParseDietaryType).ToList(),
                  new ValueComparer<List<DietaryType>>(
                      (a, b) => a!.SequenceEqual(b!),
                      v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                      v => v.ToList()))
              .HasColumnType("text[]");
            mp.Property(x => x.AdditionalNotes).HasMaxLength(2000);
            mp.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            mp.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.UpdatedBy);
            mp.HasIndex(x => x.ChildId).IsUnique();
        });

        modelBuilder.Entity<MonthlyMenu>(mm =>
        {
            mm.ToTable("monthly_menus", t => t.HasCheckConstraint("ck_monthly_menus_month", "\"Month\" BETWEEN 1 AND 12"));
            mm.HasKey(x => x.Id);
            mm.Property(x => x.Month).IsRequired();
            mm.Property(x => x.Year).IsRequired();
            mm.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
            mm.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.CreatedBy);
            // Feature 013j — plain string, "base" sentinel default (matches
            // MonthlyMenuVariantHelper.BaseSentinel and the AddMonthlyMenuVariants migration's
            // hand-corrected AddColumn default — all three must stay in sync). Not a nullable
            // column (Postgres unique indexes treat NULL as distinct from every other NULL,
            // which would silently allow more than one base-menu row per location/year/month)
            // and not a DietaryType? HasConversion (collides with MealPreference.DietaryType's
            // List<DietaryType> converter in a Npgsql provider bug) — see MonthlyMenu.cs's field
            // comment and research.md.
            mm.Property(x => x.Variant)
              .HasColumnType("text")
              .HasDefaultValue("base")
              .IsRequired();
            // FR-005: at most one menu per location/year/month/variant — editing updates this row.
            mm.HasIndex(x => new { x.LocationId, x.Year, x.Month, x.Variant }).IsUnique();
            mm.HasMany(x => x.Days).WithOne().HasForeignKey(d => d.MenuId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonthlyMenuDay>(md =>
        {
            md.ToTable("monthly_menu_days");
            md.HasKey(x => x.Id);
            md.Property(x => x.Soup).HasMaxLength(500);
            md.Property(x => x.MainCourse).HasMaxLength(500);
            md.Property(x => x.Dessert).HasMaxLength(500);
            md.Property(x => x.Notes).HasMaxLength(500);
            md.HasIndex(x => new { x.MenuId, x.MenuDate }).IsUnique();
        });

        modelBuilder.Entity<MealPreferenceChangeRequest>(mpcr =>
        {
            mpcr.ToTable("meal_preference_change_requests");
            mpcr.HasKey(x => x.Id);
            mpcr.Property(x => x.NewTexture).HasMaxLength(20);
            // Stored wire strings (013d convention) — a plain nullable text[] column, no enum
            // converter (the wire form is persisted verbatim). A ValueComparer lets EF detect
            // in-place mutations of the List<string> reference correctly.
            mpcr.Property(x => x.NewDietaryType)
              .HasColumnType("text[]");
            mpcr.Property(x => x.Notes).HasMaxLength(2000);
            mpcr.Property(x => x.DecisionNotes).HasMaxLength(2000);
            mpcr.Property(x => x.Status)
              .HasConversion(
                  v => v.ToString().ToLowerInvariant(),
                  v => (MealPreferenceChangeRequestStatus)Enum.Parse(typeof(MealPreferenceChangeRequestStatus), v, ignoreCase: true))
              .HasMaxLength(20)
              .IsRequired();
            // No cascade — a request survives independently of later child-record edits (same as
            // DayReservation). FK to children only; RequestedBy/DecidedBy are TenantUserIds.
            mpcr.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            mpcr.HasOne<TenantUser>().WithMany().HasForeignKey(x => x.DecidedBy);
            mpcr.HasIndex(x => new { x.ChildId, x.Status });
        });

        modelBuilder.Entity<Invoice>(inv =>
        {
            inv.ToTable("invoices");
            inv.HasKey(x => x.Id);
            // Feature 014 — identity column, distinct from the GUID Id primary key. Feeds only
            // the OGM base number (research.md R3); never used as a foreign key or exposed as
            // "the" invoice identifier anywhere else.
            inv.Property(x => x.SequenceNumber).UseIdentityAlwaysColumn();
            inv.HasIndex(x => x.SequenceNumber).IsUnique();
            inv.Property(x => x.Status)
              .HasConversion(
                  v => v.ToString().ToLowerInvariant(),
                  v => (InvoiceStatus)Enum.Parse(typeof(InvoiceStatus), v, ignoreCase: true))
              .HasMaxLength(20)
              .IsRequired();
            inv.Property(x => x.LineItems).HasColumnType("jsonb").IsRequired();
            inv.Property(x => x.OgmReference).HasMaxLength(30).IsRequired();
            inv.HasIndex(x => x.OgmReference).IsUnique();
            inv.HasIndex(x => new { x.ChildId, x.ContractId, x.LocationId, x.PeriodMonth }).IsUnique();
            inv.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
            inv.HasOne<Contract>().WithMany().HasForeignKey(x => x.ContractId);
            inv.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
        });
    }
}
