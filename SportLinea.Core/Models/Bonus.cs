namespace SportLinea.Models;

public class Bonus
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BonusType Type { get; set; } = BonusType.Manual;
    public decimal Amount { get; set; }
    public int UsesRemaining { get; set; } = 1;
    public int UsesTotal { get; set; } = 1;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime EndDate { get; set; }
    public BonusStatus Status { get; set; } = BonusStatus.Pending;
    public string? BookmakerComment { get; set; }
    public string? AwardedByUserId { get; set; }

    public ApplicationUser Player { get; set; } = null!;
    public ApplicationUser? AwardedBy { get; set; }
}

public static class BonusRules
{
    public const int BetsPerFreeBet = 10;
    public const decimal FreeBetAmount = 2000m;
    public const int SpinsPerRouletteBonus = 100;
    public const int FreeRouletteSpinCount = 10;
    public const decimal FreeRouletteSpinAmount = 200m;
    public const int BountyGamesPerPlinkoBonus = 10;
    public const decimal FreePlinkoBountyStake = 200m;
    public const decimal FreePlinkoBountyValue = 1000m;
    public const decimal RegistrationFreeBetAmount = 4500m;
    public const int RegistrationFreeSpinCount = 100;
    public const decimal RegistrationFreeSpinAmount = 20m;
    public const int ValidityDays = 30;

    public const string RegistrationBonusMarker = "Приветственн";
    public const string RouletteMilestoneMarker = "Crazy Time:";
    public const string PlinkoMilestoneMarker = "Bingo Plinko:";
}

public static class BonusMessages
{
    public static string NewFreeBetAwarded() =>
        $"🎁 Новый бонус! За {BonusRules.BetsPerFreeBet} ставок вы получили фрибет {BonusRules.FreeBetAmount:N0} ₽. Активируйте в разделе «Бонусы».";

    public static string NewRouletteSpinsAwarded() =>
        $"🎁 Новый бонус Crazy Time! {BonusRules.FreeRouletteSpinCount} бесплатных спинов по {BonusRules.FreeRouletteSpinAmount:N0} ₽. Активируйте в разделе «Бонусы».";

    public static string NewPlinkoBountyAwarded() =>
        $"🎁 Новый бонус Bingo Plinko! Бесплатная Bounty Hunter по {BonusRules.FreePlinkoBountyStake:N0} ₽ (номинал {BonusRules.FreePlinkoBountyValue:N0} ₽). Активируйте в разделе «Бонусы».";

    public static string Activated(string description) =>
        $"Бонус активирован: {description}";

    public static string RegistrationBonusesAwarded(bool includesCrazyTime) =>
        includesCrazyTime
            ? $"🎁 Добро пожаловать! Вы получили приветственный фрибет {BonusRules.RegistrationFreeBetAmount:N0} ₽ " +
              $"и {BonusRules.RegistrationFreeSpinCount} фриспинов Crazy Time по {BonusRules.RegistrationFreeSpinAmount:N0} ₽. " +
              "Активируйте в разделе «Бонусы»."
            : $"🎁 Добро пожаловать! Вы получили приветственный фрибет {BonusRules.RegistrationFreeBetAmount:N0} ₽. " +
              "Активируйте в разделе «Бонусы».";

    public static string BookmakerFreeBetAwarded(decimal amount, string? comment)
    {
        var text = $"🎁 Букмекер начислил вам фрибет {amount:N0} ₽. Активируйте в разделе «Бонусы».";
        return string.IsNullOrWhiteSpace(comment) ? text : $"{text} Комментарий: {comment.Trim()}";
    }

    public static string BookmakerFreeSpinsAwarded(int count, decimal spinAmount, string? comment)
    {
        var text = $"🎁 Букмекер начислил вам {count} фриспинов Crazy Time по {spinAmount:N0} ₽. Активируйте в разделе «Бонусы».";
        return string.IsNullOrWhiteSpace(comment) ? text : $"{text} Комментарий: {comment.Trim()}";
    }
}
