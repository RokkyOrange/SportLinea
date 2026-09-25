using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SportLinea.Models;

namespace SportLinea.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<SportEvent> SportEvents => Set<SportEvent>();
    public DbSet<Coefficient> Coefficients => Set<Coefficient>();
    public DbSet<Bet> Bets => Set<Bet>();
    public DbSet<AccountOperation> AccountOperations => Set<AccountOperation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Bonus> Bonuses => Set<Bonus>();
    public DbSet<ActionLog> ActionLogs => Set<ActionLog>();
    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SportEvent>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(500);
            e.Property(x => x.SportType).HasMaxLength(100);
            e.Property(x => x.SportCategory).HasMaxLength(100);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Coefficient>(e =>
        {
            e.Property(x => x.Value).HasPrecision(10, 2);
            e.Property(x => x.OutcomeDescription).HasMaxLength(200);
        });

        builder.Entity<Bet>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.CoefficientValue).HasPrecision(10, 2);
            e.Property(x => x.OutcomeDescription).HasMaxLength(200);
            e.Property(x => x.Winnings).HasPrecision(18, 2);

            e.HasOne(b => b.SportEvent)
                .WithMany(se => se.Bets)
                .HasForeignKey(b => b.SportEventId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Coefficient)
                .WithMany(c => c.Bets)
                .HasForeignKey(b => b.CoefficientId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Player)
                .WithMany(u => u.Bets)
                .HasForeignKey(b => b.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Bonus)
                .WithMany()
                .HasForeignKey(b => b.BonusId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AccountOperation>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.ZeusSideWinAmount).HasPrecision(18, 2);
            e.Property(x => x.HadesSideWinAmount).HasPrecision(18, 2);
            e.Property(x => x.CamelotCollectorNominal).HasPrecision(18, 2);

            e.HasOne(o => o.Bet)
                .WithMany()
                .HasForeignKey(o => o.BetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.Balance).HasPrecision(18, 2);
            e.Property(x => x.BlockReason).HasMaxLength(500);
            e.HasOne(x => x.BlockedBy)
                .WithMany()
                .HasForeignKey(x => x.BlockedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Bonus>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.BookmakerComment).HasMaxLength(500);
            e.HasOne(x => x.AwardedBy)
                .WithMany()
                .HasForeignKey(x => x.AwardedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<WithdrawalRequest>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProcessedBy)
                .WithMany()
                .HasForeignKey(x => x.ProcessedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
