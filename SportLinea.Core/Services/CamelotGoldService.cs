using Microsoft.Extensions.Caching.Memory;

using SportLinea.Data;

using SportLinea.Models;



namespace SportLinea.Services;



public interface ICamelotGoldService

{

    Task<(bool Success, string Message, CamelotSpinResponse? Result)> SpinAsync(string playerId, decimal amount);

    Task<(bool Success, string Message, CamelotSpinResponse? Result)> BuyBonusAsync(string playerId, decimal amount);

    Task<(bool Success, string Message, CamelotSpinResponse? Result)> BuyFeatureAsync(string playerId, decimal amount);

    Task CompleteAnimationAsync(string playerId);

}



public class CamelotPendingSession

{

    public string? NotificationMessage { get; set; }

}



public class CamelotSpinResponse

{

    public decimal NewBalance { get; set; }

    public decimal TotalWinnings { get; set; }

    public List<CamelotPhaseResult> Phases { get; set; } = new();

}



public class CamelotGoldService : ICamelotGoldService

{

    private readonly ApplicationDbContext _context;

    private readonly INotificationService _notificationService;

    private readonly IMemoryCache _cache;



    public CamelotGoldService(

        ApplicationDbContext context,

        INotificationService notificationService,

        IMemoryCache cache)

    {

        _context = context;

        _notificationService = notificationService;

        _cache = cache;

    }



    private static string CacheKey(string playerId) => $"camelot-gold-{playerId}";



    private void LogCamelotSpin(string playerId, CamelotSpinResult session, CamelotSpinKind spinKind)

    {

        var maxCollector = session.GetMaxCollectorNominal();

        _context.AccountOperations.Add(new AccountOperation

        {

            PlayerId = playerId,

            OperationType = OperationType.CamelotSpin,

            CamelotSpinKind = spinKind,

            CamelotCollectorNominal = maxCollector > 0 ? maxCollector : null,

            RouletteSpinWon = session.IsSuccessful(spinKind is CamelotSpinKind.BonusBuy),

            CreatedAt = DateTime.UtcNow

        });

    }



    private void LogCamelotWins(string playerId, CamelotSpinResult session)

    {

        foreach (var phase in session.Phases)

        {

            if (phase.Winnings <= 0)

                continue;



            _context.AccountOperations.Add(new AccountOperation

            {

                PlayerId = playerId,

                OperationType = OperationType.CamelotWin,

                Amount = phase.Winnings,

                CamelotSpinKind = phase.Phase == "bonus" ? CamelotSpinKind.Bonus : CamelotSpinKind.Main,

                CreatedAt = DateTime.UtcNow

            });

        }

    }



    public async Task<(bool Success, string Message, CamelotSpinResponse? Result)> SpinAsync(string playerId, decimal amount)

