namespace SportLinea.Models;

public enum ZeusSymbol
{
    Hearts,
    Spades,
    Diamonds,
    Clubs,
    Mountain,
    Dog,
    Cloud,
    Trident,
    Olympus
}

public enum ZeusGamePhase
{
    AwaitingLineReveal,
    Confrontation
}

public enum ZeusMultiplierPickSide
{
    Zeus,
    Hades
}

/// <summary>
/// Zeus vs Hades — сетка 5×5, высокая волатильность, ~95% RTP, теор. max ~5000× ставки.
/// </summary>
public static class ZeusVsHadesConfig
{
    public const int GridSize = 5;
    public const int ConfrontationRounds = 3;
    public const double ConfrontationChance = 0.04;
    public const double IntersectionScale = 3.48;
    public const int MaxLineMultiplier = 18;

    public static readonly decimal[] AllowedStakes = CrazyTimeWheel.AllowedStakes;

    public static bool IsAllowedStake(decimal amount) => AllowedStakes.Contains(amount);

    public static decimal GetDefaultStake(decimal balance) =>
        AllowedStakes.Where(s => s <= balance).DefaultIfEmpty(AllowedStakes[0]).Max();

    private static readonly (ZeusSymbol Symbol, int Weight)[] SymbolWeights =
    [
        (ZeusSymbol.Hearts, 310),
        (ZeusSymbol.Spades, 310),
        (ZeusSymbol.Diamonds, 185),
        (ZeusSymbol.Clubs, 185),
        (ZeusSymbol.Mountain, 52),
        (ZeusSymbol.Dog, 52),
        (ZeusSymbol.Cloud, 21),
        (ZeusSymbol.Trident, 21),
        (ZeusSymbol.Olympus, 4)
    ];

    private static readonly (int Multiplier, int Weight)[] MainLineWeights =
    [
        (0, 991),
        (1, 4),
        (3, 2),
        (6, 1),
        (15, 1),
        (18, 1)
    ];

    private static readonly (int Multiplier, int Weight)[] ConfrontationLineWeights =
    [
        (0, 620),
        (1, 200),
        (3, 95),
        (6, 45),
        (9, 22),
        (12, 10),
        (15, 5),
        (18, 3)
    ];

    public static decimal GetSymbolPayout(ZeusSymbol symbol) => symbol switch
    {
        ZeusSymbol.Hearts => 0.25m,
        ZeusSymbol.Spades => 0.25m,
        ZeusSymbol.Diamonds => 0.5m,
        ZeusSymbol.Clubs => 0.5m,
        ZeusSymbol.Mountain => 2m,
        ZeusSymbol.Dog => 2m,
        ZeusSymbol.Cloud => 5m,
        ZeusSymbol.Trident => 5m,
        ZeusSymbol.Olympus => 10m,
        _ => 0m
    };

    public static string GetSymbolKey(ZeusSymbol symbol) => symbol.ToString().ToLowerInvariant();

    public static string GetSymbolEmoji(ZeusSymbol symbol) => symbol switch
    {
        ZeusSymbol.Hearts => "♥",
        ZeusSymbol.Spades => "♠",
        ZeusSymbol.Diamonds => "♦",
        ZeusSymbol.Clubs => "♣",
        ZeusSymbol.Mountain => "⛰️",
        ZeusSymbol.Dog => "🐕",
        ZeusSymbol.Cloud => "☁️",
        ZeusSymbol.Trident => "🔱",
        ZeusSymbol.Olympus => "🏛",
        _ => "?"
    };

    public static string GetSymbolLabel(ZeusSymbol symbol) => symbol switch
    {
        ZeusSymbol.Hearts => "Черви",
        ZeusSymbol.Spades => "Пики",
        ZeusSymbol.Diamonds => "Бубны",
        ZeusSymbol.Clubs => "Крести",
        ZeusSymbol.Mountain => "Гора",
        ZeusSymbol.Dog => "Собака",
        ZeusSymbol.Cloud => "Облако",
        ZeusSymbol.Trident => "Трезубец",
        ZeusSymbol.Olympus => "Олимп",
        _ => symbol.ToString()
    };

    public static ZeusSymbol RollSymbol(Random? rng = null) => WeightedPick(SymbolWeights, rng);

    public static int RollLineMultiplier(bool confrontation, Random? rng = null) =>
        WeightedPick(confrontation ? ConfrontationLineWeights : MainLineWeights, rng);

