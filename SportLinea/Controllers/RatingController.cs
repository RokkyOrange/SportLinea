using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;
using SportLinea.Models.ViewModels;

namespace SportLinea.Controllers;

public class RatingController : Controller
{
    private readonly ApplicationDbContext _context;

    public RatingController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var players = await _context.Users
            .Where(u => u.Bets.Any())
            .Select(u => new PlayerRatingViewModel
            {
                PlayerId = u.Id,
                PlayerName = u.LastName + " " + u.FirstName,
                Wins = u.Bets.Count(b => b.Status == BetStatus.Won),
                Losses = u.Bets.Count(b => b.Status == BetStatus.Lost),
                TotalWinnings = u.Bets.Where(b => b.Status == BetStatus.Won).Sum(b => b.Winnings)
            })
            .OrderByDescending(p => p.TotalWinnings)
            .ThenByDescending(p => p.Wins)
            .ToListAsync();

        return View(players);
    }
}
