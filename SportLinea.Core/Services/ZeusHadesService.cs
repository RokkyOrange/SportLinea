using Microsoft.Extensions.Caching.Memory;
using SportLinea.Data;
using SportLinea.Models;

namespace SportLinea.Services;

public interface IZeusHadesService
{
    Task<(bool Success, string Message, ZeusSpinResult? Result)> SpinAsync(string playerId, decimal amount);
    Task<(bool Success, string Message, ZeusRevealResult? Result)> RevealLineAsync(string playerId, bool isRow, int index);
    Task<(bool Success, string Message, ZeusCompleteResult? Result)> CompleteRoundAsync(string playerId);
    Task<(bool Success, string Message, ZeusConfrontationResult? Result)> PlayConfrontationRoundAsync(string playerId);
}

public class ZeusSpinResult
{
    public decimal NewBalance { get; set; }
    public string[][] Grid { get; set; } = Array.Empty<string[]>();
    public string[][] GridKeys { get; set; } = Array.Empty<string[]>();
    public string[][] GridEmojis { get; set; } = Array.Empty<string[]>();
    public bool ConfrontationQueued { get; set; }
}

public class ZeusRevealResult
{
    public int Index { get; set; }
    public bool IsRow { get; set; }
    public int Multiplier { get; set; }
    public int RevealedRows { get; set; }
    public int RevealedCols { get; set; }
    public bool AllRevealed { get; set; }
}

public class ZeusCompleteResult
{
    public decimal Winnings { get; set; }
    public decimal NewBalance { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool ConfrontationStarted { get; set; }
    public ZeusIntersectionHit[] Hits { get; set; } = Array.Empty<ZeusIntersectionHit>();
}

public class ZeusConfrontationResult
{
    public int Round { get; set; }
    public int TotalRounds { get; set; }
    public decimal RoundWinnings { get; set; }
    public decimal TotalBonusWinnings { get; set; }
    public decimal NewBalance { get; set; }
    public bool BonusComplete { get; set; }
    public string Message { get; set; } = string.Empty;
    public string[][] Grid { get; set; } = Array.Empty<string[]>();
    public string[][] GridEmojis { get; set; } = Array.Empty<string[]>();
    public int[] RowMultipliers { get; set; } = Array.Empty<int>();
    public int[] ColMultipliers { get; set; } = Array.Empty<int>();
    public ZeusIntersectionHit[] Hits { get; set; } = Array.Empty<ZeusIntersectionHit>();
}

public class ZeusIntersectionHit
{
    public int Row { get; set; }
    public int Col { get; set; }
    public int PickedMultiplier { get; set; }
    public decimal Winnings { get; set; }
    public string SymbolEmoji { get; set; } = string.Empty;
}

internal class PendingZeusGame
{
    public string PlayerId { get; set; } = string.Empty;
    public decimal StakeAmount { get; set; }
    public ZeusGamePhase Phase { get; set; }
    public ZeusSymbol[][] Grid { get; set; } = Array.Empty<ZeusSymbol[]>();
    public int[] RowMultipliers { get; set; } = Array.Empty<int>();
    public int[] ColMultipliers { get; set; } = Array.Empty<int>();
    public bool[] RowRevealed { get; set; } = Array.Empty<bool>();
    public bool[] ColRevealed { get; set; } = Array.Empty<bool>();
    public bool ConfrontationQueued { get; set; }
    public int ConfrontationRound { get; set; }
    public decimal ConfrontationWinnings { get; set; }
    public int[]? ConfrontationRowMults { get; set; }
    public int[]? ConfrontationColMults { get; set; }
    public ZeusSymbol[][]? ConfrontationGrid { get; set; }
}

public class ZeusHadesService : IZeusHadesService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IMemoryCache _cache;

    public ZeusHadesService(
        ApplicationDbContext context,
        INotificationService notificationService,
        IMemoryCache cache)
    {
        _context = context;
        _notificationService = notificationService;
        _cache = cache;
    }

    private static string CacheKey(string playerId) => $"zeus-hades-{playerId}";

