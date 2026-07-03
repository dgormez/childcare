namespace ChildCare.Domain.Entities;

public class TenantUserRefreshToken
{
    public Guid     Id           { get; set; } = Guid.NewGuid();
    public Guid     TenantUserId { get; set; }
    public string   Token        { get; set; } = string.Empty;
    public DateTime ExpiresAt    { get; set; }
}
