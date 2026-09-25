using Microsoft.AspNetCore.Mvc;
using SportLinea.Models;

namespace SportLinea.Admin.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        if (User.IsInRole(Roles.SuperUser))
            return RedirectToAction("Index", "Admin");

        if (User.IsInRole(Roles.Bookmaker))
            return RedirectToAction("Index", "Bookmaker");

        return RedirectToAction("Login", "Account");
    }
}
