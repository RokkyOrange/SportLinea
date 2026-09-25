namespace SportLinea.Models;

public enum RouletteBonusKind
{
    None,
    Crazy,
    HotSpins
}

public class RouletteSegment
{
    public int Index { get; init; }
    public string Label { get; init; } = string.Empty;
    public decimal Multiplier { get; init; }
    public string Color { get; init; } = string.Empty;
    public int Weight { get; init; } = 1;
    public RouletteBonusKind BonusKind { get; init; } = RouletteBonusKind.None;
    public bool IsBonus => BonusKind != RouletteBonusKind.None;
}

public static class CrazyTimeWheel
{
    public const int SegmentCount = 8;
    public const int HotSpinsFreeSpinCount = 5;
    public const int BonusBuyPriceMultiplier = 10;

    public static decimal GetBonusBuyPrice(decimal stakeAmount) =>
        stakeAmount * BonusBuyPriceMultiplier;

    public static readonly decimal[] AllowedStakes = [10, 20, 50, 100, 200, 500, 1000, 5000, 10000, 100000];

    public static bool IsAllowedStake(decimal amount) => AllowedStakes.Contains(amount);

    public static decimal GetDefaultStake(decimal balance) =>
        AllowedStakes.Where(s => s <= balance).DefaultIfEmpty(AllowedStakes[0]).Max();

    public static readonly RouletteSegment[] Segments =
    [
        new() { Index = 0, Label = "×0", Multiplier = 0m, Color = "#c0392b", Weight = 5 },
        new() { Index = 1, Label = "×2", Multiplier = 2m, Color = "#9b59b6", Weight = 2 },
        new() { Index = 2, Label = "×0", Multiplier = 0m, Color = "#c0392b", Weight = 5 },
        new() { Index = 3, Label = "HOT SPINS", Multiplier = 0m, Color = "#ff9f43", Weight = 1, BonusKind = RouletteBonusKind.HotSpins },
        new() { Index = 4, Label = "×0", Multiplier = 0m, Color = "#c0392b", Weight = 5 },
        new() { Index = 5, Label = "×3", Multiplier = 3m, Color = "#2ecc71", Weight = 1 },
        new() { Index = 6, Label = "×0", Multiplier = 0m, Color = "#c0392b", Weight = 5 },
        new() { Index = 7, Label = "CRAZY", Multiplier = 0m, Color = "#ff6b6b", Weight = 2, BonusKind = RouletteBonusKind.Crazy }
    ];

    public static readonly RouletteSegment[] CrazyBonusSegments =
    [
        new() { Index = 0, Label = "×1000", Multiplier = 1000m, Color = "#f5c518", Weight = 1 },
        new() { Index = 1, Label = "BUST", Multiplier = 0m, Color = "#1a1a2e", Weight = 212 }
    ];

    public static readonly RouletteSegment[] HotSpinsSegments =
    [
        new() { Index = 0, Label = "×0", Multiplier = 0m, Color = "#c0392b", Weight = 9 },
        new() { Index = 1, Label = "×5", Multiplier = 5m, Color = "#2ecc71", Weight = 2 },
        new() { Index = 2, Label = "×0", Multiplier = 0m, Color = "#e74c3c", Weight = 9 },
        new() { Index = 3, Label = "SENSATION", Multiplier = 25m, Color = "#f5c518", Weight = 1 }
    ];

    public static RouletteSegment PickRandom(RouletteSegment[] segments)
    {
        var total = segments.Sum(s => s.Weight);
        var roll = Random.Shared.Next(total);
        var cumulative = 0;
        foreach (var segment in segments)
        {
            cumulative += segment.Weight;
            if (roll < cumulative)
                return segment;
        }
        return segments[0];
    }

    public static RouletteSegment PickMain() => PickRandom(Segments);
    public static RouletteSegment PickCrazyBonus() => PickRandom(CrazyBonusSegments);
    public static RouletteSegment PickHotSpins() => PickRandom(HotSpinsSegments);
}

public class PendingRouletteBonus
{
    public string PlayerId { get; set; } = string.Empty;
    public decimal StakeAmount { get; set; }
    public RouletteBonusKind BonusKind { get; set; }
    public int SpinsRemaining { get; set; }
    public decimal TotalBonusWinnings { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
