using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SportLinea.Models;
using SportLinea.Services;
using System.Text.Json;

namespace SportLinea.Controllers;

[Authorize(Roles = Roles.Player)]
public class Fruitland1000Controller : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IFruitland1000Service _service;
    private readonly UserManager<ApplicationUser> _userManager;

    public Fruitland1000Controller(IFruitland1000Service service, UserManager<ApplicationUser> userManager)
    {
        _service = service;
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

        var (success, message, result) = await _service.SpinAsync(user.Id, amount);
        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message,
            balance = result.NewBalance,
            totalWinnings = result.TotalWinnings,
            phases = result.Phases
        }, JsonOptions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyBonus([FromForm] decimal amount, [FromForm] int spins)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Не авторизован" });

        if (!CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
            return Json(new { success = false, message = "Слот недоступен в вашей стране проживания." });

        var (success, message, result) = await _service.BuyBonusAsync(user.Id, amount, spins);
        if (!success || result == null)
            return Json(new { success = false, message });

        return Json(new
        {
            success = true,
            message,
            balance = result.NewBalance,
            totalWinnings = result.TotalWinnings,
            phases = result.Phases
        }, JsonOptions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnimationComplete()
    {
        var userId = _userManager.GetUserId(User);
        if (userId != null)
            await _service.CompleteAnimationAsync(userId);
        return Json(new { success = true });
    }
}
