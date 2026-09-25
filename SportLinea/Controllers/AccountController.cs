using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SportLinea.Models;
using SportLinea.Models.ViewModels;
using SportLinea.Services;
using SportLinea;

namespace SportLinea.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly INotificationService _notificationService;
    private readonly IActionLogService _actionLogService;
    private readonly IBonusService _bonusService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        INotificationService notificationService,
        IActionLogService actionLogService,
        IBonusService bonusService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _notificationService = notificationService;
        _actionLogService = actionLogService;
        _bonusService = bonusService;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError(nameof(model.Email), "Пользователь с таким email уже существует");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Patronymic = model.Patronymic,
            Balance = 0,
            Status = UserStatus.Active,
            RegistrationDate = DateTime.UtcNow,
            CountryOfResidence = model.CountryOfResidence
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, Roles.Player);

        await _bonusService.AwardRegistrationBonusesAsync(user.Id);

        await _notificationService.SendAsync(user.Id, NotificationType.Registration,
            "Добро пожаловать в БК «СпортЛиния»! Ваша учётная запись успешно создана.");

        await _notificationService.SendEmailAsync(user.Email!,
            "Регистрация в БК «СпортЛиния»",
            $"Здравствуйте, {user.FirstName}! Вы успешно зарегистрированы в системе «СпортЛиния».");

        await _actionLogService.LogAsync(user.Id, "Регистрация нового игрока", HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Bonus"] = BonusMessages.RegistrationBonusesAwarded(
            CountryPolicy.AllowsCrazyTime(user.CountryOfResidence));
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Profile");
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

        if (user.Status == UserStatus.Blocked)
        {
            ApplicationUser? bookmaker = null;
            if (!string.IsNullOrEmpty(user.BlockedByUserId))
                bookmaker = await _userManager.FindByIdAsync(user.BlockedByUserId);

            ViewBag.BlockedAccount = new BlockedAccountInfoViewModel
            {
                Reason = string.IsNullOrWhiteSpace(user.BlockReason) ? "Причина не указана" : user.BlockReason,
                BookmakerName = bookmaker?.FullName ?? "Администратор БК «СпортЛиния»",
                BookmakerEmail = bookmaker?.Email ?? "admin@sportlinea.ru"
            };
            return View(model);
        }

        var isStaff = await _userManager.IsInRoleAsync(user, Roles.Bookmaker)
                      || await _userManager.IsInRoleAsync(user, Roles.SuperUser);
        if (isStaff)
        {
            ModelState.AddModelError(string.Empty, "Неверный email или пароль");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Неверный email или пароль");
            return View(model);
        }

        await _actionLogService.LogAsync(user.Id, "Авторизация", HttpContext.Connection.RemoteIpAddress?.ToString());

        if (user.BalanceResetNoticePending)
        {
            TempData["BalanceReset"] = "Ваш счет был обнулен.";
            user.BalanceResetNoticePending = false;
            await _userManager.UpdateAsync(user);
        }

        return await RedirectAfterLoginAsync(user, returnUrl);
    }

    private static readonly string[] PostOnlyReturnUrlPrefixes =
    [
        "/Profile/ActivateBonus",
        "/Account/Logout",
        "/Roulette/Spin",
        "/Roulette/BonusSpin",
        "/Roulette/BuyBonus",
        "/Plinko/Spin",
        "/Plinko/Pick",
        "/Plinko/BountyShoot",
        "/Plinko/BuyBounty",
        "/Line/Refresh",
        "/Events/PlaceBet"
    ];

    private async Task<IActionResult> RedirectAfterLoginAsync(ApplicationUser user, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) &&
            Url.IsLocalUrl(returnUrl) &&
            await IsAllowedReturnUrlAsync(user, returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Profile");
    }

    private async Task<bool> IsAllowedReturnUrlAsync(ApplicationUser user, string returnUrl)
    {
        var path = returnUrl.Split('?', '#')[0];

        if (PostOnlyReturnUrlPrefixes.Any(prefix =>
                path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (path.StartsWith("/Profile", StringComparison.OrdinalIgnoreCase) &&
            !await _userManager.IsInRoleAsync(user, Roles.Player))
        {
            return false;
        }

        if (path.StartsWith("/Bookmaker", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    [HttpGet]
    [ActionName("Logout")]
    public async Task<IActionResult> LogoutGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            await _signInManager.SignOutAsync();

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ActionName("Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutPost()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
