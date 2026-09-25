using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SportLinea;
using SportLinea.Models;
using SportLinea.Models.ViewModels;
using SportLinea.Services;

namespace SportLinea.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IActionLogService _actionLogService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IActionLogService actionLogService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _actionLogService = actionLogService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Неверный email или пароль");
            return View(model);
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!passwordValid)
        {
            ModelState.AddModelError(string.Empty, "Неверный email или пароль");
            return View(model);
        }

        var isStaff = await _userManager.IsInRoleAsync(user, Roles.Bookmaker)
                      || await _userManager.IsInRoleAsync(user, Roles.SuperUser);
        if (!isStaff)
        {
            ModelState.AddModelError(string.Empty,
                $"Игроки входят на клиентском сайте: {SiteUrls.Client}");
            return View(model);
        }

        if (user.Status == UserStatus.Blocked)
        {
            ModelState.AddModelError(string.Empty, "Учётная запись заблокирована");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Неверный email или пароль");
            return View(model);
        }

        await _actionLogService.LogAsync(user.Id, "Авторизация в АРМ", HttpContext.Connection.RemoteIpAddress?.ToString());

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        if (await _userManager.IsInRoleAsync(user, Roles.SuperUser))
            return RedirectToAction("Index", "Admin");

        return RedirectToAction("Index", "Bookmaker");
    }

    [HttpGet]
    [ActionName("Logout")]
    public async Task<IActionResult> LogoutGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpPost]
    [ActionName("Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutPost()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();
}
