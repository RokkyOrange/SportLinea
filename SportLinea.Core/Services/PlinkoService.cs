using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SportLinea.Data;
using SportLinea.Models;

namespace SportLinea.Services;

public interface IPlinkoService
{
    Task<(bool Success, string Message, PlinkoSpinResult? Result)> SpinAsync(string playerId, decimal amount);
    Task<(bool Success, string Message, PlinkoPickResult? Result)> PickTileAsync(string playerId, int tileIndex);
    Task<(bool Success, string Message, PlinkoBountyShootResult? Result)> BountyShootAsync(string playerId, int targetIndex);
    Task<(bool Success, string Message, PlinkoBuyBountyResult? Result)> BuyBountyAsync(
        string playerId, decimal stakeAmount, bool useFreeBounty = false);
}

public class PlinkoSpinResult
{
    public decimal NewBalance { get; set; }
    public bool BonusHunt { get; set; }
    public int PicksRemaining { get; set; }
}

public class PlinkoPickResult
{
    public int TileIndex { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string SymbolKey { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public int PicksRemaining { get; set; }
    public bool RoundComplete { get; set; }
    public decimal Winnings { get; set; }
    public decimal NewBalance { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool BountyTriggered { get; set; }
    public int BountySpinsRemaining { get; set; }
    public int BountyLevel { get; set; }
    public string? BonusAwardMessage { get; set; }
}

public class PlinkoBountyShootResult
{
    public int TargetIndex { get; set; }
    public string OutcomeLabel { get; set; } = string.Empty;
    public bool IsBullet { get; set; }
    public int BulletsGained { get; set; }
    public int BulletsOnLevel { get; set; }
    public int BulletsRequired { get; set; }
    public int TotalMultiplierSum { get; set; }
    public int SpinsRemaining { get; set; }
    public int Level { get; set; }
    public bool LevelAdvanced { get; set; }
    public int BonusSpinsGained { get; set; }
    public bool BonusComplete { get; set; }
    public decimal Winnings { get; set; }
    public decimal NewBalance { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PlinkoBuyBountyResult
{
    public decimal NewBalance { get; set; }
    public decimal PricePaid { get; set; }
    public int BountySpinsRemaining { get; set; }
    public int BountyLevel { get; set; }
    public string? BonusAwardMessage { get; set; }
    public bool FreePlinkoBountyUsed { get; set; }
}

public class PlinkoService : IPlinkoService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IMemoryCache _cache;
    private readonly IBonusService _bonusService;

    public PlinkoService(
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

    private static string CacheKey(string playerId) => $"plinko-wildwest-{playerId}";

    private void LogPlinkoSpin(string playerId, PlinkoSpinKind kind, bool won)
    {
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.PlinkoSpin,
            PlinkoSpinKind = kind,
            RouletteSpinWon = won,
            CreatedAt = DateTime.UtcNow
        });
    }

    private void LogPlinkoBountyGame(string playerId, bool isFreeBonus)
    {
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.PlinkoBountyGame,
            IsFreeBonus = isFreeBonus,
            CreatedAt = DateTime.UtcNow
        });
    }

    private void AddPlinkoWin(string playerId, decimal amount, PlinkoSpinKind kind, int? bountyMultiplier = null)
    {
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.PlinkoWin,
            Amount = amount,
            PlinkoSpinKind = kind,
            PlinkoBountyMultiplier = bountyMultiplier,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<(bool Success, string Message, PlinkoSpinResult? Result)> SpinAsync(string playerId, decimal amount)
    {
        if (!PlinkoWildWestConfig.IsAllowedStake(amount))
            return (false, "Выберите ставку из предложенных вариантов", null);

        if (_cache.TryGetValue(CacheKey(playerId), out PendingPlinkoGame? existing) && existing != null)
            return (false, "Сначала завершите текущий раунд или бонус Bounty Hunter!", null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null || player.Status == UserStatus.Blocked)
            return (false, "Аккаунт недоступен", null);

        if (player.Balance < amount)
            return (false, "Недостаточно средств на счёте", null);

        var bonusHunt = PlinkoWildWestConfig.RollBonusHunt();
        var board = PlinkoWildWestConfig.BuildBoard(bonusHunt);

        player.Balance -= amount;
        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.PlinkoStake,
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        });

        var pending = new PendingPlinkoGame
        {
            PlayerId = playerId,
            StakeAmount = amount,
            BonusHunt = bonusHunt,
            Board = board,
            Phase = PlinkoGamePhase.AwaitingPicks
        };

        _cache.Set(CacheKey(playerId), pending, TimeSpan.FromMinutes(20));
        await _context.SaveChangesAsync();

        return (true, bonusHunt
            ? "Bonus Hunt! На поле спрятаны звёзды шерифа — откройте 3, чтобы запустить Bounty Hunter!"
            : "Выберите 3 фишки!", new PlinkoSpinResult
        {
            NewBalance = player.Balance,
            BonusHunt = bonusHunt,
            PicksRemaining = PlinkoWildWestConfig.PicksRequired
        });
    }