    public static ZeusSymbol[][] BuildGrid(Random? rng = null)
    {
        var grid = new ZeusSymbol[GridSize][];
        for (var r = 0; r < GridSize; r++)
        {
            grid[r] = new ZeusSymbol[GridSize];
            for (var c = 0; c < GridSize; c++)
                grid[r][c] = RollSymbol(rng);
        }

        return grid;
    }

    public static int[] BuildLineMultipliers(bool confrontation, Random? rng = null)
    {
        var lines = new int[GridSize];
        for (var i = 0; i < GridSize; i++)
            lines[i] = RollLineMultiplier(confrontation, rng);
        return lines;
    }

    public static decimal CalculateGridWin(
        ZeusSymbol[][] grid,
        int[] rowMultipliers,
        int[] colMultipliers,
        decimal stake,
        Random? rng = null)
    {
        var random = rng ?? Random.Shared;
        decimal total = 0;

        for (var r = 0; r < GridSize; r++)
        {
            for (var c = 0; c < GridSize; c++)
            {
                var rowMult = rowMultipliers[r];
                var colMult = colMultipliers[c];
                var pickMax = random.Next(2) == 0;
                var pickMult = pickMax
                    ? Math.Max(rowMult, colMult)
                    : Math.Min(rowMult, colMult);

                if (pickMult <= 0)
                    continue;

                var symbolPay = GetSymbolPayout(grid[r][c]);
                total += stake * symbolPay * pickMult / (decimal)IntersectionScale;
            }
        }

        return Math.Round(total, 2);
    }

    public static bool RollConfrontation(Random? rng = null) =>
        (rng ?? Random.Shared).NextDouble() < ConfrontationChance;

    public static decimal GetTheoreticalMaxWinMultiplier()
    {
        var maxSymbolPay = GetSymbolPayout(ZeusSymbol.Olympus);
        var perCell = maxSymbolPay * MaxLineMultiplier / (decimal)IntersectionScale;
        var roundWin = perCell * GridSize * GridSize;
        return roundWin + ConfrontationRounds * roundWin;
    }

    public static decimal SimulateRtp(int sessions = 300_000, int seed = 42)
    {
        var rng = new Random(seed);
        decimal total = 0;
        for (var i = 0; i < sessions; i++)
            total += SimulateSession(stake: 1m, rng);
        return total / sessions;
    }

    private static decimal SimulateSession(decimal stake, Random rng)
    {
        var total = CalculateGridWin(
            BuildGrid(rng),
            BuildLineMultipliers(confrontation: false, rng),
            BuildLineMultipliers(confrontation: false, rng),
            stake,
            rng);

        if (RollConfrontation(rng))
        {
            for (var round = 0; round < ConfrontationRounds; round++)
            {
                total += CalculateGridWin(
                    BuildGrid(rng),
                    BuildLineMultipliers(confrontation: true, rng),
                    BuildLineMultipliers(confrontation: true, rng),
                    stake,
                    rng);
            }
        }

        return total;
    }

    public static ZeusMultiplierPickSide ResolvePickSide(int rowMult, int colMult, bool pickMax, Random rng)
    {
        if (rowMult > colMult)
            return pickMax ? ZeusMultiplierPickSide.Hades : ZeusMultiplierPickSide.Zeus;
        if (colMult > rowMult)
            return pickMax ? ZeusMultiplierPickSide.Zeus : ZeusMultiplierPickSide.Hades;
        return rng.Next(2) == 0 ? ZeusMultiplierPickSide.Hades : ZeusMultiplierPickSide.Zeus;
    }

    private static ZeusSymbol WeightedPick((ZeusSymbol Symbol, int Weight)[] weights, Random? rng = null)
    {
        var random = rng ?? Random.Shared;
        var total = weights.Sum(w => w.Weight);
        var roll = random.Next(total);
        var acc = 0;
        foreach (var (symbol, weight) in weights)
        {
            acc += weight;
            if (roll < acc)
                return symbol;
        }

        return weights[^1].Symbol;
    }

    private static int WeightedPick((int Multiplier, int Weight)[] weights, Random? rng = null)
    {
        var random = rng ?? Random.Shared;
        var total = weights.Sum(w => w.Weight);
        var roll = random.Next(total);
        var acc = 0;
        foreach (var (multiplier, weight) in weights)
        {
            acc += weight;
            if (roll < acc)
                return multiplier;
        }

        return weights[^1].Multiplier;
    }
}
