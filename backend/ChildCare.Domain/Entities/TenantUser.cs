namespace ChildCare.Domain.Entities;

public class TenantUser
{
    public Guid     Id           { get; set; } = Guid.NewGuid();
    public string   Email        { get; set; } = string.Empty;
    public string   PasswordHash { get; set; } = string.Empty;
    public string   Name         { get; set; } = string.Empty;
    public string?  GoogleId     { get; set; }
    public string?  AppleId      { get; set; }

    // Email verification (OAuth users are pre-verified by their provider)
    public bool      EmailVerified           { get; set; } = false;
    public string?   EmailVerificationToken  { get; set; }
    public DateTime? EmailVerificationExpiry { get; set; }

    public string?   PasswordResetToken  { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }

    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
}
