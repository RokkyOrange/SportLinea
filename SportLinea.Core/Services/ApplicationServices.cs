using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;

namespace SportLinea.Services;

public interface IActionLogService
{
    Task LogAsync(string? userId, string action, string? ipAddress);
}

public class ActionLogService : IActionLogService
{
    private readonly ApplicationDbContext _context;

    public ActionLogService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string? userId, string action, string? ipAddress)
    {
        _context.ActionLogs.Add(new ActionLog
        {
            UserId = userId,
            Action = action,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }
}

public interface INotificationService
{
    Task SendAsync(string userId, NotificationType type, string text);
    Task SendEmailAsync(string email, string subject, string body);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SendAsync(string userId, NotificationType type, string text)
    {
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Text = text,
            SentAt = DateTime.UtcNow,
            Status = NotificationStatus.Sent
        });
        await _context.SaveChangesAsync();
    }

    public Task SendEmailAsync(string email, string subject, string body)
    {
        _logger.LogInformation("EMAIL to {Email}: [{Subject}] {Body}", email, subject, body);
        return Task.CompletedTask;
    }
}

public interface IBetService
{
    Task<(bool Success, string Message, bool? IsWin, string? BonusMessage)> PlaceBetAsync(string playerId, int coefficientId, decimal amount, bool useFreeBet = false);
    Task<(bool Success, string Message)> SettleEventAsync(int eventId, int winningCoefficientId, string result, string? userId, string? ip, bool finishNow = false);
}

