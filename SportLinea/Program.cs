using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SportLinea.Data;
using SportLinea.Models;
using SportLinea.Services;

var builder = WebApplication.CreateBuilder(args);

if (args is ["--zeus-sim"])
{
    var rtp = ZeusVsHadesConfig.SimulateRtp();
    Console.WriteLine($"Theoretical max: {ZeusVsHadesConfig.GetTheoreticalMaxWinMultiplier():F0}x");
    Console.WriteLine($"Simulated RTP: {rtp * 100m:F2}%");
    return;
}

if (args is ["--plinko-sim"])
{
    var rtp = PlinkoWildWestConfig.SimulateRtp();
    Console.WriteLine($"Theoretical max: {PlinkoWildWestConfig.GetTheoreticalMaxWinMultiplier():F0}x");
    Console.WriteLine($"Bounty max (purchased): {PlinkoWildWestConfig.GetTheoreticalMaxBountyMultiplierSum(purchased: true)}x");
    Console.WriteLine($"Simulated RTP: {rtp * 100m:F2}%");
    return;
}

if (args is ["--camelot-collector-test"])
{
    for (var seed = 0; seed < 5000; seed++)
    {
        var session = CamelotGoldConfig.RunSession(1m, bonusBuy: true, rng: new Random(seed));
        var bonus = session.Phases.FirstOrDefault(p => p.Phase == "bonus");
        if (bonus == null) continue;
        var clears = bonus.Steps.Count(s => s.Type is "clear" or "clearAll");
        var collectors = bonus.Collectors.Count;
        if (collectors >= 2 && clears >= 2)
        {
            Console.WriteLine($"seed={seed} collectors={collectors} clears={clears} steps={bonus.Steps.Count}");
            break;
        }
    }

    return;
}

if (args is ["--fruitland-sim"])
{
    var rtp = Fruitland1000Config.SimulateRtp();
    var buy10 = Fruitland1000Config.SimulateBonusBuyRtp(10);
    var buy15 = Fruitland1000Config.SimulateBonusBuyRtp(15);
    var buy20 = Fruitland1000Config.SimulateBonusBuyRtp(20);
    Console.WriteLine($"Simulated RTP (base spin): {rtp * 100m:F2}%");
    Console.WriteLine($"Simulated RTP (bonus buy 10): {buy10 * 100m:F2}%");
    Console.WriteLine($"Simulated RTP (bonus buy 15): {buy15 * 100m:F2}%");
    Console.WriteLine($"Simulated RTP (bonus buy 20): {buy20 * 100m:F2}%");
    return;
}

if (args is ["--camelot-sim"])
{
    var rtp = CamelotGoldConfig.SimulateRtp();
    var buyRtp = CamelotGoldConfig.SimulateBonusBuyRtp();
    Console.WriteLine($"Simulated RTP (base spin): {rtp * 100m:F2}%");
    Console.WriteLine($"Simulated RTP (bonus buy): {buyRtp * 100m:F2}%");
    return;
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SportLinea.Client";
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddScoped<IActionLogService, ActionLogService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IBonusService, BonusService>();
builder.Services.AddScoped<IBetService, BetService>();
builder.Services.AddScoped<IRouletteService, RouletteService>();
builder.Services.AddScoped<IPlinkoService, PlinkoService>();
builder.Services.AddScoped<IZeusHadesService, ZeusHadesService>();
builder.Services.AddScoped<ICamelotGoldService, CamelotGoldService>();
builder.Services.AddScoped<IFruitland1000Service, Fruitland1000Service>();
builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();
builder.Services.AddScoped<ILineSelectionService, LineSelectionService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddMemoryCache();

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(app.Services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
