namespace ChildCare.Domain.Entities;

// Feature 025, data-model.md. One row per uploaded CODA file — gives the import summary and
// FR-013's "N skipped as already imported" reporting a concrete home, and an audit trail of
// who uploaded what/when.
public class CodaImport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FileName { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public Guid ImportedByUserId { get; set; }

    public int TransactionCount { get; set; }

    public int SkippedDuplicateCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
