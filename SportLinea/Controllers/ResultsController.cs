using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;

namespace SportLinea.Controllers;

public class ResultsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ResultsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var results = await _context.SportEvents
            .Where(e => e.Status == EventStatus.Completed)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();

        return View(results);
    }
}