public class BetService : IBetService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IActionLogService _actionLogService;
    private readonly IBonusService _bonusService;

    public BetService(
        ApplicationDbContext context,
        INotificationService notificationService,
        IActionLogService actionLogService,
        IBonusService bonusService)
    {
        _context = context;
        _notificationService = notificationService;
        _actionLogService = actionLogService;
        _bonusService = bonusService;
    }

    public async Task<(bool Success, string Message, bool? IsWin, string? BonusMessage)> PlaceBetAsync(string playerId, int coefficientId, decimal amount, bool useFreeBet = false)
    {
        Bonus? freeBetBonus = null;
        if (useFreeBet)
        {
            freeBetBonus = await _bonusService.GetActiveFreeBetAsync(playerId);
            if (freeBetBonus == null)
                return (false, "Нет активного фрибета. Активируйте бонус в профиле.", null, null);

            amount = freeBetBonus.Amount;
        }
        else if (amount < BetRules.MinAmount)
        {
            return (false, $"Минимальная ставка — {BetRules.MinAmount:N0} ₽", null, null);
        }

        var coefficient = await _context.Coefficients
            .Include(c => c.SportEvent)
            .FirstOrDefaultAsync(c => c.Id == coefficientId);

        if (coefficient == null)
            return (false, "Исход не найден", null, null);

        if (coefficient.SportEvent.Status != EventStatus.AcceptingBets)
            return (false, "Событие не принимает ставки", null, null);

        var player = await _context.Users.FindAsync(playerId);
        if (player == null || player.Status == UserStatus.Blocked)
            return (false, "Аккаунт недоступен", null, null);

        if (!useFreeBet && player.Balance < amount)
            return (false, "Недостаточно средств на счёте", null, null);

        if (!useFreeBet)
            player.Balance -= amount;

        var bet = new Bet
        {
            PlayerId = playerId,
            SportEventId = coefficient.SportEventId,
            CoefficientId = coefficientId,
            CoefficientValue = coefficient.Value,
            OutcomeDescription = coefficient.OutcomeDescription,
            Amount = amount,
            Status = BetStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            BonusId = freeBetBonus?.Id
        };

        _context.Bets.Add(bet);
        await _context.SaveChangesAsync();

        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = playerId,
            OperationType = OperationType.BetWithdrawal,
            Amount = amount,
            BetId = bet.Id,
            IsFreeBonus = useFreeBet,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        if (useFreeBet && freeBetBonus != null)
            await _bonusService.ConsumeFreeBetAsync(playerId, freeBetBonus.Id);

        var bonusMessage = await _bonusService.CheckBetMilestoneAsync(playerId);

        var coefficients = await _context.Coefficients
            .Where(c => c.SportEventId == coefficient.SportEventId)
            .ToListAsync();

        var winningCoefficient = coefficients[Random.Shared.Next(coefficients.Count)];
        var result = $"Исход: {winningCoefficient.OutcomeDescription}";

        await SettleEventAsync(
            coefficient.SportEventId,
            winningCoefficient.Id,
            result,
            playerId,
            null,
            finishNow: true);

        var won = winningCoefficient.Id == coefficientId;
        var betLabel = useFreeBet ? " (фрибет)" : "";
        var message = won
            ? $"Матч завершён! Вы выиграли {Math.Round(amount * coefficient.Value, 2):N2} ₽{betLabel} (исход: {winningCoefficient.OutcomeDescription})"
            : $"Матч завершён. Ставка проиграла{betLabel}. Выигрышный исход: {winningCoefficient.OutcomeDescription}";

        return (true, message, won, bonusMessage);
    }

    public async Task<(bool Success, string Message)> SettleEventAsync(
        int eventId, int winningCoefficientId, string result, string? userId, string? ip, bool finishNow = false)
    {
        var sportEvent = await _context.SportEvents
            .Include(e => e.Bets)
            .ThenInclude(b => b.Player)
            .Include(e => e.Coefficients)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (sportEvent == null)
            return (false, "Событие не найдено");

        if (sportEvent.Status == EventStatus.Completed)
            return (false, "Результат уже введён");

        sportEvent.Status = EventStatus.Completed;
        sportEvent.Result = result;
        sportEvent.WinningCoefficientId = winningCoefficientId;
        if (finishNow)
            sportEvent.StartDate = DateTime.UtcNow;

        foreach (var bet in sportEvent.Bets.Where(b => b.Status == BetStatus.Accepted))
        {
            if (bet.CoefficientId == winningCoefficientId)
            {
                bet.Status = BetStatus.Won;
                bet.Winnings = Math.Round(bet.Amount * bet.CoefficientValue, 2);
                bet.Player.Balance += bet.Winnings;

                _context.AccountOperations.Add(new AccountOperation
                {
                    PlayerId = bet.PlayerId,
                    OperationType = OperationType.Win,
                    Amount = bet.Winnings,
                    BetId = bet.Id,
                    CreatedAt = DateTime.UtcNow
                });

                await _notificationService.SendAsync(
                    bet.PlayerId,
                    NotificationType.BetWin,
                    $"Ваша ставка на «{sportEvent.Title}» выиграла! Выигрыш: {bet.Winnings:N2} ₽");

                if (!string.IsNullOrEmpty(bet.Player.Email))
                {
                    await _notificationService.SendEmailAsync(
                        bet.Player.Email,
                        "Результат ставки — СпортЛиния",
                        $"Поздравляем! Ваша ставка выиграла. Выигрыш: {bet.Winnings:N2} ₽");
                }
            }
            else
            {
                bet.Status = BetStatus.Lost;
                bet.Winnings = 0;

                await _notificationService.SendAsync(
                    bet.PlayerId,
                    NotificationType.BetLoss,
                    $"Ваша ставка на «{sportEvent.Title}» проиграла.");

                if (!string.IsNullOrEmpty(bet.Player.Email))
                {
                    await _notificationService.SendEmailAsync(
                        bet.Player.Email,
                        "Результат ставки — СпортЛиния",
                        "К сожалению, ваша ставка не сыграла.");
                }
            }
        }

        await _context.SaveChangesAsync();
        await _actionLogService.LogAsync(userId, $"Ввод результата события #{eventId}: {result}", ip);

        return (true, "Результаты рассчитаны");
    }
}
