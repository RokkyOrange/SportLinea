using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;
using SportLinea.Models.ViewModels;
using SportLinea.Services;

namespace SportLinea.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBonusService _bonusService;
    private readonly ILineSelectionService _lineSelection;

    public HomeController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IBonusService bonusService,
        ILineSelectionService lineSelection)
    {
        _context = context;
        _userManager = userManager;
        _bonusService = bonusService;
        _lineSelection = lineSelection;
    }

    public async Task<IActionResult> Index()
    {
        var isStaff = User.IsInRole(Roles.Bookmaker) || User.IsInRole(Roles.SuperUser);
        var activeQuery = _context.SportEvents
            .Include(e => e.Coefficients)
            .Include(e => e.Bets)
            .Where(e => e.Status == EventStatus.AcceptingBets && !e.IsDeleted);

        if (!isStaff)
        {
            var activeIds = await activeQuery.Select(e => e.Id).ToListAsync();
            var displayIds = _lineSelection.GetDisplayIds(activeIds);
            activeQuery = activeQuery.Where(e => displayIds.Contains(e.Id));
        }

        var activeEvents = await activeQuery.ToListAsync();
        var lineEmpty = !activeEvents.Any();
        var crazyTimeAllowed = await IsCrazyTimeAllowedForCurrentUserAsync();

        var model = new HomeViewModel
        {
            TopEvents = activeEvents
                .OrderByDescending(e => e.Bets.Count)
                .Take(3)
                .ToList(),
            NewEvents = activeEvents
                .OrderByDescending(e => e.CreatedAt)
                .Take(3)
                .ToList(),
            CrazyTimeAllowed = crazyTimeAllowed,
            ShowRoulette = lineEmpty && crazyTimeAllowed,
            ShowPlinko = lineEmpty && crazyTimeAllowed,
            ShowZeusHades = lineEmpty && crazyTimeAllowed,
            ShowCamelotGold = lineEmpty && crazyTimeAllowed,
            ShowFruitland1000 = lineEmpty && crazyTimeAllowed,
            ShowEmptyLineRefresh = lineEmpty && !crazyTimeAllowed && User.Identity?.IsAuthenticated == true
        };

        if (User.IsInRole(Roles.Player))
        {
            var user = await _userManager.GetUserAsync(User);
            model.PlayerBalance = user?.Balance;

            if (user != null && crazyTimeAllowed)
            {
                var freeRoulette = await _bonusService.GetActiveFreeRouletteBonusAsync(user.Id);
                model.FreeRouletteSpinsRemaining = freeRoulette?.UsesRemaining ?? 0;
                model.FreeRouletteSpinAmount = freeRoulette?.Amount ?? 0;

                var freePlinkoBounty = await _bonusService.GetActiveFreePlinkoBountyAsync(user.Id);
                model.FreePlinkoBountyAvailable = freePlinkoBounty != null;
                model.FreePlinkoBountyStake = freePlinkoBounty?.Amount ?? BonusRules.FreePlinkoBountyStake;
            }
        }

        return View(model);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    private async Task<bool> IsCrazyTimeAllowedForCurrentUserAsync()
    {
        if (!User.IsInRole(Roles.Player))
            return true;

        var user = await _userManager.GetUserAsync(User);
        return user == null || CountryPolicy.AllowsCrazyTime(user.CountryOfResidence);
    }
}
