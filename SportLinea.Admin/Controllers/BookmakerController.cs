using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;
using SportLinea.Models.ViewModels;
using SportLinea.Services;

namespace SportLinea.Controllers;

[Authorize(Roles = Roles.Bookmaker + "," + Roles.SuperUser)]
public class BookmakerController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBetService _betService;
    private readonly IActionLogService _actionLogService;
    private readonly IBonusService _bonusService;
    private readonly IWithdrawalService _withdrawalService;
    private readonly INotificationService _notificationService;

    public BookmakerController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IBetService betService,
        IActionLogService actionLogService,
        IBonusService bonusService,
        IWithdrawalService withdrawalService,
        INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _betService = betService;
        _actionLogService = actionLogService;
        _bonusService = bonusService;
        _withdrawalService = withdrawalService;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.EventsCount = await _context.SportEvents.CountAsync(e => e.Status == EventStatus.AcceptingBets);
        ViewBag.BetsCount = await _context.Bets.CountAsync(b => b.Status == BetStatus.Accepted);
        ViewBag.PlayersCount = (await _userManager.GetUsersInRoleAsync(Roles.Player)).Count;
        ViewBag.PendingWithdrawalsCount = await _context.WithdrawalRequests
            .CountAsync(r => r.Status == WithdrawalRequestStatus.Pending);
        return View();
    }

    public async Task<IActionResult> Events()
    {
        var events = await _context.SportEvents
            .Include(e => e.Coefficients)
            .Include(e => e.Bets)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();
        return View(events);
    }

    [HttpGet]
    public IActionResult CreateEvent() => View(new CreateEventViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(CreateEventViewModel model)
    {
        if (!SportTypes.IsValid(model.SportCategory, model.SportType))
            ModelState.AddModelError(nameof(model.SportType), "Выберите вид спорта, соответствующий категории");

        if (!ModelState.IsValid)
            return View(model);

        var sportEvent = new SportEvent
        {
            SportCategory = model.SportCategory,
            SportType = model.SportType,
            Title = model.Title,
            StartDate = model.StartDate,
            Status = EventStatus.AcceptingBets,
            CreatedAt = DateTime.UtcNow,
            Coefficients = new List<Coefficient>
            {
                new() { OutcomeDescription = model.Outcome1, Value = model.Coefficient1 },
                new() { OutcomeDescription = model.Outcome2, Value = model.Coefficient2 },
                new() { OutcomeDescription = model.Outcome3, Value = model.Coefficient3 }
            }
        };

        _context.SportEvents.Add(sportEvent);
        await _context.SaveChangesAsync();

        var user = await _userManager.GetUserAsync(User);
        await _actionLogService.LogAsync(user?.Id, $"Создание события: {model.Title}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = "Событие создано";
        return RedirectToAction(nameof(Events));
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var sportEvent = await _context.SportEvents.Include(e => e.Bets).FirstOrDefaultAsync(e => e.Id == id);
        if (sportEvent == null) return NotFound();

        return View(new EditEventViewModel
        {
            Id = sportEvent.Id,
            SportType = sportEvent.SportType,
            Title = sportEvent.Title,
            StartDate = sportEvent.StartDate,
            HasBets = sportEvent.Bets.Any()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(EditEventViewModel model)
    {
        var sportEvent = await _context.SportEvents.Include(e => e.Bets).FirstOrDefaultAsync(e => e.Id == model.Id);
        if (sportEvent == null) return NotFound();

        model.HasBets = sportEvent.Bets.Any();

        if (model.HasBets)
        {
            if (sportEvent.SportType != model.SportType || sportEvent.Title != model.Title || sportEvent.StartDate != model.StartDate)
            {
                ModelState.AddModelError(string.Empty, "Нельзя изменять событие, по которому уже есть ставки");
                return View(model);
            }
        }
        else
        {
            sportEvent.SportType = model.SportType;
            sportEvent.Title = model.Title;
            sportEvent.StartDate = model.StartDate;
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Событие обновлено";
        return RedirectToAction(nameof(Events));
    }

    [HttpGet]
    public async Task<IActionResult> Coefficients(int id)
    {
        var sportEvent = await _context.SportEvents
            .Include(e => e.Coefficients)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (sportEvent == null) return NotFound();
        return View(sportEvent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCoefficient(int id, decimal value)
    {
        var coefficient = await _context.Coefficients.FindAsync(id);
        if (coefficient == null) return NotFound();

        if (value < 1.01m || value > 100)
        {
            TempData["Error"] = "Коэффициент должен быть от 1.01 до 100";
            return RedirectToAction(nameof(Coefficients), new { id = coefficient.SportEventId });
        }

        coefficient.Value = value;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Коэффициент обновлён";
        return RedirectToAction(nameof(Coefficients), new { id = coefficient.SportEventId });
    }

    [HttpGet]
    public async Task<IActionResult> SetResult(int id)
    {
        var sportEvent = await _context.SportEvents
            .Include(e => e.Coefficients)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (sportEvent == null) return NotFound();

        ViewBag.Coefficients = sportEvent.Coefficients;

        return View(new SetResultViewModel
        {
            SportEventId = sportEvent.Id,
            EventTitle = sportEvent.Title,
            Result = sportEvent.Result ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetResult(SetResultViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        var (success, message) = await _betService.SettleEventAsync(
            model.SportEventId, model.WinningCoefficientId, model.Result,
            user?.Id, HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Events));
    }

    public async Task<IActionResult> Bets(string? search)
    {
        var query = _context.Bets
            .Include(b => b.Player)
            .Include(b => b.SportEvent)
            .Include(b => b.Coefficient)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(b =>
                b.Player.LastName.Contains(search) ||
                b.Player.FirstName.Contains(search) ||
                b.SportEvent.Title.Contains(search));

        var bets = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        ViewBag.Search = search;
        return View(bets);
    }

    public async Task<IActionResult> Players()
    {
        var playerIds = (await _userManager.GetUsersInRoleAsync(Roles.Player)).Select(p => p.Id).ToList();
        var players = await _context.Users
            .Include(u => u.BlockedBy)
            .Where(u => playerIds.Contains(u.Id))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();
        return View(players);
    }

    [HttpGet]
    public async Task<IActionResult> BlockPlayer(string id)
    {
        var player = await _userManager.FindByIdAsync(id);
        if (player == null) return NotFound();

        if (player.Status == UserStatus.Blocked)
            return RedirectToAction(nameof(Players));

        return View(new BlockPlayerViewModel
        {
            PlayerId = player.Id,
            PlayerName = player.FullName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BlockPlayer(BlockPlayerViewModel model)
    {
        var player = await _userManager.FindByIdAsync(model.PlayerId);
        if (player == null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.PlayerName = player.FullName;
            return View(model);
        }

        var bookmaker = await _userManager.GetUserAsync(User);
        player.Status = UserStatus.Blocked;
        player.BlockReason = model.BlockReason.Trim();
        player.BlockedByUserId = bookmaker?.Id;
        await _userManager.UpdateAsync(player);

        await _actionLogService.LogAsync(bookmaker?.Id,
            $"Блокировка игрока {player.Email}. Причина: {player.BlockReason}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = "Игрок заблокирован";
        return RedirectToAction(nameof(Players));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblockPlayer(string id)
    {
        var player = await _context.Users.FindAsync(id);
        if (player == null) return NotFound();

        var notifyBalanceReset = player.BalanceResetNoticePending;

        player.Status = UserStatus.Active;
        player.BlockReason = null;
        player.BlockedByUserId = null;

        if (notifyBalanceReset)
        {
            await _notificationService.SendAsync(player.Id, NotificationType.BalanceReset,
                "Ваш счёт был обнулён букмекером во время блокировки аккаунта.");
        }

        await _context.SaveChangesAsync();

        var user = await _userManager.GetUserAsync(User);
        await _actionLogService.LogAsync(user?.Id,
            $"Разблокировка игрока {player.Email}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = "Игрок разблокирован";
        return RedirectToAction(nameof(Players));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPlayerBalance(string id)
    {
        var player = await _context.Users.FindAsync(id);
        if (player == null)
            return NotFound();

        if (!await _userManager.IsInRoleAsync(player, Roles.Player))
            return NotFound();

        if (player.Status != UserStatus.Blocked)
        {
            TempData["Error"] = "Обнулить счёт можно только у заблокированного игрока";
            return RedirectToAction(nameof(Players));
        }

        if (player.Balance <= 0)
        {
            TempData["Error"] = "Баланс игрока уже нулевой";
            return RedirectToAction(nameof(Players));
        }

        var bookmaker = await _userManager.GetUserAsync(User);
        var resetAmount = player.Balance;
        player.Balance = 0;
        player.BalanceResetNoticePending = true;

        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = player.Id,
            OperationType = OperationType.BalanceReset,
            Amount = resetAmount,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        await _actionLogService.LogAsync(bookmaker?.Id,
            $"Обнуление счёта заблокированного игрока {player.Email}. Списано: {resetAmount:N2} ₽",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Счёт игрока {player.FullName} обнулён (−{resetAmount:N2} ₽)";
        return RedirectToAction(nameof(Players));
    }

    [HttpGet]
    public async Task<IActionResult> AssignBonus(string id)
    {
        var player = await _userManager.FindByIdAsync(id);
        if (player == null) return NotFound();

        return View(new AssignBonusViewModel
        {
            PlayerId = player.Id,
            PlayerName = player.FullName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignBonus(AssignBonusViewModel model)
    {
        if (model.BonusKind == BookmakerBonusKind.FreeBet)
        {
            if (model.FreeBetAmount <= 0)
                ModelState.AddModelError(nameof(model.FreeBetAmount), "Укажите сумму фрибета");
        }
        else if (model.BonusKind == BookmakerBonusKind.FreeRouletteSpins)
        {
            if (model.SpinCount <= 0)
                ModelState.AddModelError(nameof(model.SpinCount), "Укажите количество фриспинов");
            if (model.SpinAmount <= 0)
                ModelState.AddModelError(nameof(model.SpinAmount), "Укажите номинал фриспина");
        }

        if (!ModelState.IsValid)
            return View(model);

        var bookmaker = await _userManager.GetUserAsync(User);
        var (success, message) = await _bonusService.AssignBookmakerBonusAsync(
            model.PlayerId,
            model.BonusKind,
            model.FreeBetAmount,
            model.SpinCount,
            model.SpinAmount,
            model.EndDate,
            model.BookmakerComment,
            bookmaker!.Id);

        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        await _actionLogService.LogAsync(bookmaker.Id,
            $"Назначение бонуса игроку {model.PlayerId}: {model.BonusKind}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = message;
        return RedirectToAction(nameof(Players));
    }

    public async Task<IActionResult> Reports()
    {
        var report = await _context.SportEvents
            .Select(e => new
            {
                Event = e,
                BetsCount = e.Bets.Count,
                TotalAmount = e.Bets.Sum(b => (decimal?)b.Amount) ?? 0
            })
            .OrderByDescending(x => x.Event.StartDate)
            .ToListAsync();

        return View(report);
    }

    public async Task<IActionResult> ExportBets()
    {
        var bets = await _context.Bets
            .Include(b => b.Player)
            .Include(b => b.SportEvent)
            .Include(b => b.Coefficient)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Ставки");
        ws.Cell(1, 1).Value = "Дата";
        ws.Cell(1, 2).Value = "Игрок";
        ws.Cell(1, 3).Value = "Событие";
        ws.Cell(1, 4).Value = "Исход";
        ws.Cell(1, 5).Value = "Коэффициент";
        ws.Cell(1, 6).Value = "Сумма";
        ws.Cell(1, 7).Value = "Статус";
        ws.Cell(1, 8).Value = "Выигрыш";

        for (int i = 0; i < bets.Count; i++)
        {
            var b = bets[i];
            ws.Cell(i + 2, 1).Value = b.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            ws.Cell(i + 2, 2).Value = b.Player.FullName;
            ws.Cell(i + 2, 3).Value = b.SportEvent.Title;
            ws.Cell(i + 2, 4).Value = string.IsNullOrEmpty(b.OutcomeDescription) ? b.Coefficient.OutcomeDescription : b.OutcomeDescription;
            ws.Cell(i + 2, 5).Value = b.CoefficientValue;
            ws.Cell(i + 2, 6).Value = b.Amount;
            ws.Cell(i + 2, 7).Value = b.Status.ToString();
            ws.Cell(i + 2, 8).Value = b.Winnings;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"stavki_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> Withdrawals()
    {
        var requests = await _context.WithdrawalRequests
            .Include(r => r.Player)
            .Where(r => r.Status == WithdrawalRequestStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveWithdrawal(int id)
    {
        var bookmaker = await _userManager.GetUserAsync(User);
        var (success, message) = await _withdrawalService.ApproveAsync(id, bookmaker!.Id);

        await _actionLogService.LogAsync(bookmaker?.Id, message,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Withdrawals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectWithdrawal(int id)
    {
        var bookmaker = await _userManager.GetUserAsync(User);
        var (success, message) = await _withdrawalService.RejectAsync(id, bookmaker!.Id);

        await _actionLogService.LogAsync(bookmaker?.Id, message,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Withdrawals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var sportEvent = await _context.SportEvents.Include(e => e.Bets).FirstOrDefaultAsync(e => e.Id == id);
        if (sportEvent == null) return NotFound();

        if (sportEvent.Bets.Any())
        {
            TempData["Error"] = "Нельзя удалить событие со ставками";
            return RedirectToAction(nameof(Events));
        }

        sportEvent.IsDeleted = true;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Событие удалено";
        return RedirectToAction(nameof(Events));
    }
}
