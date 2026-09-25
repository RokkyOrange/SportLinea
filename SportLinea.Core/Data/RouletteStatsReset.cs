using Microsoft.EntityFrameworkCore;
using SportLinea.Models;

namespace SportLinea.Data;

public static class RouletteStatsReset
{
    public static async Task ClearLegacyRouletteOperationsAsync(ApplicationDbContext context)
    {
        await context.AccountOperations
            .Where(o => o.OperationType == OperationType.RouletteStake ||
                        o.OperationType == OperationType.RouletteWin ||
                        o.OperationType == OperationType.RouletteBust ||
                        o.OperationType == OperationType.RouletteSpin)
            .ExecuteDeleteAsync();
    }
}
