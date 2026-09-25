using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;
using SportLinea.Models.ViewModels;
using SportLinea.Services;

namespace SportLinea.Controllers;

[Authorize(Roles = Roles.SuperUser)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActionLogService _actionLogService;
    private readonly IWebHostEnvironment _env;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IActionLogService actionLogService,
        IWebHostEnvironment env)
    {
        _context = context;
        _userManager = userManager;
        _actionLogService = actionLogService;
        _env = env;
    }

    public IActionResult Index() => View();

    public async Task<IActionResult> Bookmakers()
    {
        var users = await GetUsersInRoleAsync(Roles.Bookmaker);
        return View("StaffList", users);
    }

    [HttpGet]
    public async Task<IActionResult> BlockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (!await CanManageBookmakerAsync(user))
            return Forbid();

        if (user.Status == UserStatus.Blocked)
            return RedirectToAction(nameof(Bookmakers));

        return View(new BlockStaffViewModel
        {
            UserId = user.Id,
            UserName = user.FullName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BlockUser(BlockStaffViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null) return NotFound();

        if (!await CanManageBookmakerAsync(user))
            return Forbid();

        if (!ModelState.IsValid)
        {
            model.UserName = user.FullName;
            return View(model);
        }

        var admin = await _userManager.GetUserAsync(User);
        user.Status = UserStatus.Blocked;
        user.BlockReason = model.BlockReason.Trim();
        user.BlockedByUserId = admin?.Id;
        await _userManager.UpdateAsync(user);

        await _actionLogService.LogAsync(admin?.Id,
            $"Блокировка букмекера {user.Email}. Причина: {user.BlockReason}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = "Учётная запись заблокирована";
        return RedirectToAction(nameof(Bookmakers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (!await CanManageBookmakerAsync(user))
            return Forbid();

        user.Status = UserStatus.Active;
        user.BlockReason = null;
        user.BlockedByUserId = null;
        await _userManager.UpdateAsync(user);

        var admin = await _userManager.GetUserAsync(User);
        await _actionLogService.LogAsync(admin?.Id,
            $"Разблокировка букмекера {user.Email}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = "Учётная запись разблокирована";
        return RedirectToAction(nameof(Bookmakers));
    }

    [HttpGet]
    public IActionResult CreateBookmaker() => View(new CreateBookmakerViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBookmaker(CreateBookmakerViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError(nameof(model.Email), "Пользователь уже существует");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Balance = 0,
            Status = UserStatus.Active
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, Roles.Bookmaker);

        var admin = await _userManager.GetUserAsync(User);
        await _actionLogService.LogAsync(admin?.Id, $"Создание учётной записи букмекера: {model.Email}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = "Букмекер создан";
        return RedirectToAction(nameof(Bookmakers));
    }

    public async Task<IActionResult> ActionLog()
    {
        var logs = await _context.ActionLogs
            .Include(l => l.User)
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .ToListAsync();
        return View(logs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Backup()
    {
        var backupDir = Path.Combine(_env.ContentRootPath, "Backups");
        Directory.CreateDirectory(backupDir);

        var fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var filePath = Path.Combine(backupDir, fileName);

        var data = new
        {
            Users = await _context.Users.IgnoreQueryFilters().Select(u => new { u.Id, u.Email, u.Balance, u.Status }).ToListAsync(),
            Events = await _context.SportEvents.IgnoreQueryFilters().ToListAsync(),
            Bets = await _context.Bets.ToListAsync(),
            CreatedAt = DateTime.UtcNow
        };

        await System.IO.File.WriteAllTextAsync(filePath, System.Text.Json.JsonSerializer.Serialize(data));

        var admin = await _userManager.GetUserAsync(User);
        await _actionLogService.LogAsync(admin?.Id, $"Резервное копирование: {fileName}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Резервная копия создана: {fileName}";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Backups()
    {
        var backupDir = Path.Combine(_env.ContentRootPath, "Backups");
        if (!Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);

        var files = Directory.GetFiles(backupDir, "*.json")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        return View(files);
    }

    private async Task<List<ApplicationUser>> GetUsersInRoleAsync(string role) =>
        await (
            from user in _context.Users.Include(u => u.BlockedBy)
            join userRole in _context.UserRoles on user.Id equals userRole.UserId
            join r in _context.Roles on userRole.RoleId equals r.Id
            where r.Name == role
            orderby user.LastName, user.FirstName
            select user).Distinct().ToListAsync();

    private async Task<bool> CanManageBookmakerAsync(ApplicationUser target) =>
        await _userManager.IsInRoleAsync(target, Roles.Bookmaker);
}
