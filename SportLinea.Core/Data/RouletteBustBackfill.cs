using Microsoft.EntityFrameworkCore;
using SportLinea.Models;

namespace SportLinea.Data;

public static class RouletteBustBackfill
{
    public static async Task RunAsync(ApplicationDbContext context)
    {
        var playerIds = await context.AccountOperations
            .Where(o => o.OperationType == OperationType.RouletteStake ||
                        o.OperationType == OperationType.RouletteWin)
            .Select(o => o.PlayerId)
            .Distinct()
            .ToListAsync();

        var updated = false;

        foreach (var playerId in playerIds)
        {
            var player = await context.Users.FindAsync(playerId);
            if (player == null) continue;

            var ops = await context.AccountOperations
                .Where(o => o.PlayerId == playerId)
                .OrderBy(o => o.CreatedAt)
                .ThenBy(o => o.Id)
                .ToListAsync();

            var pendingBusts = new List<AccountOperation>();

            var bustNotifications = await context.Notifications
                .Where(n => n.UserId == playerId &&
                            n.Text.Contains("БАНКРОТ") &&
                            n.Text.Contains("Crazy Time"))
                .OrderBy(n => n.SentAt)
                .ToListAsync();

            foreach (var notification in bustNotifications)
            {
                if (HasBustNear(ops, pendingBusts, notification.SentAt))
                    continue;

                var allOps = ops.Concat(pendingBusts).ToList();
                var lostBalance = CalculateBalanceAt(allOps, player.Balance, notification.SentAt);

                if (lostBalance <= 0)
                    continue;

                pendingBusts.Add(CreateBustOp(playerId, lostBalance, notification.SentAt, context));
                updated = true;
            }

            var allOpsFinal = ops.Concat(pendingBusts).ToList();
            var recordedBust = allOpsFinal
                .Where(o => o.OperationType == OperationType.RouletteBust)
                .Sum(o => o.Amount);

            var totalBustNeeded = ComputeTotalBustNeeded(player, allOpsFinal);
            var toAdd = Math.Round(totalBustNeeded - recordedBust, 2);

            if (toAdd > 0.01m)
            {
                var lastRouletteAt = allOpsFinal
                    .Where(o => o.OperationType is OperationType.RouletteStake or OperationType.RouletteWin)
                    .MaxBy(o => o.CreatedAt)?.CreatedAt ?? DateTime.UtcNow;

                pendingBusts.Add(CreateBustOp(playerId, toAdd, lastRouletteAt, context));
                updated = true;
            }
        }

        if (updated)
            await context.SaveChangesAsync();
    }

    public static decimal ComputeTotalBustNeeded(ApplicationUser player, IReadOnlyList<AccountOperation> ops)
    {
        var paidStaked = ops
            .Where(o => o.OperationType == OperationType.RouletteStake && !o.IsFreeBonus)
            .Sum(o => o.Amount);

        var won = ops
            .Where(o => o.OperationType == OperationType.RouletteWin)
            .Sum(o => o.Amount);

        var seed = GetStartingBalance(player, ops);
        var nonRoulette = GetNonRouletteEffect(ops);

        var needed = seed + nonRoulette - paidStaked + won - player.Balance;
        return needed > 0 ? needed : 0;
    }

    public static decimal GetRecordedBust(IReadOnlyList<AccountOperation> ops) =>
        ops.Where(o => o.OperationType == OperationType.RouletteBust).Sum(o => o.Amount);

    internal static decimal CalculateBalanceAt(
        IReadOnlyList<AccountOperation> ops,
        decimal currentBalance,
        DateTime at)
    {
        var totalEffectWithoutBust = ops
            .Where(o => o.OperationType != OperationType.RouletteBust)
            .Sum(GetBalanceEffect);

        var totalBust = ops
            .Where(o => o.OperationType == OperationType.RouletteBust)
            .Sum(o => o.Amount);

        var baseBalance = currentBalance - totalEffectWithoutBust + totalBust;

        var effectUntil = ops
            .Where(o => o.OperationType != OperationType.RouletteBust && o.CreatedAt <= at)
            .Sum(GetBalanceEffect);

        return baseBalance + effectUntil;
    }

    private static AccountOperation CreateBustOp(
        string playerId,
        decimal amount,
        DateTime createdAt,
        ApplicationDbContext context)
    {
        var bustOp = new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.RouletteBust,
            Amount = amount,
            CreatedAt = createdAt
        };
        context.AccountOperations.Add(bustOp);
        return bustOp;
    }

    private static bool HasBustNear(
        IReadOnlyList<AccountOperation> ops,
        IReadOnlyList<AccountOperation> pending,
        DateTime at) =>
        ops.Concat(pending).Any(o =>
            o.OperationType == OperationType.RouletteBust &&
            o.CreatedAt >= at.AddMinutes(-2) &&
            o.CreatedAt <= at.AddMinutes(2));

    private static decimal GetStartingBalance(ApplicationUser player, IReadOnlyList<AccountOperation> ops)
    {
        var hasDeposits = ops.Any(o => o.OperationType == OperationType.Deposit);
        var effectNoBust = ops
            .Where(o => o.OperationType != OperationType.RouletteBust)
            .Sum(GetBalanceEffect);
        var bust = ops
            .Where(o => o.OperationType == OperationType.RouletteBust)
            .Sum(o => o.Amount);

        var implied = player.Balance - effectNoBust + bust;
        if (implied > 0)
            return implied;

        return hasDeposits ? 0m : 10000m;
    }

    private static decimal GetNonRouletteEffect(IReadOnlyList<AccountOperation> ops) =>
        ops.Sum(o => o.OperationType switch
        {
            OperationType.Deposit => o.Amount,
            OperationType.Withdrawal => -o.Amount,
            OperationType.BetWithdrawal => -o.Amount,
            OperationType.Win => o.Amount,
            OperationType.BalanceReset => -o.Amount,
            _ => 0m
        });

    private static decimal GetBalanceEffect(AccountOperation op) => op.OperationType switch
    {
        OperationType.Deposit => op.Amount,
        OperationType.Withdrawal => -op.Amount,
        OperationType.BetWithdrawal => -op.Amount,
        OperationType.Win => op.Amount,
        OperationType.RouletteStake when !op.IsFreeBonus => -op.Amount,
        OperationType.RouletteWin => op.Amount,
        OperationType.RouletteBust => -op.Amount,
        OperationType.BalanceReset => -op.Amount,
        _ => 0m
    };
}