    {

        if (!CamelotGoldConfig.IsAllowedStake(amount))

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

            OperationType = OperationType.CamelotStake,

            Amount = amount,

            CreatedAt = DateTime.UtcNow

        });



        var session = CamelotGoldConfig.RunSession(amount, bonusBuy: false);

        if (session.TotalWinnings > 0)

        {

            player.Balance += session.TotalWinnings;

            LogCamelotWins(playerId, session);

        }



        LogCamelotSpin(playerId, session, CamelotSpinKind.Main);



        var message = session.TotalWinnings > 0

            ? $"Выигрыш {session.TotalWinnings:N2} ₽!"

            : "Сыгровки нет.";



        if (session.Phases.Any(p => p.TriggersBonus))

            message += " Запущен бонус Camelot!";



        _cache.Set(CacheKey(playerId), new CamelotPendingSession

        {

            NotificationMessage = $"Camelot's Gold: {message}"

        }, TimeSpan.FromMinutes(2));

        await _context.SaveChangesAsync();



        return (true, message, new CamelotSpinResponse

        {

            NewBalance = player.Balance,

            TotalWinnings = session.TotalWinnings,

            Phases = session.Phases

        });

    }



    public async Task<(bool Success, string Message, CamelotSpinResponse? Result)> BuyBonusAsync(string playerId, decimal amount)

    {

        if (!CamelotGoldConfig.IsAllowedStake(amount))

            return (false, "Выберите ставку из предложенных вариантов", null);



        if (_cache.TryGetValue(CacheKey(playerId), out _))

            return (false, "Дождитесь завершения анимации текущего спина", null);



        var player = await _context.Users.FindAsync(playerId);

        if (player == null || player.Status == UserStatus.Blocked)

            return (false, "Аккаунт недоступен", null);



        var price = CamelotGoldConfig.GetBonusBuyPrice(amount);

        if (player.Balance < price)

            return (false, $"Недостаточно средств. Нужно {price:N2} ₽ (ставка × {CamelotGoldConfig.BonusBuyMultiplier})", null);



        player.Balance -= price;

        _context.AccountOperations.Add(new AccountOperation

        {

            PlayerId = playerId,

            OperationType = OperationType.CamelotStake,

            Amount = price,

            IsBonusPurchase = true,

            CreatedAt = DateTime.UtcNow

        });



        var session = CamelotGoldConfig.RunSession(amount, bonusBuy: true);

        if (session.TotalWinnings > 0)

        {

            player.Balance += session.TotalWinnings;

            LogCamelotWins(playerId, session);

        }



        LogCamelotSpin(playerId, session, CamelotSpinKind.BonusBuy);



        var message = session.TotalWinnings > 0

            ? $"Бонус Camelot! Выигрыш {session.TotalWinnings:N2} ₽"

            : "Бонус завершён без выигрыша.";



        _cache.Set(CacheKey(playerId), new CamelotPendingSession

        {

            NotificationMessage = $"Camelot's Gold: {message}"

        }, TimeSpan.FromMinutes(3));

        await _context.SaveChangesAsync();



        return (true, message, new CamelotSpinResponse

        {

            NewBalance = player.Balance,

            TotalWinnings = session.TotalWinnings,

            Phases = session.Phases

        });

    }



    public async Task<(bool Success, string Message, CamelotSpinResponse? Result)> BuyFeatureAsync(string playerId, decimal amount)

    {

        if (!CamelotGoldConfig.IsAllowedStake(amount))

            return (false, "Выберите ставку из предложенных вариантов", null);



        if (_cache.TryGetValue(CacheKey(playerId), out _))

            return (false, "Дождитесь завершения анимации текущего спина", null);



        var player = await _context.Users.FindAsync(playerId);

        if (player == null || player.Status == UserStatus.Blocked)

            return (false, "Аккаунт недоступен", null);



        var price = CamelotGoldConfig.GetFeatureBuyPrice(amount);

        if (player.Balance < price)

            return (false, $"Недостаточно средств. Нужно {price:N2} ₽ (ставка × {CamelotGoldConfig.FeatureBuyMultiplier})", null);



        player.Balance -= price;

        _context.AccountOperations.Add(new AccountOperation

        {

            PlayerId = playerId,

            OperationType = OperationType.CamelotStake,

            Amount = price,

            IsBonusPurchase = true,

            CreatedAt = DateTime.UtcNow

        });



        var session = CamelotGoldConfig.RunSession(amount, bonusBuy: false, featureBuy: true);

        if (session.TotalWinnings > 0)

        {

            player.Balance += session.TotalWinnings;

            LogCamelotWins(playerId, session);

        }



        LogCamelotSpin(playerId, session, CamelotSpinKind.FeatureBuy);



        var message = session.TotalWinnings > 0

            ? $"Выигрыш {session.TotalWinnings:N2} ₽!"

            : "Сыгровки нет.";



        if (session.Phases.Any(p => p.TriggersBonus))

            message += " Запущен бонус Camelot!";



        _cache.Set(CacheKey(playerId), new CamelotPendingSession

        {

            NotificationMessage = $"Camelot's Gold: {message}"

        }, TimeSpan.FromMinutes(2));

        await _context.SaveChangesAsync();



        return (true, message, new CamelotSpinResponse

        {

            NewBalance = player.Balance,

            TotalWinnings = session.TotalWinnings,

            Phases = session.Phases

        });

    }



    public async Task CompleteAnimationAsync(string playerId)

    {

        if (!_cache.TryGetValue<CamelotPendingSession>(CacheKey(playerId), out var pending))

            return;



        _cache.Remove(CacheKey(playerId));



        if (!string.IsNullOrWhiteSpace(pending?.NotificationMessage))

            await _notificationService.SendAsync(playerId, NotificationType.Info, pending.NotificationMessage);

    }

}


