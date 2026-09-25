using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;
using SportLinea.Models.ViewModels;

namespace SportLinea.Services;

public interface IBonusService
{
    Task<string?> CheckBetMilestoneAsync(string playerId);
    Task<string?> CheckRouletteMilestoneAsync(string playerId);
    Task<string?> CheckPlinkoMilestoneAsync(string playerId);
    Task RepairRouletteMilestoneBonusesAsync();
    Task RepairPlinkoMilestoneBonusesAsync();
    Task<(bool Success, string Message)> ActivateAsync(string playerId, int bonusId);
    Task<Bonus?> GetActiveFreeBetAsync(string playerId);
    Task<Bonus?> GetActiveFreeRouletteBonusAsync(string playerId);
    Task<Bonus?> GetActiveFreePlinkoBountyAsync(string playerId);
    Task<(bool Success, string Message)> ConsumeFreeBetAsync(string playerId, int bonusId);
    Task<(bool Success, string Message, Bonus? Bonus)> ConsumeFreeRouletteSpinAsync(string playerId);
    Task<(bool Success, string Message)> ConsumeFreePlinkoBountyAsync(string playerId);
    Task<bool> AwardRegistrationBonusesAsync(string playerId);
    Task EnsureRegistrationBonusesForExistingPlayersAsync();
    Task<(bool Success, string Message)> AssignBookmakerBonusAsync(
        string playerId, BookmakerBonusKind kind, decimal freeBetAmount, int spinCount, decimal spinAmount,
        DateTime endDate, string? bookmakerComment, string bookmakerId);
}