    public async Task<(bool Success, string Message, PlinkoPickResult? Result)> PickTileAsync(string playerId, int tileIndex)
    {
        if (!_cache.TryGetValue(CacheKey(playerId), out PendingPlinkoGame? pending) || pending == null)
            return (false, "Нет активного раунда. Сделайте спин.", null);

        if (pending.Phase != PlinkoGamePhase.AwaitingPicks)
            return (false, "Сейчас активен бонус Bounty Hunter", null);

        if (tileIndex < 0 || tileIndex >= PlinkoWildWestConfig.TileCount)
            return (false, "Некорректная фишка", null);

        if (pending.PickedIndices.Contains(tileIndex))
            return (false, "Эта фишка уже открыта", null);

        pending.PickedIndices.Add(tileIndex);
        var symbol = pending.Board[tileIndex];
        var picksLeft = PlinkoWildWestConfig.PicksRequired - pending.PickedIndices.Count;
        var roundComplete = picksLeft <= 0;

        decimal winnings = 0;
        var bountyTriggered = false;
        var message = FormatSymbolMessage(symbol);
        string? bonusAwardMessage = null;
        var player = await _context.Users.FindAsync(playerId);
        if (player == null)
            return (false, "Аккаунт не найден", null);

        if (roundComplete)
        {
            var picks = pending.PickedIndices.Select(i => pending.Board[i]).ToArray();

            if (PlinkoWildWestConfig.TriggersBountyHunter(picks, pending.BonusHunt))
            {
                bountyTriggered = true;
                StartBountyHunter(pending, purchased: false);
                LogPlinkoBountyGame(playerId, isFreeBonus: false);
                LogPlinkoSpin(playerId, PlinkoSpinKind.Main, won: true);
                message = "⭐ Bounty Hunter! Соберите 6 пуль, чтобы перейти на следующий уровень.";
            }
            else
            {
                winnings = PlinkoWildWestConfig.EvaluateMainPayout(picks, pending.StakeAmount);
                LogPlinkoSpin(playerId, PlinkoSpinKind.Main, won: winnings > 0);

                if (winnings > 0)
                {
                    player.Balance += winnings;
                    AddPlinkoWin(playerId, winnings, PlinkoSpinKind.Main);
                    message = $"Выигрыш {winnings:N2} ₽! {DescribePicks(picks)}";
                }
                else
                {
                    message = $"Сыгровки нет. {DescribePicks(picks)}";
                }

                _cache.Remove(CacheKey(playerId));
                await _notificationService.SendAsync(playerId, NotificationType.Info,
                    $"Bingo Plinko: {message}");
            }
        }

        if (!roundComplete || bountyTriggered)
            _cache.Set(CacheKey(playerId), pending, TimeSpan.FromMinutes(20));

        await _context.SaveChangesAsync();

        if (bountyTriggered)
            bonusAwardMessage = await _bonusService.CheckPlinkoMilestoneAsync(playerId);

        await _context.SaveChangesAsync();

        return (true, message, new PlinkoPickResult
        {
            TileIndex = tileIndex,
            Symbol = PlinkoWildWestConfig.GetSymbolLabel(symbol),
            SymbolKey = PlinkoWildWestConfig.GetSymbolKey(symbol),
            Emoji = PlinkoWildWestConfig.GetSymbolEmoji(symbol),
            PicksRemaining = bountyTriggered ? 0 : picksLeft,
            RoundComplete = roundComplete,
            Winnings = winnings,
            NewBalance = player.Balance,
            Message = message,
            BountyTriggered = bountyTriggered,
            BountySpinsRemaining = bountyTriggered ? pending.SpinsRemaining : 0,
            BountyLevel = bountyTriggered ? pending.BountyLevel : 0,
            BonusAwardMessage = bonusAwardMessage
        });
    }

