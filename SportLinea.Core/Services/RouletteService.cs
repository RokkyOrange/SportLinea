using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SportLinea.Data;
using SportLinea.Models;

namespace SportLinea.Services;

public interface IRouletteService
{
    Task<(bool Success, string Message, RouletteSpinResult? Result)> SpinAsync(string playerId, decimal amount, bool useFreeSpin = false);
    Task<(bool Success, string Message, RouletteBonusResult? Result)> BonusSpinAsync(string playerId);
    Task<(bool Success, string Message, RouletteBuyBonusResult? Result)> BuyBonusAsync(string playerId, decimal stakeAmount, RouletteBonusKind bonusKind);
    Task<RoulettePendingBonusState?> GetPendingBonusAsync(string playerId);
}

public class RoulettePendingBonusState
{
    public RouletteBonusKind BonusKind { get; set; }
    public int SpinsRemaining { get; set; }
    public decimal StakeAmount { get; set; }
    public decimal TotalBonusWinnings { get; set; }
}

public class RouletteBuyBonusResult
{
    public decimal NewBalance { get; set; }
    public decimal PricePaid { get; set; }
    public decimal StakeAmount { get; set; }
    public RouletteBonusKind BonusKind { get; set; }
    public int BonusSpinsRemaining { get; set; }
}

public class RouletteSpinResult
{
    public int SegmentIndex { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Multiplier { get; set; }
    public decimal Winnings { get; set; }
    public decimal NewBalance { get; set; }
    public bool IsBonus { get; set; }
    public RouletteBonusKind BonusKind { get; set; }
    public int BonusSpinsRemaining { get; set; }
    public string? BonusAwardMessage { get; set; }
}

public class RouletteBonusResult
{
    public int SegmentIndex { get; set; }
    public string Label { get; set; } = string.Empty;
    public RouletteBonusKind BonusKind { get; set; }
    public bool IsJackpot { get; set; }
    public bool IsBankrupt { get; set; }
    public bool IsSensation { get; set; }
    public decimal Multiplier { get; set; }
    public decimal Winnings { get; set; }
    public decimal NewBalance { get; set; }
    public int SpinsRemaining { get; set; }
    public bool BonusComplete { get; set; }
    public decimal TotalBonusWinnings { get; set; }
    public string? BonusTotalSummary { get; set; }
}

public class RouletteService : IRouletteService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IMemoryCache _cache;
    private readonly IBonusService _bonusService;

    public RouletteService(
        ApplicationDbContext context,
        INotificationService notificationService,
        IMemoryCache cache,
        IBonusService bonusService)
    {
        _context = context;
        _notificationService = notificationService;
        _cache = cache;
        _bonusService = bonusService;
    }

    private static string BonusCacheKey(string playerId) => $"roulette-bonus-{playerId}";

    private static string FormatBonusTotalSummary(decimal total) =>
        total switch
        {
            > 0 => $"Итого в бонусной игре: +{total:N2} ₽",
            < 0 => $"Итого в бонусной игре: {total:N2} ₽",
            _ => "Итого в бонусной игре: 0 ₽"
        };