public class BonusService : IBonusService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public BonusService(ApplicationDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<string?> CheckBetMilestoneAsync(string playerId)
    {
        var player = await _context.Users.FindAsync(playerId);
        if (player == null) return null;

        var betCount = await _context.Bets.CountAsync(b => b.PlayerId == playerId);
        var expectedTier = betCount / BonusRules.BetsPerFreeBet;

        string? awardedMessage = null;
        while (player.BetBonusTier < expectedTier)
        {
            player.BetBonusTier++;
            awardedMessage = await AwardFreeBetAsync(playerId);
        }

        await _context.SaveChangesAsync();
        return awardedMessage;
    }

    public async Task<string?> CheckRouletteMilestoneAsync(string playerId)
    {
        var player = await _context.Users.FindAsync(playerId);
        if (player == null || !CountryPolicy.AllowsCrazyTime(player.CountryOfResidence))
            return null;

        var spinCount = await _context.AccountOperations.CountAsync(o =>
            o.PlayerId == playerId &&
            o.OperationType == OperationType.RouletteStake &&
            !o.IsFreeBonus);

        var expectedTier = spinCount / BonusRules.SpinsPerRouletteBonus;
        var milestoneBonusCount = await CountRouletteMilestoneBonusesAsync(playerId);

        string? awardedMessage = null;
        while (milestoneBonusCount < expectedTier)
        {
            milestoneBonusCount++;
            awardedMessage = await AwardFreeRouletteBonusAsync(playerId);
        }

        if (player.RouletteBonusTier < expectedTier)
            player.RouletteBonusTier = expectedTier;

        await _context.SaveChangesAsync();
        return awardedMessage;
    }

    public async Task<string?> CheckPlinkoMilestoneAsync(string playerId)
    {
        var player = await _context.Users.FindAsync(playerId);
        if (player == null || !CountryPolicy.AllowsCrazyTime(player.CountryOfResidence))
            return null;

        var bountyGameCount = await _context.AccountOperations.CountAsync(o =>
            o.PlayerId == playerId &&
            o.OperationType == OperationType.PlinkoBountyGame &&
            !o.IsFreeBonus);

        var expectedTier = bountyGameCount / BonusRules.BountyGamesPerPlinkoBonus;
        var milestoneBonusCount = await CountPlinkoMilestoneBonusesAsync(playerId);

        string? awardedMessage = null;
        while (milestoneBonusCount < expectedTier)
        {
            milestoneBonusCount++;
            awardedMessage = await AwardFreePlinkoBountyAsync(playerId);
        }

        if (player.PlinkoBonusTier < expectedTier)
            player.PlinkoBonusTier = expectedTier;

        await _context.SaveChangesAsync();
        return awardedMessage;
    }

    public async Task RepairPlinkoMilestoneBonusesAsync()
    {
        var players = await _context.Users.ToListAsync();

        foreach (var player in players)
        {
            if (!CountryPolicy.AllowsCrazyTime(player.CountryOfResidence))
                continue;

            await CheckPlinkoMilestoneAsync(player.Id);
        }
    }

    public async Task RepairRouletteMilestoneBonusesAsync()
    {
        var players = await _context.Users.ToListAsync();

        foreach (var player in players)
        {
            if (!CountryPolicy.AllowsCrazyTime(player.CountryOfResidence))
                continue;

            await CheckRouletteMilestoneAsync(player.Id);
        }
    }

    public async Task<(bool Success, string Message)> ActivateAsync(string playerId, int bonusId)
    {
        var bonus = await _context.Bonuses
            .FirstOrDefaultAsync(b => b.Id == bonusId && b.PlayerId == playerId);

        if (bonus == null)
            return (false, "Бонус не найден");

        if (bonus.Status != BonusStatus.Pending)
            return (false, "Этот бонус уже активирован или использован");

        if (bonus.EndDate < DateTime.UtcNow)
        {
            bonus.Status = BonusStatus.Expired;
            await _context.SaveChangesAsync();
            return (false, "Срок действия бонуса истёк");
        }

        if (bonus.Type == BonusType.FreeBet)
        {
            if (await HasActiveBonusAsync(playerId, BonusType.FreeBet))
                return (false, "У вас уже есть активный фрибет. Сначала используйте его.");

            bonus.Status = BonusStatus.Active;
            bonus.UsesRemaining = 1;
        }
        else if (bonus.Type == BonusType.FreeRouletteSpins)
        {
            var player = await _context.Users.FindAsync(playerId);
            if (player == null || !CountryPolicy.AllowsCrazyTime(player.CountryOfResidence))
                return (false, "Crazy Time недоступен в вашей стране проживания.");

            if (await HasActiveBonusAsync(playerId, BonusType.FreeRouletteSpins))
                return (false, "У вас уже есть активные бесплатные спины. Сначала используйте их.");

            bonus.Status = BonusStatus.Active;
            bonus.UsesRemaining = bonus.UsesTotal;
        }
        else if (bonus.Type == BonusType.FreePlinkoBounty)
        {
            var player = await _context.Users.FindAsync(playerId);
            if (player == null || !CountryPolicy.AllowsCrazyTime(player.CountryOfResidence))
                return (false, "Bingo Plinko недоступен в вашей стране проживания.");

            if (await HasActiveBonusAsync(playerId, BonusType.FreePlinkoBounty))
                return (false, "У вас уже есть активная бесплатная Bounty Hunter. Сначала используйте её.");

            bonus.Status = BonusStatus.Active;
            bonus.UsesRemaining = bonus.UsesTotal;
        }
        else
        {
            return (false, "Этот бонус не требует активации");
        }

        await _context.SaveChangesAsync();

        await _notificationService.SendAsync(playerId, NotificationType.Bonus,
            BonusMessages.Activated(bonus.Description));

        return (true, "Бонус активирован! Теперь его можно использовать.");
    }

    public Task<Bonus?> GetActiveFreeBetAsync(string playerId) =>
        _context.Bonuses.FirstOrDefaultAsync(b =>
            b.PlayerId == playerId &&
            b.Type == BonusType.FreeBet &&
            b.Status == BonusStatus.Active &&
            b.UsesRemaining > 0 &&
            b.EndDate >= DateTime.UtcNow);

    public Task<Bonus?> GetActiveFreeRouletteBonusAsync(string playerId) =>
        _context.Bonuses.FirstOrDefaultAsync(b =>
            b.PlayerId == playerId &&
            b.Type == BonusType.FreeRouletteSpins &&
            b.Status == BonusStatus.Active &&
            b.UsesRemaining > 0 &&
            b.EndDate >= DateTime.UtcNow);

    public Task<Bonus?> GetActiveFreePlinkoBountyAsync(string playerId) =>
        _context.Bonuses.FirstOrDefaultAsync(b =>
            b.PlayerId == playerId &&
            b.Type == BonusType.FreePlinkoBounty &&
            b.Status == BonusStatus.Active &&
            b.UsesRemaining > 0 &&
            b.EndDate >= DateTime.UtcNow);

    public async Task<(bool Success, string Message)> ConsumeFreeBetAsync(string playerId, int bonusId)
    {
        var bonus = await GetActiveFreeBetAsync(playerId);
        if (bonus == null || bonus.Id != bonusId)
            return (false, "Активный фрибет не найден");

        bonus.UsesRemaining = 0;
        bonus.Status = BonusStatus.Used;
        await _context.SaveChangesAsync();
        return (true, "Фрибет использован");
    }

    public async Task<(bool Success, string Message, Bonus? Bonus)> ConsumeFreeRouletteSpinAsync(string playerId)
    {
        var bonus = await GetActiveFreeRouletteBonusAsync(playerId);
        if (bonus == null)
            return (false, "Нет активных бесплатных спинов", null);

        bonus.UsesRemaining--;
        if (bonus.UsesRemaining <= 0)
            bonus.Status = BonusStatus.Used;

        await _context.SaveChangesAsync();
        return (true, "Бесплатный спин использован", bonus);
    }

    public async Task<(bool Success, string Message)> ConsumeFreePlinkoBountyAsync(string playerId)
    {
        var bonus = await GetActiveFreePlinkoBountyAsync(playerId);
        if (bonus == null)
            return (false, "Нет активной бесплатной Bounty Hunter");

        bonus.UsesRemaining = 0;
        bonus.Status = BonusStatus.Used;
        await _context.SaveChangesAsync();
        return (true, "Бесплатная Bounty Hunter использована");
    }

    public async Task<bool> AwardRegistrationBonusesAsync(string playerId)
    {
        if (await HasRegistrationBonusesAsync(playerId))
            return false;

        var player = await _context.Users.FindAsync(playerId);
        if (player == null)
            return false;

        var includesCrazyTime = CountryPolicy.AllowsCrazyTime(player.CountryOfResidence);
        var endDate = DateTime.UtcNow.AddDays(BonusRules.ValidityDays);

        _context.Bonuses.Add(new Bonus
        {
            PlayerId = playerId,
            Type = BonusType.FreeBet,
            Description = $"Приветственный фрибет за регистрацию — {BonusRules.RegistrationFreeBetAmount:N0} ₽ на любое событие",
            Amount = BonusRules.RegistrationFreeBetAmount,
            UsesTotal = 1,
            UsesRemaining = 0,
            StartDate = DateTime.UtcNow,
            EndDate = endDate,
            Status = BonusStatus.Pending
        });

        if (includesCrazyTime)
        {
            _context.Bonuses.Add(new Bonus
            {
                PlayerId = playerId,
                Type = BonusType.FreeRouletteSpins,
                Description = $"Приветственные фриспины Crazy Time — {BonusRules.RegistrationFreeSpinCount} × {BonusRules.RegistrationFreeSpinAmount:N0} ₽",
                Amount = BonusRules.RegistrationFreeSpinAmount,
                UsesTotal = BonusRules.RegistrationFreeSpinCount,
                UsesRemaining = 0,
                StartDate = DateTime.UtcNow,
                EndDate = endDate,
                Status = BonusStatus.Pending
            });
        }

        await _context.SaveChangesAsync();

        await _notificationService.SendAsync(playerId, NotificationType.Bonus,
            BonusMessages.RegistrationBonusesAwarded(includesCrazyTime));

        return true;
    }

    public async Task<(bool Success, string Message)> AssignBookmakerBonusAsync(
        string playerId,
        BookmakerBonusKind kind,
        decimal freeBetAmount,
        int spinCount,
        decimal spinAmount,
        DateTime endDate,
        string? bookmakerComment,
        string bookmakerId)
    {
        var player = await _context.Users.FindAsync(playerId);
        if (player == null)
            return (false, "Игрок не найден");

        if (endDate.Date < DateTime.UtcNow.Date)
            return (false, "Дата окончания бонуса не может быть в прошлом");

        string notificationText;
        Bonus bonus;

        if (kind == BookmakerBonusKind.FreeBet)
        {
            if (freeBetAmount <= 0)
                return (false, "Укажите сумму фрибета");

            bonus = new Bonus
            {
                PlayerId = playerId,
                Type = BonusType.FreeBet,
                Description = $"Фрибет от букмекера — {freeBetAmount:N0} ₽",
                Amount = freeBetAmount,
                UsesTotal = 1,
                UsesRemaining = 0,
                StartDate = DateTime.UtcNow,
                EndDate = endDate.ToUniversalTime(),
                Status = BonusStatus.Pending,
                BookmakerComment = bookmakerComment?.Trim(),
                AwardedByUserId = bookmakerId
            };
            notificationText = BonusMessages.BookmakerFreeBetAwarded(freeBetAmount, bookmakerComment);
        }
        else
        {
            if (spinCount <= 0 || spinAmount <= 0)
                return (false, "Укажите количество и номинал фриспинов");

            if (!CountryPolicy.AllowsCrazyTime(player.CountryOfResidence))
                return (false, "Фриспины Crazy Time недоступны для резидентов России");

            bonus = new Bonus
            {
                PlayerId = playerId,
                Type = BonusType.FreeRouletteSpins,
                Description = $"Crazy Time от букмекера — {spinCount} × {spinAmount:N0} ₽",
                Amount = spinAmount,
                UsesTotal = spinCount,
                UsesRemaining = 0,
                StartDate = DateTime.UtcNow,
                EndDate = endDate.ToUniversalTime(),
                Status = BonusStatus.Pending,
                BookmakerComment = bookmakerComment?.Trim(),
                AwardedByUserId = bookmakerId
            };
            notificationText = BonusMessages.BookmakerFreeSpinsAwarded(spinCount, spinAmount, bookmakerComment);
        }

        _context.Bonuses.Add(bonus);
        await _context.SaveChangesAsync();
        await _notificationService.SendAsync(playerId, NotificationType.Bonus, notificationText);

        return (true, "Бонус назначен, игрок получил уведомление");
    }

    public async Task EnsureRegistrationBonusesForExistingPlayersAsync()
    {
        var playerIds = await (
            from user in _context.Users
            join userRole in _context.UserRoles on user.Id equals userRole.UserId
            join role in _context.Roles on userRole.RoleId equals role.Id
            where role.Name == Roles.Player
            select user.Id).Distinct().ToListAsync();

        foreach (var playerId in playerIds)
            await AwardRegistrationBonusesAsync(playerId);
    }

    private Task<bool> HasRegistrationBonusesAsync(string playerId) =>
        _context.Bonuses.AnyAsync(b =>
            b.PlayerId == playerId &&
            b.Description.StartsWith(BonusRules.RegistrationBonusMarker));

    private async Task<string> AwardFreeBetAsync(string playerId)
    {
        _context.Bonuses.Add(new Bonus
        {
            PlayerId = playerId,
            Type = BonusType.FreeBet,
            Description = $"Фрибет за {BonusRules.BetsPerFreeBet} ставок — {BonusRules.FreeBetAmount:N0} ₽ на любое событие",
            Amount = BonusRules.FreeBetAmount,
            UsesTotal = 1,
            UsesRemaining = 0,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(BonusRules.ValidityDays),
            Status = BonusStatus.Pending
        });

        var message = BonusMessages.NewFreeBetAwarded();
        await _notificationService.SendAsync(playerId, NotificationType.Bonus, message);
        return message;
    }

    private async Task<string> AwardFreeRouletteBonusAsync(string playerId)
    {
        _context.Bonuses.Add(new Bonus
        {
            PlayerId = playerId,
            Type = BonusType.FreeRouletteSpins,
            Description = $"Crazy Time: {BonusRules.FreeRouletteSpinCount} бесплатных спинов по {BonusRules.FreeRouletteSpinAmount:N0} ₽",
            Amount = BonusRules.FreeRouletteSpinAmount,
            UsesTotal = BonusRules.FreeRouletteSpinCount,
            UsesRemaining = 0,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(BonusRules.ValidityDays),
            Status = BonusStatus.Pending
        });

        var message = BonusMessages.NewRouletteSpinsAwarded();
        await _notificationService.SendAsync(playerId, NotificationType.Bonus, message);
        return message;
    }

    private async Task<string> AwardFreePlinkoBountyAsync(string playerId)
    {
        _context.Bonuses.Add(new Bonus
        {
            PlayerId = playerId,
            Type = BonusType.FreePlinkoBounty,
            Description =
                $"{BonusRules.PlinkoMilestoneMarker} бесплатная Bounty Hunter по {BonusRules.FreePlinkoBountyStake:N0} ₽",
            Amount = BonusRules.FreePlinkoBountyStake,
            UsesTotal = 1,
            UsesRemaining = 0,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(BonusRules.ValidityDays),
            Status = BonusStatus.Pending
        });

        var message = BonusMessages.NewPlinkoBountyAwarded();
        await _notificationService.SendAsync(playerId, NotificationType.Bonus, message);
        return message;
    }

    private Task<int> CountRouletteMilestoneBonusesAsync(string playerId) =>
        _context.Bonuses.CountAsync(b =>
            b.PlayerId == playerId &&
            b.Type == BonusType.FreeRouletteSpins &&
            b.Description.StartsWith(BonusRules.RouletteMilestoneMarker));

    private Task<int> CountPlinkoMilestoneBonusesAsync(string playerId) =>
        _context.Bonuses.CountAsync(b =>
            b.PlayerId == playerId &&
            b.Type == BonusType.FreePlinkoBounty &&
            b.Description.StartsWith(BonusRules.PlinkoMilestoneMarker));

    private Task<bool> HasActiveBonusAsync(string playerId, BonusType type) =>
        _context.Bonuses.AnyAsync(b =>
            b.PlayerId == playerId &&
            b.Type == type &&
            b.Status == BonusStatus.Active &&
            b.UsesRemaining > 0 &&
            b.EndDate >= DateTime.UtcNow);
}
