using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SportLinea.Models;
using SportLinea.Services;

namespace SportLinea.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, bool migrate = true)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (migrate)
            await context.Database.MigrateAsync();

        string[] roles = { Roles.Player, Roles.Bookmaker, Roles.SuperUser };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        if (await userManager.FindByEmailAsync("admin@sportlinea.ru") == null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@sportlinea.ru",
                Email = "admin@sportlinea.ru",
                EmailConfirmed = true,
                FirstName = "Админ",
                LastName = "Системный",
                Balance = 0,
                Status = UserStatus.Active
            };
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, Roles.SuperUser);
        }

        if (await userManager.FindByEmailAsync("bookmaker@sportlinea.ru") == null)
        {
            var bookmaker = new ApplicationUser
            {
                UserName = "bookmaker@sportlinea.ru",
                Email = "bookmaker@sportlinea.ru",
                EmailConfirmed = true,
                FirstName = "Иван",
                LastName = "Букмекеров",
                Balance = 0,
                Status = UserStatus.Active
            };
            await userManager.CreateAsync(bookmaker, "Book123!");
            await userManager.AddToRoleAsync(bookmaker, Roles.Bookmaker);
        }

        if (await userManager.FindByEmailAsync("player@sportlinea.ru") == null)
        {
            var player = new ApplicationUser
            {
                UserName = "player@sportlinea.ru",
                Email = "player@sportlinea.ru",
                EmailConfirmed = true,
                FirstName = "Пётр",
                LastName = "Игроков",
                Balance = 10000,
                Status = UserStatus.Active
            };
            await userManager.CreateAsync(player, "Player123!");
            await userManager.AddToRoleAsync(player, Roles.Player);
        }

        if (!await context.SportEvents.AnyAsync())
        {
            var events = CreateActiveDemoEvents().Take(LineRules.ActiveEventCapacity).ToList();
            events.Add(CreateCompletedDemoEvent());
            context.SportEvents.AddRange(events);
            await context.SaveChangesAsync();

            var completed = events.Last();
            completed.WinningCoefficientId = completed.Coefficients.First().Id;
            await context.SaveChangesAsync();
        }

        await BackfillLegacyEventsAsync(context);
        await SyncBonusTiersAsync(context);

        var bonusService = scope.ServiceProvider.GetRequiredService<IBonusService>();
        await bonusService.RepairRouletteMilestoneBonusesAsync();
        await bonusService.RepairPlinkoMilestoneBonusesAsync();
        await bonusService.EnsureRegistrationBonusesForExistingPlayersAsync();

        var misplacedDates = await context.SportEvents
            .Include(e => e.Bets)
            .Where(e => e.Status == EventStatus.Completed && e.StartDate > DateTime.UtcNow)
            .ToListAsync();

        foreach (var sportEvent in misplacedDates)
        {
            if (sportEvent.Bets.Any())
                sportEvent.StartDate = sportEvent.Bets.Max(b => b.CreatedAt);
        }

        if (misplacedDates.Count > 0)
            await context.SaveChangesAsync();
    }

    private static async Task SyncBonusTiersAsync(ApplicationDbContext context)
    {
        var players = await context.Users.ToListAsync();
        var updated = false;

        foreach (var player in players)
        {
            var betCount = await context.Bets.CountAsync(b => b.PlayerId == player.Id);

            var betTier = betCount / BonusRules.BetsPerFreeBet;

            if (player.BetBonusTier < betTier)
            {
                player.BetBonusTier = betTier;
                updated = true;
            }

        }

        if (updated)
            await context.SaveChangesAsync();
    }

    public static async Task<int> EnsureActiveLineAsync(ApplicationDbContext context) =>
        await EnsureActiveEventsAsync(context);

    private static async Task<int> EnsureActiveEventsAsync(ApplicationDbContext context)
    {
        var minActiveEvents = LineRules.ActiveEventCapacity;
        var activeCount = await context.SportEvents.CountAsync(e => e.Status == EventStatus.AcceptingBets);
        if (activeCount >= minActiveEvents)
            return 0;

        var needed = minActiveEvents - activeCount;
        var existingTitles = (await context.SportEvents
            .Select(e => e.Title)
            .ToListAsync())
            .ToHashSet(StringComparer.Ordinal);

        var templates = CreateActiveDemoEvents();
        var toAdd = new List<SportEvent>();
        var tour = 1;

        while (toAdd.Count < needed && tour <= 50)
        {
            foreach (var template in templates)
            {
                if (toAdd.Count >= needed)
                    break;

                var title = tour == 1 ? template.Title : $"{template.Title} · тур {tour}";
                if (existingTitles.Contains(title))
                    continue;

                toAdd.Add(CloneEvent(template, title, toAdd.Count));
                existingTitles.Add(title);
            }

            tour++;
        }

        if (toAdd.Count == 0)
            return 0;

        context.SportEvents.AddRange(toAdd);
        await context.SaveChangesAsync();
        return toAdd.Count;
    }

    private static SportEvent CloneEvent(SportEvent template, string title, int offset) =>
        new()
        {
            SportCategory = template.SportCategory,
            SportType = template.SportType,
            Title = title,
            StartDate = DateTime.Now.AddDays(1 + offset % 7),
            Status = EventStatus.AcceptingBets,
            Coefficients = template.Coefficients.Select(c => new Coefficient
            {
                OutcomeDescription = c.OutcomeDescription,
                Value = c.Value
            }).ToList()
        };

    private static async Task BackfillLegacyEventsAsync(ApplicationDbContext context)
    {
        var templateByTitle = CreateActiveDemoEvents()
            .ToDictionary(e => e.Title, e => e);

        templateByTitle[CreateCompletedDemoEvent().Title] = CreateCompletedDemoEvent();

        var events = await context.SportEvents.ToListAsync();
        var updated = false;

        foreach (var sportEvent in events)
        {
            if (templateByTitle.TryGetValue(sportEvent.Title, out var template))
            {
                if (sportEvent.SportCategory != template.SportCategory)
                {
                    sportEvent.SportCategory = template.SportCategory;
                    updated = true;
                }

                if (sportEvent.SportType != template.SportType)
                {
                    sportEvent.SportType = template.SportType;
                    updated = true;
                }

                continue;
            }

            if (!string.IsNullOrEmpty(sportEvent.SportCategory))
                continue;

            sportEvent.SportCategory = sportEvent.SportType switch
            {
                "CS2" or "Dota 2" or "League of Legends" or "Valorant" => SportCategories.Esports,
                _ when sportEvent.Title.Contains("ЛФЛ", StringComparison.OrdinalIgnoreCase)
                    || sportEvent.Title.Contains("Студлига", StringComparison.OrdinalIgnoreCase)
                    || sportEvent.Title.Contains("Корпоративная", StringComparison.OrdinalIgnoreCase)
                    => SportCategories.Amateur,
                _ => SportCategories.Professional
            };
            updated = true;
        }

        if (updated)
            await context.SaveChangesAsync();
    }

    private static List<SportEvent> CreateActiveDemoEvents() => new()
    {
        CreateEvent(SportCategories.Professional, "Футбол", "РПЛ: Зенит — Динамо", 1,
            ("Победа Зенит", 2.05m), ("Ничья", 3.30m), ("Победа Динамо", 3.40m)),
        CreateEvent(SportCategories.Professional, "Хоккей", "КХЛ: ЦСКА — Локомотив", 2,
            ("Победа ЦСКА", 1.90m), ("Ничья (ОТ)", 4.20m), ("Победа Локомотив", 3.50m)),
        CreateEvent(SportCategories.Professional, "Теннис", "ATP: Синнер — Алькарас", 3,
            ("Победа Синнер", 1.75m), ("Победа Алькарас", 2.10m)),
        CreateEvent(SportCategories.Professional, "Баскетбол", "NBA: Лейкерс — Селтикс", 1,
            ("Победа Лейкерс", 1.85m), ("Победа Селтикс", 1.95m)),
        CreateEvent(SportCategories.Professional, "ММА", "UFC 310: Махачев — Оливейра", 4,
            ("Победа Махачев", 1.65m), ("Победа Оливейра", 2.25m)),
        CreateEvent(SportCategories.Amateur, "Футбол", "ЛФЛ: Спартак-люб — Факел-люб", 2,
            ("Победа Спартак", 2.40m), ("Ничья", 3.10m), ("Победа Факел", 2.80m)),
        CreateEvent(SportCategories.Amateur, "Баскетбол", "Студлига: МГУ — ВШЭ", 3,
            ("Победа МГУ", 1.70m), ("Победа ВШЭ", 2.05m)),
        CreateEvent(SportCategories.Amateur, "Волейбол", "Корпоративная лига: Газпром — Лукойл", 5,
            ("Победа Газпром", 1.80m), ("Победа Лукойл", 1.90m)),
        CreateEvent(SportCategories.Esports, "CS2", "PGL Major: Spirit — NAVI", 1,
            ("Победа Spirit", 1.55m), ("Победа NAVI", 2.35m)),
        CreateEvent(SportCategories.Esports, "Dota 2", "The International: BetBoom — Liquid", 2,
            ("Победа BetBoom", 1.90m), ("Победа Liquid", 1.85m)),
        CreateEvent(SportCategories.Esports, "League of Legends", "Worlds: T1 — Gen.G", 3,
            ("Победа T1", 2.00m), ("Победа Gen.G", 1.75m)),
        CreateEvent(SportCategories.Esports, "Valorant", "VCT Masters: Fnatic — LOUD", 4,
            ("Победа Fnatic", 1.60m), ("Победа LOUD", 2.20m))
    };

    private static SportEvent CreateEvent(
        string category,
        string sportType,
        string title,
        int daysFromNow,
        params (string Outcome, decimal Value)[] outcomes)
    {
        return new SportEvent
        {
            SportCategory = category,
            SportType = sportType,
            Title = title,
            StartDate = DateTime.Now.AddDays(daysFromNow),
            Status = EventStatus.AcceptingBets,
            Coefficients = outcomes.Select(o => new Coefficient
            {
                OutcomeDescription = o.Outcome,
                Value = o.Value
            }).ToList()
        };
    }

    private static SportEvent CreateCompletedDemoEvent() => new()
    {
        SportCategory = SportCategories.Professional,
        SportType = "Футбол",
        Title = "АПЛ: Арсенал — Челси",
        StartDate = DateTime.Now.AddDays(-1),
        Status = EventStatus.Completed,
        Result = "2:1",
        Coefficients = new List<Coefficient>
        {
            new() { OutcomeDescription = "Победа Арсенал", Value = 2.00m },
            new() { OutcomeDescription = "Ничья", Value = 3.30m },
            new() { OutcomeDescription = "Победа Челси", Value = 3.50m }
        }
    };
}
