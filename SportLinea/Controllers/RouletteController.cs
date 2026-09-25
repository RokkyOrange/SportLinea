using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SportLinea.Models;
using SportLinea.Services;

namespace SportLinea.Controllers;

[Authorize(Roles = Roles.Player)]
public class RouletteController : Controller
{
    private readonly IRouletteService _rouletteService;
    private readonly IBonusService _bonusService;
    private readonly UserManager<ApplicationUser> _userManager;

    public RouletteController(
        IRouletteService rouletteService,
        IBonusService bonusService,
        UserManager<ApplicationUser> userManager)
    {
        _rouletteService = rouletteService;
        _bonusService = bonusService;
        _userManager = userManager;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Spin([FromForm] decimal amount, [FromForm] bool useFreeSpin = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Crazy Time недоступен в вашей стране проживания." });

        var (success, message, result) = await _rouletteService.SpinAsync(user.Id, amount, useFreeSpin);

        if (!success || result == null)
            return Json(new { success = false, message });

        var freeRoulette = await _bonusService.GetActiveFreeRouletteBonusAsync(user.Id);

        return Json(new
        {
            success = true,
            message,
            segmentIndex = result.SegmentIndex,
            label = result.Label,
            multiplier = result.Multiplier,
            winnings = result.Winnings,
            balance = result.NewBalance,
            isBonus = result.IsBonus,
            bonusKind = result.BonusKind.ToString().ToLowerInvariant(),
            bonusSpinsRemaining = result.BonusSpinsRemaining,
            bonusMessage = result.BonusAwardMessage,
            freeSpinsRemaining = freeRoulette?.UsesRemaining ?? 0
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BonusSpin()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Crazy Time недоступен в вашей стране проживания." });

        var (success, message, result) = await _rouletteService.BonusSpinAsync(user.Id);

        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message,
            segmentIndex = result.SegmentIndex,
            label = result.Label,
            bonusKind = result.BonusKind.ToString().ToLowerInvariant(),
            isJackpot = result.IsJackpot,
            isBankrupt = result.IsBankrupt,
            isSensation = result.IsSensation,
            multiplier = result.Multiplier,
            winnings = result.Winnings,
            balance = result.NewBalance,
            spinsRemaining = result.SpinsRemaining,
            bonusComplete = result.BonusComplete,
            totalBonusWinnings = result.TotalBonusWinnings,
            bonusTotalSummary = result.BonusTotalSummary
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyBonus([FromForm] decimal amount, [FromForm] string bonusKind)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Crazy Time недоступен в вашей стране проживания." });

        var kind = bonusKind.ToLowerInvariant() switch
        {
            "crazy" => RouletteBonusKind.Crazy,
            "hotspins" => RouletteBonusKind.HotSpins,
            _ => RouletteBonusKind.None
        };

        if (kind == RouletteBonusKind.None)
            return Json(new { success = false, message = "Некорректный тип бонусной игры" });

        var (success, message, result) = await _rouletteService.BuyBonusAsync(user.Id, amount, kind);

        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message,
            balance = result.NewBalance,
            pricePaid = result.PricePaid,
            stakeAmount = result.StakeAmount,
            bonusKind = result.BonusKind.ToString().ToLowerInvariant(),
            bonusSpinsRemaining = result.BonusSpinsRemaining
        });
    }

    [HttpGet]
    public async Task<IActionResult> State()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Crazy Time недоступен в вашей стране проживания." });

        var pending = await _rouletteService.GetPendingBonusAsync(user.Id);
        if (pending == null)
            return Json(new { success = true, hasPendingBonus = false });

        return Json(new
        {
            success = true,
            hasPendingBonus = true,
            bonusKind = pending.BonusKind.ToString().ToLowerInvariant(),
            bonusSpinsRemaining = pending.SpinsRemaining,
            stakeAmount = pending.StakeAmount,
            totalBonusWinnings = pending.TotalBonusWinnings
        });
    }
}
