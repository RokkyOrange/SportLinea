using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SportLinea.Models;
using SportLinea.Services;

namespace SportLinea.Controllers;

[Authorize(Roles = Roles.Player)]
public class PlinkoController : Controller
{
    private readonly IPlinkoService _plinkoService;
    private readonly UserManager<ApplicationUser> _userManager;

    public PlinkoController(IPlinkoService plinkoService, UserManager<ApplicationUser> userManager)
    {
        _plinkoService = plinkoService;
        _userManager = userManager;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Spin([FromForm] decimal amount)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Слот недоступен в вашей стране проживания." });

        var (success, message, result) = await _plinkoService.SpinAsync(user.Id, amount);
        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message,
            balance = result.NewBalance,
            bonusHunt = result.BonusHunt,
            picksRemaining = result.PicksRemaining
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pick([FromForm] int tileIndex)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Слот недоступен в вашей стране проживания." });

        var (success, message, result) = await _plinkoService.PickTileAsync(user.Id, tileIndex);
        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message = result.Message,
            tileIndex = result.TileIndex,
            symbol = result.Symbol,
            symbolKey = result.SymbolKey,
            emoji = result.Emoji,
            picksRemaining = result.PicksRemaining,
            roundComplete = result.RoundComplete,
            winnings = result.Winnings,
            balance = result.NewBalance,
            bountyTriggered = result.BountyTriggered,
            bountySpinsRemaining = result.BountySpinsRemaining,
            bountyLevel = result.BountyLevel,
            bonusMessage = result.BonusAwardMessage
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BountyShoot([FromForm] int targetIndex)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Слот недоступен в вашей стране проживания." });

        var (success, message, result) = await _plinkoService.BountyShootAsync(user.Id, targetIndex);
        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message = result.Message,
            targetIndex = result.TargetIndex,
            outcomeLabel = result.OutcomeLabel,
            isBullet = result.IsBullet,
            bulletsGained = result.BulletsGained,
            bulletsOnLevel = result.BulletsOnLevel,
            bulletsRequired = result.BulletsRequired,
            totalMultiplierSum = result.TotalMultiplierSum,
            spinsRemaining = result.SpinsRemaining,
            level = result.Level,
            levelAdvanced = result.LevelAdvanced,
            bonusSpinsGained = result.BonusSpinsGained,
            bonusComplete = result.BonusComplete,
            winnings = result.Winnings,
            balance = result.NewBalance
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyBounty([FromForm] decimal amount, [FromForm] bool useFreeBounty = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Слот недоступен в вашей стране проживания." });

        var (success, message, result) = await _plinkoService.BuyBountyAsync(
            user.Id, amount, useFreeBounty: useFreeBounty);
        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message,
            balance = result.NewBalance,
            pricePaid = result.PricePaid,
            bountySpinsRemaining = result.BountySpinsRemaining,
            bountyLevel = result.BountyLevel,
            bonusMessage = result.BonusAwardMessage,
            freePlinkoBountyUsed = result.FreePlinkoBountyUsed
        });
    }
}
