using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;
using SportLinea.Models.ViewModels;
using SportLinea.Services;

namespace SportLinea.Controllers;

public class EventsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBetService _betService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBonusService _bonusService;

    public EventsController(
        ApplicationDbContext context,
        IBetService betService,
        UserManager<ApplicationUser> userManager,
        IBonusService bonusService)
    {
        _context = context;
        _betService = betService;
        _userManager = userManager;
        _bonusService = bonusService;
    }

    public async Task<IActionResult> Details(int id)
    {
        var sportEvent = await _context.SportEvents
            .Include(e => e.Coefficients)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (sportEvent == null)
            return NotFound();

        if (User.IsInRole(Roles.Player))
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
                ViewBag.ActiveFreeBet = await _bonusService.GetActiveFreeBetAsync(user.Id);
        }

        return View(sportEvent);
    }

    [Authorize(Roles = Roles.Player)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceBet(PlaceBetViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        var (success, message, isWin, bonusMessage) = await _betService.PlaceBetAsync(user.Id, model.CoefficientId, model.Amount, model.UseFreeBet);

        if (!success)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(Details), new { id = model.SportEventId });
        }

        if (isWin == true)
            TempData["BetWin"] = message;
        else
            TempData["BetLoss"] = message;

        if (!string.IsNullOrEmpty(bonusMessage))
            TempData["Bonus"] = bonusMessage;

        return Redirect("/Line");
    }
}