    public async Task<(bool Success, string Message, PlinkoBountyShootResult? Result)> BountyShootAsync(
        string playerId, int targetIndex)
    {
        if (!_cache.TryGetValue(CacheKey(playerId), out PendingPlinkoGame? pending) || pending == null)
            return (false, "Нет активного бонуса", null);

        if (pending.Phase != PlinkoGamePhase.BountyHunter)
            return (false, "Bounty Hunter не активен", null);

        if (targetIndex < 0 || targetIndex >= PlinkoWildWestConfig.BountyPlates)
            return (false, "Выберите тарелку", null);

        if (pending.SpinsRemaining <= 0)
            return (false, "Бесплатные выстрелы закончились", null);

        if (pending.CurrentPlates == null)
            pending.CurrentPlates = PlinkoWildWestConfig.BuildBountyPlates(pending.BountyLevel);

        if (pending.RevealedPlates.Contains(targetIndex))
            return (false, "По этой тарелке уже стреляли в этом раунде", null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null)
            return (false, "Аккаунт не найден", null);

        var outcome = pending.CurrentPlates[targetIndex];
        pending.RevealedPlates.Add(targetIndex);
        pending.SpinsRemaining--;

        var levelAdvanced = false;
        var bonusSpinsGained = 0;
        var bonusComplete = false;
        decimal winnings = 0;
        string outcomeLabel;

        if (outcome.Kind == BountyPlateKind.Bullet)
        {
            pending.BulletsOnLevel += outcome.Bullets;
            outcomeLabel = $"Пуля +{outcome.Bullets}";
        }
        else
        {
            pending.TotalMultiplierSum += outcome.Multiplier;
            outcomeLabel = outcome.Multiplier == 0 ? "×0" : $"×{outcome.Multiplier}";
        }

        var shotWon = outcome.Kind == BountyPlateKind.Bullet || outcome.Multiplier > 0;
        LogPlinkoSpin(playerId, PlinkoSpinKind.BountyHunter, shotWon);

        if (pending.BulletsOnLevel >= PlinkoWildWestConfig.BulletsToAdvance && pending.BountyLevel < 3)
        {
            levelAdvanced = true;
            bonusSpinsGained = PlinkoWildWestConfig.SpinsOnLevelUp;
            pending.BountyLevel++;
            pending.BulletsOnLevel = 0;
            pending.SpinsRemaining += bonusSpinsGained;

            if (pending.BountyLevel == 3)
            {
                pending.SpinsRemaining += PlinkoWildWestConfig.FinalLevelBonusSpins;
                bonusSpinsGained += PlinkoWildWestConfig.FinalLevelBonusSpins;
            }

            pending.CurrentPlates = PlinkoWildWestConfig.BuildBountyPlates(pending.BountyLevel);
            pending.RevealedPlates.Clear();
        }

        if (pending.RevealedPlates.Count >= PlinkoWildWestConfig.BountyPlates && !levelAdvanced)
        {
            pending.CurrentPlates = PlinkoWildWestConfig.BuildBountyPlates(pending.BountyLevel);
            pending.RevealedPlates.Clear();
        }

        if (pending.SpinsRemaining <= 0)
            bonusComplete = true;

        if (pending.BountyLevel >= 3 && pending.BulletsOnLevel >= PlinkoWildWestConfig.BulletsToAdvance)
            bonusComplete = true;

        if (bonusComplete)
        {
            winnings = Math.Round(pending.StakeAmount * pending.TotalMultiplierSum, 2);
            if (winnings > 0)
                player.Balance += winnings;

            if (winnings > 0 || pending.TotalMultiplierSum > 0)
                AddPlinkoWin(playerId, winnings, PlinkoSpinKind.BountyHunter, pending.TotalMultiplierSum);

            _cache.Remove(CacheKey(playerId));
            await _notificationService.SendAsync(playerId, NotificationType.Info,
                $"Bounty Hunter: множитель {pending.TotalMultiplierSum}, выигрыш {winnings:N2} ₽");
        }
        else
        {
            _cache.Set(CacheKey(playerId), pending, TimeSpan.FromMinutes(20));
        }

        await _context.SaveChangesAsync();

        var message = bonusComplete
            ? $"Бонус завершён! Множитель: {pending.TotalMultiplierSum}. Выигрыш {winnings:N2} ₽"
            : levelAdvanced
                ? "Поздравляем, вы перешли на следующий уровень! Вы получили ещё бонусных спинов."
                : outcomeLabel;

        return (true, message, new PlinkoBountyShootResult
        {
            TargetIndex = targetIndex,
            OutcomeLabel = outcomeLabel,
            IsBullet = outcome.Kind == BountyPlateKind.Bullet,
            BulletsGained = outcome.Kind == BountyPlateKind.Bullet ? outcome.Bullets : 0,
            BulletsOnLevel = pending.BulletsOnLevel,
            BulletsRequired = PlinkoWildWestConfig.BulletsToAdvance,
            TotalMultiplierSum = pending.TotalMultiplierSum,
            SpinsRemaining = bonusComplete ? 0 : pending.SpinsRemaining,
            Level = pending.BountyLevel,
            LevelAdvanced = levelAdvanced,
            BonusSpinsGained = bonusSpinsGained,
            BonusComplete = bonusComplete,
            Winnings = winnings,
            NewBalance = player.Balance,
            Message = message
        });
    }

