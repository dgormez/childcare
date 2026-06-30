using Microsoft.EntityFrameworkCore;
using ChildCare.Api.Models;

namespace ChildCare.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>             Users             => Set<User>();
    public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();
    public DbSet<Habit>            Habits            => Set<Habit>();
    public DbSet<HabitCompletion>  HabitCompletions  => Set<HabitCompletion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(u =>
        {
            u.HasKey(x => x.Id);
            u.HasIndex(x => x.Email).IsUnique();
            u.Property(x => x.Email).IsRequired().HasMaxLength(254);
            u.Property(x => x.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<UserRefreshToken>(t =>
        {
            t.HasKey(x => x.Id);
            t.HasIndex(x => x.Token).IsUnique();
            t.Property(x => x.Token).IsRequired().HasMaxLength(128);
            t.HasOne(x => x.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Habit>(h =>
        {
            h.HasKey(x => x.Id);
            h.HasIndex(x => x.UserId);
            h.Property(x => x.Name).IsRequired().HasMaxLength(100);
            h.Property(x => x.Color).IsRequired().HasMaxLength(7);
            h.Property(x => x.Icon).IsRequired().HasMaxLength(10);
            h.HasOne(x => x.User)
             .WithMany(u => u.Habits)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HabitCompletion>(c =>
        {
            c.HasKey(x => x.Id);
            c.HasIndex(x => new { x.HabitId, x.Date }).IsUnique(); // one completion per habit per day
            c.HasIndex(x => new { x.UserId, x.Date });              // for fetching all completions by date
            c.HasOne(x => x.Habit)
             .WithMany(h => h.Completions)
             .HasForeignKey(x => x.HabitId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