    private void LogZeusSpin(string playerId, ZeusSpinKind kind, bool won)
    {
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.ZeusHadesSpin,
            ZeusSpinKind = kind,
            RouletteSpinWon = won,
            CreatedAt = DateTime.UtcNow
        });
    }

    private void AddZeusWin(
        string playerId,
        decimal amount,
        ZeusSpinKind kind,
        decimal zeusSideAmount,
        decimal hadesSideAmount)
    {
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.ZeusHadesWin,
            Amount = amount,
            ZeusSpinKind = kind,
            ZeusSideWinAmount = zeusSideAmount,
            HadesSideWinAmount = hadesSideAmount,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<(bool Success, string Message, ZeusSpinResult? Result)> SpinAsync(string playerId, decimal amount)
    {
        if (!ZeusVsHadesConfig.IsAllowedStake(amount))
            return (false, "Выберите ставку из предложенных вариантов", null);

        if (_cache.TryGetValue(CacheKey(playerId), out PendingZeusGame? existing) && existing != null)
            return (false, "Сначала завершите текущий раунд или бонус Confrontation!", null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null || player.Status == UserStatus.Blocked)
            return (false, "Аккаунт недоступен", null);

        if (player.Balance < amount)
            return (false, "Недостаточно средств на счёте", null);

        player.Balance -= amount;
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.ZeusHadesStake,
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        });

        var confrontationQueued = ZeusVsHadesConfig.RollConfrontation();
        var pending = new PendingZeusGame
        {
            PlayerId = playerId,
            StakeAmount = amount,
            Phase = ZeusGamePhase.AwaitingLineReveal,
            Grid = ZeusVsHadesConfig.BuildGrid(),
            RowMultipliers = ZeusVsHadesConfig.BuildLineMultipliers(confrontation: false),
            ColMultipliers = ZeusVsHadesConfig.BuildLineMultipliers(confrontation: false),
            RowRevealed = new bool[ZeusVsHadesConfig.GridSize],
            ColRevealed = new bool[ZeusVsHadesConfig.GridSize],
            ConfrontationQueued = confrontationQueued
        };

        _cache.Set(CacheKey(playerId), pending, TimeSpan.FromMinutes(20));
        await _context.SaveChangesAsync();

        return (true, confrontationQueued
            ? "Сетка готова! Откройте огонь и молнии — после раунда вас ждёт Confrontation!"
            : "Сетка готова! Откройте символы огня слева и молнии сверху.", new ZeusSpinResult
        {
            NewBalance = player.Balance,
            Grid = ToLabelGrid(pending.Grid),
            GridKeys = ToKeyGrid(pending.Grid),
            GridEmojis = ToEmojiGrid(pending.Grid),
            ConfrontationQueued = confrontationQueued
        });
    }

    public Task<(bool Success, string Message, ZeusRevealResult? Result)> RevealLineAsync(
        string playerId, bool isRow, int index)
    {
        if (!_cache.TryGetValue(CacheKey(playerId), out PendingZeusGame? pending) || pending == null)
            return Task.FromResult<(bool, string, ZeusRevealResult?)>((false, "Нет активного раунда. Сделайте спин.", null));

        if (pending.Phase != ZeusGamePhase.AwaitingLineReveal)
            return Task.FromResult<(bool, string, ZeusRevealResult?)>((false, "Сейчас идёт бонус Confrontation", null));

        if (index < 0 || index >= ZeusVsHadesConfig.GridSize)
            return Task.FromResult<(bool, string, ZeusRevealResult?)>((false, "Некорректная линия", null));

        if (isRow)
        {
            if (pending.RowRevealed[index])
                return Task.FromResult<(bool, string, ZeusRevealResult?)>((false, "Эта строка уже открыта", null));
            pending.RowRevealed[index] = true;
        }
        else
        {
            if (pending.ColRevealed[index])
                return Task.FromResult<(bool, string, ZeusRevealResult?)>((false, "Этот столбец уже открыт", null));
            pending.ColRevealed[index] = true;
        }

        _cache.Set(CacheKey(playerId), pending, TimeSpan.FromMinutes(20));

        var multiplier = isRow ? pending.RowMultipliers[index] : pending.ColMultipliers[index];
        var revealedRows = pending.RowRevealed.Count(r => r);
        var revealedCols = pending.ColRevealed.Count(c => c);

        return Task.FromResult<(bool, string, ZeusRevealResult?)>((true, isRow
            ? $"Строка {index + 1}: ×{multiplier}"
            : $"Столбец {index + 1}: ×{multiplier}", new ZeusRevealResult
        {
            Index = index,
            IsRow = isRow,
            Multiplier = multiplier,
            RevealedRows = revealedRows,
            RevealedCols = revealedCols,
            AllRevealed = revealedRows == ZeusVsHadesConfig.GridSize &&
                            revealedCols == ZeusVsHadesConfig.GridSize
        }));
    }

    public async Task<(bool Success, string Message, ZeusCompleteResult? Result)> CompleteRoundAsync(string playerId)
    {
        if (!_cache.TryGetValue(CacheKey(playerId), out PendingZeusGame? pending) || pending == null)
            return (false, "Нет активного раунда", null);

        if (pending.Phase != ZeusGamePhase.AwaitingLineReveal)
            return (false, "Раунд уже завершён", null);

        if (!pending.RowRevealed.All(r => r) || !pending.ColRevealed.All(c => c))
            return (false, "Сначала откройте все символы огня и молнии", null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null)
            return (false, "Аккаунт не найден", null);

        var (winnings, zeusWinnings, hadesWinnings, hits) = CalculateHits(
            pending.Grid, pending.RowMultipliers, pending.ColMultipliers, pending.StakeAmount);

        var confrontationStarted = pending.ConfrontationQueued;
        LogZeusSpin(playerId, ZeusSpinKind.Main, winnings > 0 || confrontationStarted);

        if (winnings > 0)
        {
            player.Balance += winnings;
            AddZeusWin(playerId, winnings, ZeusSpinKind.Main, zeusWinnings, hadesWinnings);
        }

        if (confrontationStarted)
        {
            pending.Phase = ZeusGamePhase.Confrontation;
            pending.ConfrontationRound = 0;
            pending.ConfrontationWinnings = 0;
        }
        else
        {
            _cache.Remove(CacheKey(playerId));
        }

        if (confrontationStarted)
            _cache.Set(CacheKey(playerId), pending, TimeSpan.FromMinutes(20));

        await _context.SaveChangesAsync();

        var message = winnings > 0
            ? $"Выигрыш {winnings:N2} ₽!{(confrontationStarted ? " Начинается Confrontation!" : "")}"
            : confrontationStarted
                ? "Сыгровки нет. Начинается Confrontation!"
                : "Сыгровки нет.";

        if (!confrontationStarted)
        {
            await _notificationService.SendAsync(playerId, NotificationType.Info,
                $"Zeus vs Hades: {message}");
        }

        return (true, message, new ZeusCompleteResult
        {
            Winnings = winnings,
            NewBalance = player.Balance,
            Message = message,
            ConfrontationStarted = confrontationStarted,
            Hits = hits
        });
    }

    public async Task<(bool Success, string Message, ZeusConfrontationResult? Result)> PlayConfrontationRoundAsync(
        string playerId)
    {
        if (!_cache.TryGetValue(CacheKey(playerId), out PendingZeusGame? pending) || pending == null)
            return (false, "Нет активного бонуса", null);

        if (pending.Phase != ZeusGamePhase.Confrontation)
            return (false, "Confrontation не активен", null);

        if (pending.ConfrontationRound >= ZeusVsHadesConfig.ConfrontationRounds)
            return (false, "Бонус уже завершён", null);

        pending.ConfrontationRound++;
        var grid = ZeusVsHadesConfig.BuildGrid();
        var rowMults = ZeusVsHadesConfig.BuildLineMultipliers(confrontation: true);
        var colMults = ZeusVsHadesConfig.BuildLineMultipliers(confrontation: true);

        var (roundWin, _, _, hits) = CalculateHits(grid, rowMults, colMults, pending.StakeAmount);
        pending.ConfrontationWinnings += roundWin;

        var player = await _context.Users.FindAsync(playerId);
        if (player == null)
            return (false, "Аккаунт не найден", null);

        LogZeusSpin(playerId, ZeusSpinKind.Confrontation, roundWin > 0);

        var bonusComplete = pending.ConfrontationRound >= ZeusVsHadesConfig.ConfrontationRounds;
        if (bonusComplete && pending.ConfrontationWinnings > 0)
        {
            player.Balance += pending.ConfrontationWinnings;
            AddZeusWin(playerId, pending.ConfrontationWinnings, ZeusSpinKind.Confrontation, 0m, 0m);
        }

        if (bonusComplete)
            _cache.Remove(CacheKey(playerId));
        else
            _cache.Set(CacheKey(playerId), pending, TimeSpan.FromMinutes(20));

        await _context.SaveChangesAsync();

        var message = bonusComplete
            ? pending.ConfrontationWinnings > 0
                ? $"Confrontation завершён! Бонус {pending.ConfrontationWinnings:N2} ₽"
                : "Confrontation завершён без выигрыша."
            : $"Раунд {pending.ConfrontationRound}/{ZeusVsHadesConfig.ConfrontationRounds}: +{roundWin:N2} ₽";

        if (bonusComplete)
        {
            await _notificationService.SendAsync(playerId, NotificationType.Info,
                $"Zeus vs Hades: {message}");
        }

        return (true, message, new ZeusConfrontationResult
        {
            Round = pending.ConfrontationRound,
            TotalRounds = ZeusVsHadesConfig.ConfrontationRounds,
            RoundWinnings = roundWin,
            TotalBonusWinnings = pending.ConfrontationWinnings,
            NewBalance = player.Balance,
            BonusComplete = bonusComplete,
            Message = message,
            Grid = ToLabelGrid(grid),
            GridEmojis = ToEmojiGrid(grid),
            RowMultipliers = rowMults,
            ColMultipliers = colMults,
            Hits = hits
        });
    }

    private static (decimal Total, decimal ZeusTotal, decimal HadesTotal, ZeusIntersectionHit[] Hits) CalculateHits(
        ZeusSymbol[][] grid,
        int[] rowMultipliers,
        int[] colMultipliers,
        decimal stake)
    {
        var hits = new List<ZeusIntersectionHit>();
        var rng = Random.Shared;
        decimal total = 0;
        decimal zeusTotal = 0;
        decimal hadesTotal = 0;

        for (var r = 0; r < ZeusVsHadesConfig.GridSize; r++)
        {
            for (var c = 0; c < ZeusVsHadesConfig.GridSize; c++)
            {
                var rowMult = rowMultipliers[r];
                var colMult = colMultipliers[c];
                var pickMax = rng.Next(2) == 0;
                var pickMult = pickMax
                    ? Math.Max(rowMult, colMult)
                    : Math.Min(rowMult, colMult);

                if (pickMult <= 0)
                    continue;

                var symbolPay = ZeusVsHadesConfig.GetSymbolPayout(grid[r][c]);
                var cellWin = Math.Round(stake * symbolPay * pickMult / (decimal)ZeusVsHadesConfig.IntersectionScale, 2);
                if (cellWin <= 0)
                    continue;

                var side = ZeusVsHadesConfig.ResolvePickSide(rowMult, colMult, pickMax, rng);
                total += cellWin;
                if (side == ZeusMultiplierPickSide.Zeus)
                    zeusTotal += cellWin;
                else
                    hadesTotal += cellWin;

                hits.Add(new ZeusIntersectionHit
                {
                    Row = r,
                    Col = c,
                    PickedMultiplier = pickMult,
                    Winnings = cellWin,
                    SymbolEmoji = ZeusVsHadesConfig.GetSymbolEmoji(grid[r][c])
                });
            }
        }

        return (Math.Round(total, 2), Math.Round(zeusTotal, 2), Math.Round(hadesTotal, 2), hits.ToArray());
    }

    private static string[][] ToLabelGrid(ZeusSymbol[][] grid) =>
        grid.Select(row => row.Select(ZeusVsHadesConfig.GetSymbolLabel).ToArray()).ToArray();

    private static string[][] ToKeyGrid(ZeusSymbol[][] grid) =>
        grid.Select(row => row.Select(ZeusVsHadesConfig.GetSymbolKey).ToArray()).ToArray();

    private static string[][] ToEmojiGrid(ZeusSymbol[][] grid) =>
        grid.Select(row => row.Select(ZeusVsHadesConfig.GetSymbolEmoji).ToArray()).ToArray();
}
