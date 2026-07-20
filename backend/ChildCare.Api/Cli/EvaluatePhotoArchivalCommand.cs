using ChildCare.Application.Common;
using ChildCare.Application.GroupActivities;
using ChildCare.Domain.Enums;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChildCare.Api.Cli;

/// <summary>
/// The `evaluate-photo-archival` CLI subcommand body (031-photo-lifecycle-governance, research.md
/// R2/R5). Mirrors SendPaymentRemindersCommand's tenant-loop structure exactly, including
/// per-tenant failure isolation. Two independent transitions per tenant, both storage-class-only
/// (never delete, never affect resolvability — FR-001/SC-005):
///
/// 1. Archive-on-departure: a deactivated child's profile photo and health/vaccine attachments
///    move to Coldline 30 days after `DeactivatedAt`; a group-activity photo moves to Coldline
///    once every one of its derived children (IGroupActivityChildDerivationService, shared with
///    GetParentGroupActivityGalleryQuery and PurgeChildPhotosCommand) has been inactive for 30
///    days.
/// 2. General no-activity tiering: `group-activities/` full-resolution objects (only — never the
///    thumbnail) move to Nearline after 90 days, approximated by `UploadedAt` rather than a real
///    GCS access-time lookup (research.md R2). The other four prefixes get this same 90-day rule
///    natively via the Terraform `lifecycle_rule` on the bucket (research.md R5) — no app-level
///    work needed for those.
///
/// A photo eligible for Coldline is never also tiered to Nearline in the same run (Coldline
/// takes precedence), so this never downgrades an already-archived object.
/// </summary>
public static class EvaluatePhotoArchivalCommand
{
    private const int ArchiveEligibilityDays = 30;
    private const int GeneralTieringEligibilityDays = 90;
    private const string Coldline = "COLDLINE";
    private const string Nearline = "NEARLINE";

    /// <summary>Returns the process exit code: 0 if every tenant succeeded, 1 if any failed.</summary>
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var publicDb = services.GetRequiredService<PublicDbContext>();
        var resolver = services.GetRequiredService<ITenantDbContextResolver>();
        var profileStorage = services.GetRequiredService<IProfilePhotoStorage>();
        var healthStorage = services.GetRequiredService<IHealthAttachmentStorage>();
        var groupActivityStorage = services.GetRequiredService<IGroupActivityPhotoStorage>();

        var tenants = await publicDb.Tenants
            .Where(t => t.ProvisioningStatus == ProvisioningStatus.Ready)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var failureCount = 0;
        var now = DateTime.UtcNow;

        foreach (var tenant in tenants)
        {
            try
            {
                var db = resolver.ForSchema(tenant.SchemaName);
                var transitioned = await ProcessTenantAsync(db, profileStorage, healthStorage, groupActivityStorage, now);
                Console.WriteLine($"{tenant.Slug}: {transitioned} object(s) transitioned");
            }
            catch (Exception ex)
            {
                failureCount++;
                Console.WriteLine($"{tenant.Slug}: failed — {ex.Message}");
            }
        }

        Console.WriteLine($"Summary: {tenants.Count - failureCount}/{tenants.Count} tenants succeeded.");

        return failureCount == 0 ? 0 : 1;
    }

    private static async Task<int> ProcessTenantAsync(
        ITenantDbContext db, IProfilePhotoStorage profileStorage, IHealthAttachmentStorage healthStorage,
        IGroupActivityPhotoStorage groupActivityStorage, DateTime now)
    {
        var derivation = new GroupActivityChildDerivationService(db);
        var transitioned = 0;
        var archiveThreshold = now.AddDays(-ArchiveEligibilityDays);

        var departedChildren = await db.Children
            .Where(c => c.DeactivatedAt != null && c.DeactivatedAt <= archiveThreshold)
            .ToListAsync();

        foreach (var child in departedChildren.Where(c => c.ProfilePhotoObjectPath != null))
        {
            await profileStorage.SetStorageClassAsync(child.ProfilePhotoObjectPath!, Coldline);
            transitioned++;
        }

        var departedChildIds = departedChildren.Select(c => c.Id).ToList();

        var departedAttachmentPaths = await db.HealthRecords
            .Where(r => departedChildIds.Contains(r.ChildId) && r.AttachmentObjectPath != null)
            .Select(r => r.AttachmentObjectPath!)
            .Concat(db.VaccineRecords
                .Where(r => departedChildIds.Contains(r.ChildId) && r.AttachmentObjectPath != null)
                .Select(r => r.AttachmentObjectPath!))
            .ToListAsync();

        foreach (var path in departedAttachmentPaths)
        {
            await healthStorage.SetStorageClassAsync(path, Coldline);
            transitioned++;
        }

        // Every currently-deactivated child's DeactivatedAt, keyed by child id — used below to
        // evaluate group-activity photo archival eligibility without a query per photo.
        var deactivatedAtByChildId = await db.Children
            .Where(c => c.DeactivatedAt != null)
            .ToDictionaryAsync(c => c.Id, c => c.DeactivatedAt!.Value);

        var generalTieringThreshold = now.AddDays(-GeneralTieringEligibilityDays);
        var photos = await db.GroupActivityPhotos.ToListAsync();

        foreach (var photo in photos)
        {
            var depictedChildIds = await derivation.GetDepictedChildIdsAsync(photo.GroupActivityId);

            var isArchiveEligible = depictedChildIds.Count > 0 && depictedChildIds.All(childId =>
                deactivatedAtByChildId.TryGetValue(childId, out var deactivatedAt) && deactivatedAt <= archiveThreshold);

            if (isArchiveEligible)
            {
                await groupActivityStorage.SetStorageClassAsync(photo.ObjectPath, Coldline);
                transitioned++;
                continue;
            }

            if (photo.UploadedAt <= generalTieringThreshold)
            {
                await groupActivityStorage.SetStorageClassAsync(photo.ObjectPath, Nearline);
                transitioned++;
            }
        }

        return transitioned;
    }
}
