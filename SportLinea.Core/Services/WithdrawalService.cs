using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;

namespace SportLinea.Services;

public interface IWithdrawalService
{
    Task<(bool Success, string Message, string FlashKey)> RequestWithdrawalAsync(string playerId, decimal amount);
    Task<(bool Success, string Message)> ApproveAsync(int requestId, string bookmakerId);
    Task<(bool Success, string Message)> RejectAsync(int requestId, string bookmakerId);
}

public class WithdrawalService : IWithdrawalService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public WithdrawalService(ApplicationDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<(bool Success, string Message, string FlashKey)> RequestWithdrawalAsync(string playerId, decimal amount)
    {
        var user = await _context.Users.FindAsync(playerId);
        if (user == null)
            return (false, "Пользователь не найден", "Error");

        if (amount < WithdrawRules.MinAmount)
            return (false, $"Минимальная сумма вывода — {WithdrawRules.MinAmount:N0} ₽", "Error");

        if (amount > user.Balance)
            return (false, "Недостаточно средств на счёте", "Error");

        user.Balance -= amount;

        if (amount < WithdrawRules.BookmakerApprovalThreshold)
        {
            _context.AccountOperations.Add(new AccountOperation
            {
                PlayerId = playerId,
                OperationType = OperationType.Withdrawal,
                Amount = amount,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                playerId,
                NotificationType.WithdrawalAccepted,
                $"Заявка на вывод {amount:N2} ₽ принята. Средства списаны со счёта.");

            return (true, $"Заявка на вывод {amount:N2} ₽ принята. Средства списаны со счёта.", "Success");
        }

        _context.WithdrawalRequests.Add(new WithdrawalRequest
        {
            PlayerId = playerId,
            Amount = amount,
            Status = WithdrawalRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _notificationService.SendAsync(playerId, NotificationType.WithdrawalPending,
            $"Заявка на вывод {amount:N2} ₽ отправлена на проверку букмекеру.");

        return (true, $"Заявка на вывод {amount:N2} ₽ отправлена на проверку букмекеру.", "Warning");
    }

    public async Task<(bool Success, string Message)> ApproveAsync(int requestId, string bookmakerId)
    {
        var request = await _context.WithdrawalRequests
            .Include(r => r.Player)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
            return (false, "Заявка не найдена");

        if (request.Status != WithdrawalRequestStatus.Pending)
            return (false, "Заявка уже обработана");

        request.Status = WithdrawalRequestStatus.Approved;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedByUserId = bookmakerId;

        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = request.PlayerId,
            OperationType = OperationType.Withdrawal,
            Amount = request.Amount,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _notificationService.SendAsync(request.PlayerId, NotificationType.WithdrawalApproved,
            $"Вывод {request.Amount:N2} ₽ подтверждён букмекером. Средства списаны со счёта.");

        return (true, $"Вывод {request.Amount:N2} ₽ для {request.Player.FullName} подтверждён");
    }

    public async Task<(bool Success, string Message)> RejectAsync(int requestId, string bookmakerId)
    {
        var request = await _context.WithdrawalRequests
            .Include(r => r.Player)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
            return (false, "Заявка не найдена");

        if (request.Status != WithdrawalRequestStatus.Pending)
            return (false, "Заявка уже обработана");

        request.Status = WithdrawalRequestStatus.Rejected;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedByUserId = bookmakerId;
        request.Player.Balance += request.Amount;

        await _context.SaveChangesAsync();

        await _notificationService.SendAsync(request.PlayerId, NotificationType.WithdrawalRejected,
            $"Заявка на вывод {request.Amount:N2} ₽ отклонена букмекером. Средства возвращены на ваш счёт.");

        return (true, $"Вывод {request.Amount:N2} ₽ для {request.Player.FullName} отклонён, средства возвращены");
    }
}
