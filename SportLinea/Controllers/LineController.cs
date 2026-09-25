using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;
using SportLinea.Services;

namespace SportLinea.Controllers;

public class LineController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILineSelectionService _lineSelection;
    private readonly IActionLogService _actionLogService;

    public LineController(
        ApplicationDbContext context,
        ILineSelectionService lineSelection,
        IActionLogService actionLogService)
    {
        _context = context;
        _lineSelection = lineSelection;
        _actionLogService = actionLogService;
    }

    public async Task<IActionResult> Index(string? sport, string? category)
    {
        var isStaff = IsStaffLineView();
        var allActiveIds = await GetActiveEventIdsAsync();

        var query = _context.SportEvents
            .Include(e => e.Coefficients)
            .Where(e => e.Status == EventStatus.AcceptingBets && !e.IsDeleted);

        if (!isStaff)
        {
            var selectedIds = _lineSelection.GetDisplayIds(allActiveIds);
            query = query.Where(e => selectedIds.Contains(e.Id));
            ViewBag.AvailableEventCount = selectedIds.Count;
        }

        if (!string.IsNullOrEmpty(sport))
            query = query.Where(e => e.SportType == sport);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(e => e.SportCategory == category);

        var events = await query.OrderBy(e => e.SportCategory).ThenBy(e => e.StartDate).ToListAsync();

        if (User.IsInRole(Roles.Player))
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
            ViewBag.CrazyTimeAllowed = user == null || CountryPolicy.AllowsCrazyTime(user.CountryOfResidence);
        }
        else
        {
            ViewBag.CrazyTimeAllowed = true;
        }

        var filterBase = _context.SportEvents
            .Where(e => e.Status == EventStatus.AcceptingBets && !e.IsDeleted);
        if (!isStaff)
        {
            var selectedIds = _lineSelection.GetDisplayIds(allActiveIds);
            filterBase = filterBase.Where(e => selectedIds.Contains(e.Id));
        }

        ViewBag.Sports = await filterBase
            .Select(e => e.SportType)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();
        ViewBag.Categories = await filterBase
            .Select(e => e.SportCategory)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
        ViewBag.SelectedSport = sport;
        ViewBag.SelectedCategory = category;
        ViewBag.IsStaffLineView = isStaff;
        ViewBag.ShowRefresh = true;
        ViewBag.DisplayedCount = events.Count;
        ViewBag.TotalActiveCount = allActiveIds.Count;
        ViewBag.PlayerLineLimit = LineRules.PlayerLineDisplayCount;
        return View(events);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh(bool returnHome = false)
    {
        if (IsStaffLineView())
            return await RefreshStaffLineAsync();

        return await RefreshPlayerLineAsync(returnHome);
    }

    private async Task<IActionResult> RefreshStaffLineAsync()
    {
        var eventsToReopen = await _context.SportEvents
            .Where(e => !e.IsDeleted && e.Status != EventStatus.AcceptingBets)
            .ToListAsync();

        foreach (var sportEvent in eventsToReopen)
        {
            sportEvent.Status = EventStatus.AcceptingBets;
            sportEvent.Result = null;
            sportEvent.WinningCoefficientId = null;
        }

        if (eventsToReopen.Count > 0)
            await _context.SaveChangesAsync();

        var totalActive = await _context.SportEvents
            .CountAsync(e => e.Status == EventStatus.AcceptingBets && !e.IsDeleted);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity!.Name);
        await _actionLogService.LogAsync(
            user?.Id,
            eventsToReopen.Count > 0
                ? $"Обновление линии букмекером: {eventsToReopen.Count} событий снова принимают ставки"
                : "Обновление линии букмекером: все события уже активны",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = eventsToReopen.Count > 0
            ? $"Линия обновлена: {eventsToReopen.Count} {(eventsToReopen.Count == 1 ? "событие снова" : eventsToReopen.Count < 5 ? "события снова" : "событий снова")} принимает ставки."
            : $"Все {totalActive} {(totalActive == 1 ? "событие уже" : totalActive < 5 ? "события уже" : "событий уже")} принимает ставки.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> RefreshPlayerLineAsync(bool returnHome)
    {
        var allActiveIds = await GetActiveEventIdsAsync();

        if (allActiveIds.Count == 0)
        {
            TempData["Warning"] = "Нет активных событий. Дождитесь, пока букмекер обновит линию.";
        }
        else
        {
            var selected = _lineSelection.Refresh(allActiveIds);
            TempData["Success"] =
                $"Линия обновлена: показано {selected.Count} {(selected.Count == 1 ? "новое событие" : selected.Count < 5 ? "новых события" : "новых событий")}.";
        }

        if (returnHome && allActiveIds.Count > 0)
            return RedirectToAction("Index", "Home");

        return RedirectToAction(nameof(Index));
    }

    private Task<List<int>> GetActiveEventIdsAsync() =>
        _context.SportEvents
            .Where(e => e.Status == EventStatus.AcceptingBets && !e.IsDeleted)
            .Select(e => e.Id)
            .ToListAsync();

    private bool IsStaffLineView() =>
        User.IsInRole(Roles.Bookmaker) || User.IsInRole(Roles.SuperUser);
}