    public async Task<(bool Success, string Message, PlinkoBuyBountyResult? Result)> BuyBountyAsync(
        string playerId, decimal stakeAmount, bool useFreeBounty = false)
    {
        if (useFreeBounty)
        {
            var freeBonus = await _bonusService.GetActiveFreePlinkoBountyAsync(playerId);
            if (freeBonus == null)
                return (false, "Нет активной бесплатной Bounty Hunter. Активируйте бонус в профиле.", null);

            stakeAmount = freeBonus.Amount;
        }
        else if (!PlinkoWildWestConfig.IsAllowedStake(stakeAmount))
        {
            return (false, "Выберите ставку из предложенных вариантов", null);
        }

        if (_cache.TryGetValue(CacheKey(playerId), out PendingPlinkoGame? existing) && existing != null)
            return (false, "Сначала завершите текущий раунд или бонус Bounty Hunter!", null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null || player.Status == UserStatus.Blocked)
            return (false, "Аккаунт недоступен", null);

        var price = useFreeBounty ? 0 : PlinkoWildWestConfig.GetBountyBuyPrice(stakeAmount);
        if (!useFreeBounty && player.Balance < price)
            return (false, $"Недостаточно средств. Нужно {price:N2} ₽ (ставка × {PlinkoWildWestConfig.BountyBuyPriceMultiplier})", null);

        if (!useFreeBounty)
            player.Balance -= price;

        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.PlinkoStake,
            Amount = useFreeBounty ? stakeAmount : price,
            IsFreeBonus = useFreeBounty,
            IsBonusPurchase = !useFreeBounty,
            CreatedAt = DateTime.UtcNow
        });

        var pending = new PendingPlinkoGame
        {
            PlayerId = playerId,
            StakeAmount = stakeAmount,
            Board = Array.Empty<PlinkoSymbol>(),
            Phase = PlinkoGamePhase.BountyHunter
        };
        StartBountyHunter(pending, purchased: true);
        LogPlinkoBountyGame(playerId, isFreeBonus: useFreeBounty);

        _cache.Set(CacheKey(playerId), pending, TimeSpan.FromMinutes(20));
        await _context.SaveChangesAsync();

        if (useFreeBounty)
            await _bonusService.ConsumeFreePlinkoBountyAsync(playerId);

        string? bonusAwardMessage = null;
        if (!useFreeBounty)
            bonusAwardMessage = await _bonusService.CheckPlinkoMilestoneAsync(playerId);

        var message = useFreeBounty
            ? $"Бесплатная Bounty Hunter! {pending.SpinsRemaining} выстрелов (номинал {stakeAmount:N0} ₽)."
            : $"Bounty Hunter куплен за {price:N2} ₽! {pending.SpinsRemaining} выстрелов (номинал {stakeAmount:N0} ₽).";

        return (true, message, new PlinkoBuyBountyResult
        {
            NewBalance = player.Balance,
            PricePaid = price,
            BountySpinsRemaining = pending.SpinsRemaining,
            BountyLevel = pending.BountyLevel,
            BonusAwardMessage = bonusAwardMessage,
            FreePlinkoBountyUsed = useFreeBounty
        });
    }

    private static void StartBountyHunter(PendingPlinkoGame pending, bool purchased)
    {
        pending.Phase = PlinkoGamePhase.BountyHunter;
        pending.BountyLevel = 1;
        pending.SpinsRemaining = purchased
            ? PlinkoWildWestConfig.PurchasedBountyLevel1Spins
            : PlinkoWildWestConfig.Level1Spins;
        pending.BulletsOnLevel = 0;
        pending.TotalMultiplierSum = 0;
        pending.BountyPurchased = purchased;
        pending.CurrentPlates = PlinkoWildWestConfig.BuildBountyPlates(1);
        pending.RevealedPlates.Clear();
    }

    private static string FormatSymbolMessage(PlinkoSymbol symbol)
    {
        var emoji = PlinkoWildWestConfig.GetSymbolEmoji(symbol);
        var label = PlinkoWildWestConfig.GetSymbolLabel(symbol);
        return string.IsNullOrEmpty(emoji) ? label : $"{emoji} {label}";
    }

    private static string DescribePicks(PlinkoSymbol[] picks) =>
        string.Join(", ", picks.Select(PlinkoWildWestConfig.GetSymbolLabel));
}