    private void LogRouletteSpin(string playerId, RouletteSpinKind kind, bool won)
    {
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.RouletteSpin,
            RouletteSpinKind = kind,
            RouletteSpinWon = won,
            CreatedAt = DateTime.UtcNow
        });
    }

    private void AddRouletteWin(string playerId, decimal amount, RouletteSpinKind kind)
    {
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.RouletteWin,
            Amount = amount,
            RouletteSpinKind = kind,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<(bool Success, string Message, RouletteSpinResult? Result)> SpinAsync(string playerId, decimal amount, bool useFreeSpin = false)
    {
        Bonus? freeSpinBonus = null;
        if (useFreeSpin)
        {
            freeSpinBonus = await _bonusService.GetActiveFreeRouletteBonusAsync(playerId);
            if (freeSpinBonus == null)
                return (false, "Нет активных бесплатных спинов. Активируйте бонус в профиле.", null);

            amount = freeSpinBonus.Amount;
        }
        else if (!CrazyTimeWheel.IsAllowedStake(amount))
        {
            return (false, "Выберите ставку из предложенных вариантов", null);
        }

        if (_cache.TryGetValue(BonusCacheKey(playerId), out _))
            return (false, "Сначала завершите бонусную игру!", null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null || player.Status == UserStatus.Blocked)
            return (false, "Аккаунт недоступен", null);

        if (!useFreeSpin && player.Balance < amount)
            return (false, "Недостаточно средств на счёте", null);

        var segment = CrazyTimeWheel.PickMain();

        if (!useFreeSpin)
            player.Balance -= amount;

        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.RouletteStake,
            Amount = amount,
            IsFreeBonus = useFreeSpin,
            CreatedAt = DateTime.UtcNow
        });

        decimal winnings = 0;
        string message;
        var isBonus = segment.IsBonus;
        var bonusKind = segment.BonusKind;
        var bonusSpinsRemaining = 0;

        if (isBonus)
        {
            bonusSpinsRemaining = bonusKind == RouletteBonusKind.HotSpins
                ? CrazyTimeWheel.HotSpinsFreeSpinCount
                : 1;

            _cache.Set(BonusCacheKey(playerId), new PendingRouletteBonus
            {
                PlayerId = playerId,
                StakeAmount = amount,
                BonusKind = bonusKind,
                SpinsRemaining = bonusSpinsRemaining,
                CreatedAt = DateTime.UtcNow
            }, TimeSpan.FromMinutes(15));

            LogRouletteSpin(playerId, RouletteSpinKind.Main, won: true);

            message = bonusKind switch
            {
                RouletteBonusKind.HotSpins =>
                    $"HOT SPINS! {CrazyTimeWheel.HotSpinsFreeSpinCount} бесплатных вращений по {amount:N0} ₽!",
                _ => "CRAZY TIME! Бонусная игра — крутите второе колесо!"
            };
        }
        else
        {
            winnings = Math.Round(amount * segment.Multiplier, 2);
            player.Balance += winnings;

            LogRouletteSpin(playerId, RouletteSpinKind.Main, won: winnings > 0);

            if (winnings > 0)
                AddRouletteWin(playerId, winnings, RouletteSpinKind.Main);

            message = segment.Multiplier switch
            {
                0m => $"Не повезло! Выпало {segment.Label}. Ставка проиграна.",
                3m => $"Отлично! {segment.Label} — выигрыш {winnings:N2} ₽!",
                _ => $"Выпало {segment.Label}. Выигрыш: {winnings:N2} ₽"
            };

            await _notificationService.SendAsync(playerId, NotificationType.Info,
                $"Crazy Time: {message}");
        }

        await _context.SaveChangesAsync();

        if (useFreeSpin)
            await _bonusService.ConsumeFreeRouletteSpinAsync(playerId);

        string? bonusAwardMessage = null;
        if (!useFreeSpin)
            bonusAwardMessage = await _bonusService.CheckRouletteMilestoneAsync(playerId);

        return (true, message, new RouletteSpinResult
        {
            SegmentIndex = segment.Index,
            Label = segment.Label,
            Multiplier = segment.Multiplier,
            Winnings = winnings,
            NewBalance = player.Balance,
            IsBonus = isBonus,
            BonusKind = bonusKind,
            BonusSpinsRemaining = bonusSpinsRemaining,
            BonusAwardMessage = bonusAwardMessage
        });
    }

    public async Task<(bool Success, string Message, RouletteBonusResult? Result)> BonusSpinAsync(string playerId)
    {
        if (!_cache.TryGetValue(BonusCacheKey(playerId), out PendingRouletteBonus? pending) || pending == null)
            return (false, "Нет активной бонусной игры", null);

        return pending.BonusKind switch
        {
            RouletteBonusKind.HotSpins => await HotSpinsSpinAsync(playerId, pending),
            _ => await CrazyBonusSpinAsync(playerId, pending)
        };
    }

    private async Task<(bool Success, string Message, RouletteBonusResult? Result)> CrazyBonusSpinAsync(
        string playerId, PendingRouletteBonus pending)
    {
        var player = await _context.Users.FindAsync(playerId);
        if (player == null)
            return (false, "Аккаунт не найден", null);

        var segment = CrazyTimeWheel.PickCrazyBonus();
        decimal winnings = 0;
        decimal totalBonusWinnings;
        var isJackpot = false;
        var isBankrupt = false;
        string message;

        if (segment.Multiplier > 0)
        {
            isJackpot = true;
            winnings = Math.Round(pending.StakeAmount * segment.Multiplier, 2);
            totalBonusWinnings = winnings;
            player.Balance += winnings;
            AddRouletteWin(playerId, winnings, RouletteSpinKind.CrazyBonus);
            LogRouletteSpin(playerId, RouletteSpinKind.CrazyBonus, won: true);

            message = $"ДЖЕКПОТ! {segment.Label} — выигрыш {winnings:N2} ₽!";
        }
        else
        {
            isBankrupt = true;
            totalBonusWinnings = 0;
            LogRouletteSpin(playerId, RouletteSpinKind.CrazyBonus, won: false);
            message = $"BUST! Ставка {pending.StakeAmount:N2} ₽ проиграна";
        }

        var bonusTotalSummary = FormatBonusTotalSummary(totalBonusWinnings);
        _cache.Remove(BonusCacheKey(playerId));
        await _context.SaveChangesAsync();

        await _notificationService.SendAsync(playerId, NotificationType.Info,
            $"Crazy Time Бонус: {message} {bonusTotalSummary}");

        return (true, message, new RouletteBonusResult
        {
            SegmentIndex = segment.Index,
            Label = segment.Label,
            BonusKind = RouletteBonusKind.Crazy,
            IsJackpot = isJackpot,
            IsBankrupt = isBankrupt,
            Winnings = winnings,
            NewBalance = player.Balance,
            SpinsRemaining = 0,
            BonusComplete = true,
            TotalBonusWinnings = totalBonusWinnings,
            BonusTotalSummary = bonusTotalSummary
        });
    }

    private async Task<(bool Success, string Message, RouletteBonusResult? Result)> HotSpinsSpinAsync(
        string playerId, PendingRouletteBonus pending)
    {
        if (pending.SpinsRemaining <= 0)
            return (false, "Бесплатные вращения HOT SPINS уже завершены", null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null)
            return (false, "Аккаунт не найден", null);

        var segment = CrazyTimeWheel.PickHotSpins();
        decimal winnings = 0;
        var isSensation = segment.Label == "SENSATION";
        string message;

        if (isSensation)
        {
            winnings = Math.Round(pending.StakeAmount * segment.Multiplier, 2);
            player.Balance += winnings;

            if (winnings > 0)
                AddRouletteWin(playerId, winnings, RouletteSpinKind.HotSpins);

            message = $"SENSATION! ×{segment.Multiplier:0} — выигрыш {winnings:N2} ₽";
        }
        else if (segment.Multiplier > 0)
        {
            winnings = Math.Round(pending.StakeAmount * segment.Multiplier, 2);
            player.Balance += winnings;
            AddRouletteWin(playerId, winnings, RouletteSpinKind.HotSpins);
            message = $"{segment.Label}! Выигрыш {winnings:N2} ₽";
        }
        else
        {
            message = "×0 — без выигрыша";
        }

        LogRouletteSpin(playerId, RouletteSpinKind.HotSpins, won: winnings > 0);

        pending.TotalBonusWinnings += winnings;
        pending.SpinsRemaining--;
        var spinsLeft = pending.SpinsRemaining;
        var bonusComplete = spinsLeft <= 0;
        string? bonusTotalSummary = null;

        if (bonusComplete)
        {
            bonusTotalSummary = FormatBonusTotalSummary(pending.TotalBonusWinnings);
            _cache.Remove(BonusCacheKey(playerId));
        }
        else
            _cache.Set(BonusCacheKey(playerId), pending, TimeSpan.FromMinutes(15));

        await _context.SaveChangesAsync();

        if (bonusComplete)
        {
            await _notificationService.SendAsync(playerId, NotificationType.Info,
                $"HOT SPINS завершены. {message} {bonusTotalSummary}");
        }

        return (true, message, new RouletteBonusResult
        {
            SegmentIndex = segment.Index,
            Label = segment.Label,
            BonusKind = RouletteBonusKind.HotSpins,
            IsSensation = isSensation,
            Multiplier = segment.Multiplier,
            Winnings = winnings,
            NewBalance = player.Balance,
            SpinsRemaining = spinsLeft,
            BonusComplete = bonusComplete,
            TotalBonusWinnings = pending.TotalBonusWinnings,
            BonusTotalSummary = bonusTotalSummary
        });
    }

    public async Task<(bool Success, string Message, RouletteBuyBonusResult? Result)> BuyBonusAsync(
        string playerId, decimal stakeAmount, RouletteBonusKind bonusKind)
    {
        if (!CrazyTimeWheel.IsAllowedStake(stakeAmount))
            return (false, "Выберите ставку из предложенных вариантов", null);

        if (bonusKind is not (RouletteBonusKind.Crazy or RouletteBonusKind.HotSpins))
            return (false, "Некорректный тип бонусной игры", null);

        if (_cache.TryGetValue(BonusCacheKey(playerId), out _))
            return (false, "Сначала завершите бонусную игру!", null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null || player.Status == UserStatus.Blocked)
            return (false, "Аккаунт недоступен", null);

        var price = CrazyTimeWheel.GetBonusBuyPrice(stakeAmount);
        if (player.Balance < price)
            return (false, $"Недостаточно средств. Нужно {price:N2} ₽ (ставка × {CrazyTimeWheel.BonusBuyPriceMultiplier})", null);

        var spinsRemaining = bonusKind == RouletteBonusKind.HotSpins
            ? CrazyTimeWheel.HotSpinsFreeSpinCount
            : 1;

        player.Balance -= price;
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.RouletteStake,
            Amount = price,
            IsFreeBonus = true,
            IsBonusPurchase = true,
            CreatedAt = DateTime.UtcNow
        });

        _cache.Set(BonusCacheKey(playerId), new PendingRouletteBonus
        {
            PlayerId = playerId,
            StakeAmount = stakeAmount,
            BonusKind = bonusKind,
            SpinsRemaining = spinsRemaining,
            CreatedAt = DateTime.UtcNow
        }, TimeSpan.FromMinutes(15));

        await _context.SaveChangesAsync();

        var message = bonusKind == RouletteBonusKind.HotSpins
            ? $"HOT SPINS куплены за {price:N2} ₽! {spinsRemaining} вращений по {stakeAmount:N0} ₽."
            : $"CRAZY TIME куплен за {price:N2} ₽! Крутите бонусное колесо (номинал {stakeAmount:N0} ₽).";

        return (true, message, new RouletteBuyBonusResult
        {
            NewBalance = player.Balance,
            PricePaid = price,
            StakeAmount = stakeAmount,
            BonusKind = bonusKind,
            BonusSpinsRemaining = spinsRemaining
        });
    }

    public Task<RoulettePendingBonusState?> GetPendingBonusAsync(string playerId)
    {
        if (_cache.TryGetValue(BonusCacheKey(playerId), out PendingRouletteBonus? pending) && pending != null)
        {
            return Task.FromResult<RoulettePendingBonusState?>(new RoulettePendingBonusState
            {
                BonusKind = pending.BonusKind,
                SpinsRemaining = pending.SpinsRemaining,
                StakeAmount = pending.StakeAmount,
                TotalBonusWinnings = pending.TotalBonusWinnings
            });
        }

        return Task.FromResult<RoulettePendingBonusState?>(null);
    }
}
