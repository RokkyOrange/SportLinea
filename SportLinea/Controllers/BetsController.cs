using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;

namespace SportLinea.Controllers;

[Authorize(Roles = Roles.Player)]
public class BetsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public BetsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> History(BetStatus? status, DateTime? from, DateTime? to)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var query = _context.Bets
            .Include(b => b.SportEvent)
            .Include(b => b.Coefficient)
            .Where(b => b.PlayerId == user.Id);

        if (status.HasValue)
            query = query.Where(b => b.Status == status);

        if (from.HasValue)
            query = query.Where(b => b.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(b => b.CreatedAt <= to.Value.AddDays(1));

        var bets = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();

        ViewBag.Status = status;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");

        return View(bets);
    }
}
