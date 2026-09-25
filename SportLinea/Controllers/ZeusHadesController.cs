using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SportLinea.Models;
using SportLinea.Services;

namespace SportLinea.Controllers;

[Authorize(Roles = Roles.Player)]
public class ZeusHadesController : Controller
{
    private readonly IZeusHadesService _zeusHadesService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ZeusHadesController(IZeusHadesService zeusHadesService, UserManager<ApplicationUser> userManager)
    {
        _zeusHadesService = zeusHadesService;
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

        var (success, message, result) = await _zeusHadesService.SpinAsync(user.Id, amount);
        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message,
            balance = result.NewBalance,
            grid = result.Grid,
            gridKeys = result.GridKeys,
            gridEmojis = result.GridEmojis,
            confrontationQueued = result.ConfrontationQueued
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reveal([FromForm] bool isRow, [FromForm] int index)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Слот недоступен в вашей стране проживания." });

        var (success, message, result) = await _zeusHadesService.RevealLineAsync(user.Id, isRow, index);
        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message,
            index = result.Index,
            isRow = result.IsRow,
            multiplier = result.Multiplier,
            revealedRows = result.RevealedRows,
            revealedCols = result.RevealedCols,
            allRevealed = result.AllRevealed
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Слот недоступен в вашей стране проживания." });

        var (success, message, result) = await _zeusHadesService.CompleteRoundAsync(user.Id);
        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message = result.Message,
            winnings = result.Winnings,
            balance = result.NewBalance,
            confrontationStarted = result.ConfrontationStarted,
            hits = result.Hits
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confrontation()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Слот недоступен в вашей стране проживания." });

        var (success, message, result) = await _zeusHadesService.PlayConfrontationRoundAsync(user.Id);
        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message = result.Message,
            round = result.Round,
            totalRounds = result.TotalRounds,
            roundWinnings = result.RoundWinnings,
            totalBonusWinnings = result.TotalBonusWinnings,
            bonusComplete = result.BonusComplete,
            balance = result.NewBalance,
            grid = result.Grid,
            gridEmojis = result.GridEmojis,
            rowMultipliers = result.RowMultipliers,
            colMultipliers = result.ColMultipliers,
            hits = result.Hits
        });
    }
}
