using Microsoft.Extensions.Caching.Memory;
using SportLinea.Data;
using SportLinea.Models;

namespace SportLinea.Services;

public interface IFruitland1000Service
{
    Task<(bool Success, string Message, FruitlandSpinResponse? Result)> SpinAsync(string playerId, decimal amount);
    Task<(bool Success, string Message, FruitlandSpinResponse? Result)> BuyBonusAsync(string playerId, decimal amount, int spins);
    Task CompleteAnimationAsync(string playerId);
}

public class FruitlandPendingSession
{
    public string? NotificationMessage { get; set; }
}

public class FruitlandSpinResponse
{
    public decimal NewBalance { get; set; }
    public decimal TotalWinnings { get; set; }
    public List<FruitlandPhaseResult> Phases { get; set; } = new();
}

public class Fruitland1000Service : IFruitland1000Service
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IMemoryCache _cache;

    public Fruitland1000Service(
        ApplicationDbContext context,
        INotificationService notificationService,
        IMemoryCache cache)
    {
        _context = context;
        _notificationService = notificationService;
        _cache = cache;
    }

    private static string CacheKey(string playerId) => $"fruitland-1000-{playerId}";

    private void LogSpin(string playerId, FruitlandSessionResult session, FruitlandSpinKind spinKind)
    {
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.FruitlandSpin,
            FruitlandSpinKind = spinKind,
            FruitlandGlobalMultiplier = session.GetMaxGlobalMultiplier() > 0 ? session.GetMaxGlobalMultiplier() : null,
            RouletteSpinWon = session.IsSuccessful(spinKind == FruitlandSpinKind.BonusBuy),
            CreatedAt = DateTime.UtcNow
        });
    }

    private void LogWins(string playerId, FruitlandSessionResult session)
    {
        foreach (var phase in session.Phases)
        {
            if (phase.Winnings <= 0)
                continue;

            _context.AccountOperations.Add(new AccountOperation
            {
                PlayerId = playerId,
                OperationType = OperationType.FruitlandWin,
                Amount = phase.Winnings,
                FruitlandSpinKind = phase.Phase == "bonus" ? FruitlandSpinKind.Bonus : FruitlandSpinKind.Main,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    public async Task<(bool Success, string Message, FruitlandSpinResponse? Result)> SpinAsync(string playerId, decimal amount)
    {
        if (!Fruitland1000Config.IsAllowedStake(amount))
            return (false, "Выберите ставку из предложенных вариантов", null);

        if (_cache.TryGetValue(CacheKey(playerId), out _))
            return (false, "Дождитесь завершения анимации текущего спина", null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null || player.Status == UserStatus.Blocked)
            return (false, "Аккаунт недоступен", null);

        if (player.Balance < amount)
            return (false, "Недостаточно средств на счёте", null);

        player.Balance -= amount;
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.FruitlandStake,
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        });

        var session = Fruitland1000Config.RunSession(amount, FruitlandSessionMode.Normal);
        if (session.TotalWinnings > 0)
        {
            player.Balance += session.TotalWinnings;
            LogWins(playerId, session);
        }

        LogSpin(playerId, session, FruitlandSpinKind.Main);

        var message = session.TotalWinnings > 0
            ? $"Выигрыш {session.TotalWinnings:N2} ₽!"
            : "Сыгровки нет.";

        if (session.Phases.Any(p => p.TriggersBonus))
            message += $" Бонус Fruitland — {session.BonusFreeSpins} бесплатных спинов!";

        _cache.Set(CacheKey(playerId), new FruitlandPendingSession
        {
            NotificationMessage = $"Fruitland 1000: {message}"
        }, TimeSpan.FromMinutes(3));

        await _context.SaveChangesAsync();

        return (true, message, new FruitlandSpinResponse
        {
            NewBalance = player.Balance,
            TotalWinnings = session.TotalWinnings,
            Phases = session.Phases
        });
    }

    public async Task<(bool Success, string Message, FruitlandSpinResponse? Result)> BuyBonusAsync(
        string playerId, decimal amount, int spins)
    {
        if (!Fruitland1000Config.IsAllowedStake(amount))
            return (false, "Выберите ставку из предложенных вариантов", null);

        if (spins is not (10 or 15 or 20))
            return (false, "Недопустимый вариант покупки бонуса", null);

        if (_cache.TryGetValue(CacheKey(playerId), out _))
            return (false, "Дождитесь завершения анимации текущего спина", null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null || player.Status == UserStatus.Blocked)
            return (false, "Аккаунт недоступен", null);

        var price = Fruitland1000Config.GetBonusBuyPrice(amount, spins);
        var priceMult = spins switch
        {
            10 => Fruitland1000Config.BonusBuy10Multiplier,
            15 => Fruitland1000Config.BonusBuy15Multiplier,
            _ => Fruitland1000Config.BonusBuy20Multiplier
        };

        if (player.Balance < price)
            return (false, $"Недостаточно средств. Нужно {price:N2} ₽ (ставка × {priceMult})", null);

        player.Balance -= price;
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.FruitlandStake,
            Amount = price,
            IsBonusPurchase = true,
            CreatedAt = DateTime.UtcNow
        });

        var mode = spins switch
        {
            10 => FruitlandSessionMode.BonusBuy10,
            15 => FruitlandSessionMode.BonusBuy15,
            _ => FruitlandSessionMode.BonusBuy20
        };

        var session = Fruitland1000Config.RunSession(amount, mode);
        if (session.TotalWinnings > 0)
        {
            player.Balance += session.TotalWinnings;
            LogWins(playerId, session);
        }

        LogSpin(playerId, session, FruitlandSpinKind.BonusBuy);

        var message = session.TotalWinnings > 0
            ? $"Бонус Fruitland! Выигрыш {session.TotalWinnings:N2} ₽"
            : "Бонус завершён без выигрыша.";

        _cache.Set(CacheKey(playerId), new FruitlandPendingSession
        {
            NotificationMessage = $"Fruitland 1000: {message}"
        }, TimeSpan.FromMinutes(4));

        await _context.SaveChangesAsync();

        return (true, message, new FruitlandSpinResponse
        {
            NewBalance = player.Balance,
            TotalWinnings = session.TotalWinnings,
            Phases = session.Phases
        });
    }

    public async Task CompleteAnimationAsync(string playerId)
    {
        if (!_cache.TryGetValue<FruitlandPendingSession>(CacheKey(playerId), out var pending))
            return;

        _cache.Remove(CacheKey(playerId));

        if (!string.IsNullOrWhiteSpace(pending?.NotificationMessage))
            await _notificationService.SendAsync(playerId, NotificationType.Info, pending.NotificationMessage);
    }
}
