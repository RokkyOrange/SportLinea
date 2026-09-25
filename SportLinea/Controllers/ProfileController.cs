using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;
using SportLinea.Models.ViewModels;
using SportLinea.Services;

namespace SportLinea.Controllers;

[Authorize(Roles = Roles.Player)]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActionLogService _actionLogService;
    private readonly IBonusService _bonusService;
    private readonly IWithdrawalService _withdrawalService;
    private readonly INotificationService _notificationService;

    public ProfileController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IActionLogService actionLogService,
        IBonusService bonusService,
        IWithdrawalService withdrawalService,
        INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _actionLogService = actionLogService;
        _bonusService = bonusService;
        _withdrawalService = withdrawalService;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var stats = await GetStatsAsync(user.Id);
        ViewBag.Stats = stats;
        ViewBag.Notifications = await _context.Notifications
            .Where(n => n.UserId == user.Id)
            .OrderByDescending(n => n.SentAt)
            .Take(5)
            .ToListAsync();

        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        return View(new ProfileEditViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Patronymic = user.Patronymic,
            CountryOfResidence = user.CountryOfResidence
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProfileEditViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var previousCountry = user.CountryOfResidence;

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Patronymic = model.Patronymic;
        user.CountryOfResidence = model.CountryOfResidence;

        if (!string.IsNullOrEmpty(model.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }
        }

        await _userManager.UpdateAsync(user);
        await _actionLogService.LogAsync(user.Id, "Редактирование профиля", HttpContext.Connection.RemoteIpAddress?.ToString());

        if (previousCountry != user.CountryOfResidence)
        {
            if (CountryPolicy.AllowsCrazyTime(user.CountryOfResidence))
                TempData["Success"] = "Профиль обновлён. Наши игровые автоматы снова доступны.";
            else
                TempData["Success"] = "Профиль обновлён. Наши игровые автоматы недоступны для выбранной страны проживания.";
        }
        else
        {
            TempData["Success"] = "Профиль обновлён";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Deposit()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewBag.Balance = user.Balance;
        return View(new DepositViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deposit(DepositViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewBag.Balance = user.Balance;

        if (model.Amount <= 0)
            ModelState.AddModelError(nameof(model.Amount), "Укажите корректную сумму");

        if (model.Amount < DepositRules.MinAmount)
            ModelState.AddModelError(nameof(model.Amount), "Минимальная сумма пополнения — 1 000 ₽");

        if (!ModelState.IsValid)
            return View(model);

        user.Balance += model.Amount;
        await _userManager.UpdateAsync(user);

        _context.AccountOperations.Add(new AccountOperation
        {
            PlayerId = user.Id,
            OperationType = OperationType.Deposit,
            Amount = model.Amount,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await _notificationService.SendAsync(
            user.Id,
            NotificationType.BalanceDeposit,
            $"Ваш баланс пополнен на {model.Amount:N2} ₽.");

        await _actionLogService.LogAsync(user.Id, $"Пополнение счёта на {model.Amount:N2} ₽",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Счёт пополнен на {model.Amount:N2} ₽";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Withdraw()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewBag.Balance = user.Balance;
        return View(new WithdrawViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(WithdrawViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewBag.Balance = user.Balance;

        if (model.Amount <= 0)
            ModelState.AddModelError(nameof(model.Amount), "Укажите корректную сумму");

        if (model.Amount < WithdrawRules.MinAmount)
            ModelState.AddModelError(nameof(model.Amount), $"Минимальная сумма вывода — {WithdrawRules.MinAmount:N0} ₽");

        if (model.Amount > user.Balance)
            ModelState.AddModelError(nameof(model.Amount), "Недостаточно средств на счёте");

        if (!ModelState.IsValid)
            return View(model);

        var (success, message, flashKey) = await _withdrawalService.RequestWithdrawalAsync(user.Id, model.Amount);
        if (!success)
        {
            ModelState.AddModelError(nameof(model.Amount), message);
            return View(model);
        }

        await _actionLogService.LogAsync(user.Id, $"Заявка на вывод {model.Amount:N2} ₽",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData[flashKey] = message;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Stats()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewBag.CrazyTimeAllowed = CountryPolicy.AllowsCrazyTime(user.CountryOfResidence);
        return View(await GetStatsAsync(user.Id));
    }

    public async Task<IActionResult> Bonuses()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var bonuses = await _context.Bonuses
            .Where(b => b.PlayerId == user.Id)
            .OrderByDescending(b => b.StartDate)
            .ToListAsync();

        var betCount = await _context.Bets.CountAsync(b => b.PlayerId == user.Id);
        var spinCount = await _context.AccountOperations.CountAsync(o =>
            o.PlayerId == user.Id &&
            o.OperationType == OperationType.RouletteStake &&
            !o.IsFreeBonus);

        ViewBag.BetsUntilNextBonus = BonusRules.BetsPerFreeBet - betCount % BonusRules.BetsPerFreeBet;
        ViewBag.SpinsUntilNextBonus = BonusRules.SpinsPerRouletteBonus - spinCount % BonusRules.SpinsPerRouletteBonus;
        var bountyGameCount = await _context.AccountOperations.CountAsync(o =>
            o.PlayerId == user.Id &&
            o.OperationType == OperationType.PlinkoBountyGame &&
            !o.IsFreeBonus);
        ViewBag.BountyGamesUntilNextBonus =
            BonusRules.BountyGamesPerPlinkoBonus - bountyGameCount % BonusRules.BountyGamesPerPlinkoBonus;
        ViewBag.CrazyTimeAllowed = CountryPolicy.AllowsCrazyTime(user.CountryOfResidence);

        return View(bonuses);
    }

    [HttpGet]
    [ActionName("ActivateBonus")]
    public IActionResult ActivateBonusGet(int id) => RedirectToAction(nameof(Bonuses));

    [HttpPost]
    [ActionName("ActivateBonus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateBonusPost(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var (success, message) = await _bonusService.ActivateAsync(user.Id, id);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Bonuses));
    }

    public async Task<IActionResult> Notifications()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var notifications = await _context.Notifications
            .Where(n => n.UserId == user.Id)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();

        foreach (var n in notifications.Where(n => n.Status != NotificationStatus.Read))
            n.Status = NotificationStatus.Read;
        await _context.SaveChangesAsync();

        return View(notifications);
    }

    private async Task<PlayerStatsViewModel> GetStatsAsync(string userId)
    {
        var bets = await _context.Bets.Where(b => b.PlayerId == userId).ToListAsync();
        var rouletteOps = await _context.AccountOperations
            .Where(o => o.PlayerId == userId &&
                        (o.OperationType == OperationType.RouletteStake ||
                         o.OperationType == OperationType.RouletteWin ||
                         o.OperationType == OperationType.RouletteSpin))
            .ToListAsync();

        var spinOps = rouletteOps.Where(o => o.OperationType == OperationType.RouletteSpin).ToList();
        var rouletteStakes = rouletteOps.Where(o => o.OperationType == OperationType.RouletteStake).ToList();
        var rouletteWins = rouletteOps.Where(o => o.OperationType == OperationType.RouletteWin).ToList();

        var plinkoOps = await _context.AccountOperations
            .Where(o => o.PlayerId == userId &&
                        (o.OperationType == OperationType.PlinkoStake ||
                         o.OperationType == OperationType.PlinkoWin ||
                         o.OperationType == OperationType.PlinkoSpin))
            .ToListAsync();

        var plinkoSpinOps = plinkoOps.Where(o => o.OperationType == OperationType.PlinkoSpin).ToList();
        var plinkoStakes = plinkoOps.Where(o => o.OperationType == OperationType.PlinkoStake).ToList();
        var plinkoWins = plinkoOps.Where(o => o.OperationType == OperationType.PlinkoWin).ToList();

        var zeusOps = await _context.AccountOperations
            .Where(o => o.PlayerId == userId &&
                        (o.OperationType == OperationType.ZeusHadesStake ||
                         o.OperationType == OperationType.ZeusHadesWin ||
                         o.OperationType == OperationType.ZeusHadesSpin))
            .ToListAsync();

        var zeusSpinOps = zeusOps.Where(o => o.OperationType == OperationType.ZeusHadesSpin).ToList();
        var zeusStakes = zeusOps.Where(o => o.OperationType == OperationType.ZeusHadesStake).ToList();
        var zeusWins = zeusOps.Where(o => o.OperationType == OperationType.ZeusHadesWin).ToList();

        var totalSpent = rouletteStakes
            .Where(o => !o.IsFreeBonus || o.IsBonusPurchase)
            .Sum(o => o.Amount);

        var totalWon = rouletteWins.Sum(o => o.Amount);
        var wonHotSpins = rouletteWins
            .Where(o => o.RouletteSpinKind == RouletteSpinKind.HotSpins)
            .Sum(o => o.Amount);
        var wonCrazy = rouletteWins
            .Where(o => o.RouletteSpinKind == RouletteSpinKind.CrazyBonus)
            .Sum(o => o.Amount);

        var plinkoSpent = plinkoStakes
            .Where(o => !o.IsFreeBonus || o.IsBonusPurchase)
            .Sum(o => o.Amount);
        var plinkoTotalWon = plinkoWins.Sum(o => o.Amount);
        var plinkoWonBounty = plinkoWins
            .Where(o => o.PlinkoSpinKind == PlinkoSpinKind.BountyHunter)
            .Sum(o => o.Amount);
        var maxBountyMultiplier = plinkoWins
            .Where(o => o.PlinkoBountyMultiplier.HasValue)
            .Select(o => o.PlinkoBountyMultiplier!.Value)
            .DefaultIfEmpty(0)
            .Max();

        var zeusSpent = zeusStakes.Sum(o => o.Amount);
        var zeusWonZeus = zeusWins
            .Where(o => o.ZeusSpinKind == ZeusSpinKind.Main)
            .Sum(o => o.ZeusSideWinAmount ?? 0m);
        var zeusWonHades = zeusWins
            .Where(o => o.ZeusSpinKind == ZeusSpinKind.Main)
            .Sum(o => o.HadesSideWinAmount ?? 0m);
        var zeusWonConfrontation = zeusWins
            .Where(o => o.ZeusSpinKind == ZeusSpinKind.Confrontation)
            .Sum(o => o.Amount);

        var camelotOps = await _context.AccountOperations
            .Where(o => o.PlayerId == userId &&
                        (o.OperationType == OperationType.CamelotStake ||
                         o.OperationType == OperationType.CamelotWin ||
                         o.OperationType == OperationType.CamelotSpin))
            .ToListAsync();

        var camelotSpinOps = camelotOps.Where(o => o.OperationType == OperationType.CamelotSpin).ToList();
        var camelotStakes = camelotOps.Where(o => o.OperationType == OperationType.CamelotStake).ToList();
        var camelotWins = camelotOps.Where(o => o.OperationType == OperationType.CamelotWin).ToList();
        var camelotSpent = camelotStakes.Sum(o => o.Amount);
        var camelotTotalWon = camelotWins.Sum(o => o.Amount);
        var camelotWonBonus = camelotWins
            .Where(o => o.CamelotSpinKind == CamelotSpinKind.Bonus)
            .Sum(o => o.Amount);
        var maxCollectorNominal = camelotSpinOps
            .Where(o => o.CamelotCollectorNominal.HasValue)
            .Select(o => o.CamelotCollectorNominal!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return new PlayerStatsViewModel
        {
            TotalBets = bets.Count,
            Wins = bets.Count(b => b.Status == BetStatus.Won),
            Losses = bets.Count(b => b.Status == BetStatus.Lost),
            Pending = bets.Count(b => b.Status == BetStatus.Accepted),
            TotalWinnings = bets.Where(b => b.Status == BetStatus.Won).Sum(b => b.Winnings),
            TotalStaked = bets.Sum(b => b.Amount),
            Roulette = new RouletteStatsViewModel
            {
                TotalSpins = spinOps.Count,
                SuccessfulSpins = spinOps.Count(s => s.RouletteSpinWon),
                TotalSpent = totalSpent,
                TotalWon = totalWon,
                WonHotSpins = wonHotSpins,
                WonCrazy = wonCrazy
            },
            Plinko = new PlinkoStatsViewModel
            {
                TotalSpins = plinkoSpinOps.Count,
                SuccessfulSpins = plinkoSpinOps.Count(s => s.RouletteSpinWon),
                TotalSpent = plinkoSpent,
                TotalWon = plinkoTotalWon,
                WonBountyHunter = plinkoWonBounty,
                MaxBountyMultiplier = maxBountyMultiplier
            },
            Zeus = new ZeusStatsViewModel
            {
                TotalSpins = zeusSpinOps.Count,
                SuccessfulSpins = zeusSpinOps.Count(s => s.RouletteSpinWon),
                TotalSpent = zeusSpent,
                WonZeus = zeusWonZeus,
                WonHades = zeusWonHades,
                WonConfrontation = zeusWonConfrontation
            },
            Camelot = new CamelotStatsViewModel
            {
                TotalSpins = camelotSpinOps.Count,
                SuccessfulSpins = camelotSpinOps.Count(s => s.RouletteSpinWon),
                TotalSpent = camelotSpent,
                TotalWon = camelotTotalWon,
                WonBonus = camelotWonBonus,
                MaxCollectorNominal = maxCollectorNominal
            },
            Fruitland = await GetFruitlandStatsAsync(userId)
        };
    }

    private async Task<FruitlandStatsViewModel> GetFruitlandStatsAsync(string userId)
    {
        var ops = await _context.AccountOperations
            .Where(o => o.PlayerId == userId &&
                        (o.OperationType == OperationType.FruitlandStake ||
                         o.OperationType == OperationType.FruitlandWin ||
                         o.OperationType == OperationType.FruitlandSpin))
            .ToListAsync();

        var spinOps = ops.Where(o => o.OperationType == OperationType.FruitlandSpin).ToList();
        var stakes = ops.Where(o => o.OperationType == OperationType.FruitlandStake).ToList();
        var wins = ops.Where(o => o.OperationType == OperationType.FruitlandWin).ToList();

        return new FruitlandStatsViewModel
        {
            TotalSpins = spinOps.Count,
            SuccessfulSpins = spinOps.Count(s => s.RouletteSpinWon),
            TotalSpent = stakes.Sum(o => o.Amount),
            TotalWon = wins.Sum(o => o.Amount),
            WonBonus = wins.Where(o => o.FruitlandSpinKind == FruitlandSpinKind.Bonus).Sum(o => o.Amount),
            MaxGlobalMultiplier = spinOps
                .Where(o => o.FruitlandGlobalMultiplier.HasValue)
                .Select(o => o.FruitlandGlobalMultiplier!.Value)
                .DefaultIfEmpty(0)
                .Max()
        };
    }
}
