namespace SportLinea.Models;

public enum FruitlandSymbol
{
    Pear = 0,
    Watermelon = 1,
    Lemon = 2,
    Grape = 3,
    Cherry = 4,
    Peach = 5,
    GreenApple = 6,
    Strawberry = 7,
    Lightning = 8,
    Scatter = 9,
    Empty = 10
}

public enum FruitlandSessionMode
{
    Normal,
    BonusBuy10,
    BonusBuy15,
    BonusBuy20
}

public class FruitlandCellDto
{
    public int Row { get; set; }
    public int Col { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int? Multiplier { get; set; }
}

public class FruitlandClusterWinDto
{
    public List<FruitlandCellDto> Cells { get; set; } = new();
    public string Fruit { get; set; } = string.Empty;
    public int Size { get; set; }
    public decimal Payout { get; set; }
    public int LightningMultiplier { get; set; }
    public int GlobalMultiplierApplied { get; set; }
}

public class FruitlandAnimationStep
{
    public string Type { get; set; } = string.Empty;
    public List<FruitlandCellDto>? Grid { get; set; }
    public FruitlandClusterWinDto? Win { get; set; }
    public int? GlobalMultiplier { get; set; }
    public int? FreeSpin { get; set; }
    public int? FreeSpinsTotal { get; set; }
    public int? FreeSpinsAdded { get; set; }
    public int? ScatterCount { get; set; }
}

public class FruitlandPhaseResult
{
    public string Phase { get; set; } = string.Empty;
    public List<FruitlandAnimationStep> Steps { get; set; } = new();
    public decimal Winnings { get; set; }
    public int ScatterCount { get; set; }
    public bool TriggersBonus { get; set; }
    public int BonusFreeSpins { get; set; }
    public int MaxGlobalMultiplier { get; set; }
}

public class FruitlandSessionResult
{
    public decimal TotalWinnings { get; set; }
    public List<FruitlandPhaseResult> Phases { get; set; } = new();
    public int BonusFreeSpins { get; set; }

    public bool IsSuccessful(bool bonusBuy) =>
        TotalWinnings > 0 || (!bonusBuy && Phases.Any(p => p.Phase == "main" && p.TriggersBonus));

    public int GetMaxGlobalMultiplier() =>
        Phases.Select(p => p.MaxGlobalMultiplier).DefaultIfEmpty(0).Max();
}

/// <summary>
/// Return of Zeus: Fruitland 1000 — кластеры 6×5, молния ×, бонус 💎. ~95% RTP, высокая волатильность.
/// </summary>
public static class Fruitland1000Config
{
    public const int Rows = 6;
    public const int Cols = 5;
    public const int MinClusterSize = 5;
    public const int ScatterBonus4 = 4;
    public const int ScatterBonus5 = 5;
    public const int FreeSpinsOn4 = 10;
    public const int FreeSpinsOn5 = 15;
    public const int RetriggerScatterMin = 3;
    public const int RetriggerScatter4 = 4;
    public const int RetriggerSpinsOn3 = 5;
    public const int RetriggerSpinsOn4 = 10;
    public const int BonusBuy10Multiplier = 100;
    public const int BonusBuy15Multiplier = 150;
    public const int BonusBuy20Multiplier = 200;
    public const int ExclusiveBonusBuySpins = 20;

    public static readonly decimal[] AllowedStakes = CrazyTimeWheel.AllowedStakes;

    public static bool IsAllowedStake(decimal amount) => AllowedStakes.Contains(amount);

    public static decimal GetDefaultStake(decimal balance) =>
        AllowedStakes.Where(s => s <= balance).DefaultIfEmpty(AllowedStakes[0]).Max();

    public static decimal GetBonusBuyPrice(decimal stake, int spins) => spins switch
    {
        10 => stake * BonusBuy10Multiplier,
        15 => stake * BonusBuy15Multiplier,
        20 => stake * BonusBuy20Multiplier,
        _ => stake * BonusBuy10Multiplier
    };

    private static readonly string[] FruitNames =
        ["pear", "watermelon", "lemon", "grape", "cherry", "peach", "greenapple", "strawberry"];

    private static readonly (FruitlandSymbol Symbol, int Weight)[] SpawnWeights =
    [
        (FruitlandSymbol.Pear, 2200),
        (FruitlandSymbol.Watermelon, 1750),
        (FruitlandSymbol.Lemon, 1400),
        (FruitlandSymbol.Grape, 1050),
        (FruitlandSymbol.Cherry, 780),
        (FruitlandSymbol.Peach, 520),
        (FruitlandSymbol.GreenApple, 320),
        (FruitlandSymbol.Strawberry, 180),
        (FruitlandSymbol.Lightning, 110),
        (FruitlandSymbol.Scatter, 65)
    ];

  // Pay multipliers × stake by fruit index and cluster size bucket (5..12+)
    private static readonly decimal[][] Paytable =
    [
        [0.17m, 0.29m, 0.46m, 0.71m, 1.07m, 1.62m, 2.49m, 3.85m],
        [0.22m, 0.38m, 0.59m, 0.90m, 1.38m, 2.14m, 3.29m, 5.12m],
        [0.27m, 0.46m, 0.73m, 1.15m, 1.78m, 2.73m, 4.24m, 6.62m],
        [0.35m, 0.60m, 0.94m, 1.49m, 2.33m, 3.59m, 5.57m, 8.63m],
        [0.44m, 0.76m, 1.22m, 1.93m, 3.00m, 4.66m, 7.22m, 11.24m],
        [0.56m, 0.97m, 1.56m, 2.46m, 3.82m, 5.94m, 9.22m, 14.39m],
        [0.72m, 1.26m, 2.04m, 3.23m, 5.04m, 7.83m, 12.14m, 18.90m],
        [0.94m, 1.66m, 2.72m, 4.29m, 6.70m, 10.40m, 16.11m, 25.12m]
    ];

    private static readonly (int Mult, int Weight)[] NormalLightningWeights =
    [
        (2, 420), (3, 280), (5, 160), (10, 80), (25, 40), (50, 20)
    ];

    private static readonly (int Mult, int Weight)[] BonusLightningWeights =
    [
        (2, 260), (3, 200), (5, 150), (10, 120), (25, 90), (50, 55), (100, 25), (500, 5), (1000, 1)
    ];

    private const decimal BonusPayScale = 0.235m;
    private const int MaxBonusGlobalMultiplier = 80;
    private const decimal RtpPayScale = 97m / 95.56m * 97m / 96.94m;

    public static FruitlandSessionResult RunSession(
        decimal stake,
        FruitlandSessionMode mode = FruitlandSessionMode.Normal,
        Random? rng = null)
    {
        var random = rng ?? Random.Shared;
        var result = new FruitlandSessionResult();

        if (mode != FruitlandSessionMode.Normal)
        {
            var spins = mode switch
            {
                FruitlandSessionMode.BonusBuy10 => FreeSpinsOn4,
                FruitlandSessionMode.BonusBuy15 => FreeSpinsOn5,
                FruitlandSessionMode.BonusBuy20 => ExclusiveBonusBuySpins,
                _ => FreeSpinsOn4
            };
            var bonus = RunBonusPhase(stake, spins, random);
            result.Phases.Add(bonus);
            result.TotalWinnings = bonus.Winnings;
            result.BonusFreeSpins = spins;
            return result;
        }

        var main = RunSingleSpin(stake, bonus: false, random, globalMult: 0, out var scatterCount);
        main.Phase = "main";
        main.ScatterCount = scatterCount;
        result.Phases.Add(main);
        result.TotalWinnings += main.Winnings;

        if (scatterCount >= ScatterBonus4)
        {
            main.TriggersBonus = true;
            main.BonusFreeSpins = scatterCount >= ScatterBonus5 ? FreeSpinsOn5 : FreeSpinsOn4;
            result.BonusFreeSpins = main.BonusFreeSpins;

            var bonus = RunBonusPhase(stake, main.BonusFreeSpins, random);
            result.Phases.Add(bonus);
            result.TotalWinnings += bonus.Winnings;
        }

        result.TotalWinnings = Math.Round(result.TotalWinnings, 2);
        return result;
    }

    public static decimal SimulateRtp(int sessions = 150_000, int seed = 42)
    {
        var rng = new Random(seed);
        decimal total = 0;
        var bonusHits = 0;
        decimal maxWin = 0;
        for (var i = 0; i < sessions; i++)
        {
            var s = RunSession(1m, FruitlandSessionMode.Normal, rng);
            total += s.TotalWinnings;
            if (s.BonusFreeSpins > 0 && s.Phases.Any(p => p.Phase == "main" && p.TriggersBonus))
                bonusHits++;
            if (s.TotalWinnings > maxWin)
                maxWin = s.TotalWinnings;
        }

        Console.WriteLine($"Fruitland bonus rate: {bonusHits * 100m / sessions:F3}% | Max win: {maxWin:F0}x");
        return total / sessions;
    }

    public static decimal SimulateBonusBuyRtp(int spins, int sessions = 30_000, int seed = 42)
    {
        var mode = spins switch
        {
            10 => FruitlandSessionMode.BonusBuy10,
            15 => FruitlandSessionMode.BonusBuy15,
            20 => FruitlandSessionMode.BonusBuy20,
            _ => FruitlandSessionMode.BonusBuy10
        };
        var priceMult = spins switch
        {
            10 => BonusBuy10Multiplier,
            15 => BonusBuy15Multiplier,
            20 => BonusBuy20Multiplier,
            _ => BonusBuy10Multiplier
        };
        var rng = new Random(seed);
        decimal total = 0;
        for (var i = 0; i < sessions; i++)
            total += RunSession(1m, mode, rng).TotalWinnings;
        return total / (sessions * priceMult);
    }

    private static FruitlandPhaseResult RunBonusPhase(decimal stake, int freeSpins, Random rng)
    {
        var phase = new FruitlandPhaseResult { Phase = "bonus" };
        var globalMult = 0;
        decimal totalWin = 0;
        var maxGlobal = 0;
        var totalFreeSpins = freeSpins;

        phase.Steps.Add(new FruitlandAnimationStep
        {
            Type = "bonusStart",
            FreeSpinsTotal = totalFreeSpins,
            GlobalMultiplier = 0
        });

        var spin = 0;
        while (spin < totalFreeSpins)
        {
            spin++;
            phase.Steps.Add(new FruitlandAnimationStep
            {
                Type = "freeSpinStart",
                FreeSpin = spin,
                FreeSpinsTotal = totalFreeSpins,
                GlobalMultiplier = globalMult
            });

            var spinPhase = RunSingleSpin(stake, bonus: true, rng, globalMult, out var spinScatter);
            foreach (var step in spinPhase.Steps)
                phase.Steps.Add(step);

            totalWin += spinPhase.Winnings;
            globalMult = spinPhase.MaxGlobalMultiplier;
            if (globalMult > maxGlobal)
                maxGlobal = globalMult;

            if (spinScatter >= RetriggerScatterMin)
            {
                var added = spinScatter >= RetriggerScatter4 ? RetriggerSpinsOn4 : RetriggerSpinsOn3;
                totalFreeSpins += added;
                phase.Steps.Add(new FruitlandAnimationStep
                {
                    Type = "retrigger",
                    ScatterCount = spinScatter,
                    FreeSpinsAdded = added,
                    FreeSpinsTotal = totalFreeSpins,
                    GlobalMultiplier = globalMult
                });
            }
        }

        phase.Steps.Add(new FruitlandAnimationStep
        {
            Type = "bonusEnd",
            GlobalMultiplier = globalMult
        });

        phase.Winnings = Math.Round(totalWin, 2);
        phase.MaxGlobalMultiplier = maxGlobal;
        phase.BonusFreeSpins = totalFreeSpins;
        return phase;
    }

    private static FruitlandPhaseResult RunSingleSpin(
        decimal stake,
        bool bonus,
        Random rng,
        int globalMult,
        out int scatterCount)
    {
        var phase = new FruitlandPhaseResult();
        var grid = new FruitlandSymbol[Rows, Cols];
        var lightningMults = new int?[Rows, Cols];
        FillGrid(grid, lightningMults, bonus, rng);
        scatterCount = CountScatters(grid);
        var maxScatter = scatterCount;
        var runningGlobal = globalMult;
        var maxGlobal = globalMult;
        decimal spinWin = 0;

        phase.Steps.Add(new FruitlandAnimationStep
        {
            Type = "grid",
            Grid = SnapshotGrid(grid, lightningMults),
            GlobalMultiplier = runningGlobal
        });

        while (true)
        {
            var clusters = FindClusters(grid);
            var winners = clusters.Where(c => c.Cells.Count >= MinClusterSize).ToList();
            if (winners.Count == 0)
                break;

            winners = winners.OrderBy(c => c.Cells.Min(p => p.Row))
                .ThenBy(c => c.Cells.Min(p => p.Col))
                .ToList();

            var waveWins = new List<(Cluster Cluster, int LightningMult, int EffectiveGlobal, decimal Payout, List<FruitlandCellDto> Cells, List<(int R, int C, int Factor)> LightningHits)>();

            foreach (var cluster in winners)
            {
                var lightningMult = 1;
                var lightningHits = new List<(int R, int C, int Factor)>();
                foreach (var (r, c) in cluster.Cells)
                {
                    if (grid[r, c] != FruitlandSymbol.Lightning)
                        continue;
                    var x = lightningMults[r, c] ?? PickWeighted(
                        bonus ? BonusLightningWeights : NormalLightningWeights, rng);
                    lightningMults[r, c] = x;
                    lightningHits.Add((r, c, x));
                    lightningMult *= x;
                }

                var effectiveGlobal = Math.Max(1, runningGlobal);
                var basePay = GetClusterPay(cluster.Fruit, cluster.Cells.Count, stake, bonus);
                var payout = Math.Round(basePay * lightningMult * (bonus ? effectiveGlobal : 1), 2);
                spinWin += payout;

                if (bonus)
                {
                    foreach (var (_, _, x) in lightningHits)
                        runningGlobal = Math.Min(MaxBonusGlobalMultiplier, runningGlobal + x);
                    if (runningGlobal > maxGlobal)
                        maxGlobal = runningGlobal;
                }

                var clusterCells = BuildClusterCellDtos(grid, lightningMults, cluster.Cells, lightningHits);
                waveWins.Add((cluster, lightningMult, effectiveGlobal, payout, clusterCells, lightningHits));
            }

            foreach (var (cluster, lightningMult, effectiveGlobal, payout, clusterCells, lightningHits) in waveWins)
            {
                phase.Steps.Add(new FruitlandAnimationStep
                {
                    Type = "highlight",
                    Win = new FruitlandClusterWinDto
                    {
                        Cells = clusterCells,
                        Fruit = FruitNames[(int)cluster.Fruit],
                        Size = cluster.Cells.Count,
                        Payout = payout,
                        LightningMultiplier = lightningMult,
                        GlobalMultiplierApplied = bonus ? effectiveGlobal : 1
                    },
                    GlobalMultiplier = runningGlobal
                });

                if (bonus && lightningHits.Count > 0)
                {
                    phase.Steps.Add(new FruitlandAnimationStep
                    {
                        Type = "globalmult",
                        GlobalMultiplier = runningGlobal
                    });
                }

                phase.Steps.Add(new FruitlandAnimationStep
                {
                    Type = "win",
                    Win = new FruitlandClusterWinDto
                    {
                        Cells = clusterCells,
                        Fruit = FruitNames[(int)cluster.Fruit],
                        Size = cluster.Cells.Count,
                        Payout = payout,
                        LightningMultiplier = lightningMult,
                        GlobalMultiplierApplied = bonus ? effectiveGlobal : 1
                    },
                    GlobalMultiplier = runningGlobal
                });
            }

            var cleared = new HashSet<(int Row, int Col)>();
            foreach (var (cluster, _, _, _, _, _) in waveWins)
            foreach (var cell in cluster.Cells)
                cleared.Add(cell);

            foreach (var (r, c) in cleared)
            {
                grid[r, c] = FruitlandSymbol.Empty;
                lightningMults[r, c] = null;
            }

            DropAndRefill(grid, lightningMults, bonus, rng);
            scatterCount = CountScatters(grid);
            if (scatterCount > maxScatter)
                maxScatter = scatterCount;

            phase.Steps.Add(new FruitlandAnimationStep
            {
                Type = "cascade",
                Grid = SnapshotGrid(grid, lightningMults),
                GlobalMultiplier = runningGlobal
            });
        }

        if (!bonus && maxScatter >= ScatterBonus4)
        {
            phase.Steps.Add(new FruitlandAnimationStep
            {
                Type = "clearall",
                ScatterCount = maxScatter
            });
        }

        phase.Winnings = Math.Round(spinWin, 2);
        phase.MaxGlobalMultiplier = maxGlobal;
        phase.ScatterCount = maxScatter;
        scatterCount = maxScatter;
        return phase;
    }

    private sealed class Cluster
    {
        public FruitlandSymbol Fruit { get; set; }
        public List<(int Row, int Col)> Cells { get; set; } = new();
    }

    private static List<Cluster> FindClusters(FruitlandSymbol[,] grid)
    {
        var fruitVisited = new bool[Rows, Cols];
        var clusters = new List<Cluster>();

        for (var r = 0; r < Rows; r++)
        for (var c = 0; c < Cols; c++)
        {
            var sym = grid[r, c];
            if (sym is FruitlandSymbol.Empty or FruitlandSymbol.Scatter or FruitlandSymbol.Lightning)
                continue;
            if (fruitVisited[r, c])
                continue;

            var fruit = sym;
            var cells = new List<(int, int)>();
            var localVisited = new bool[Rows, Cols];
            var queue = new Queue<(int, int)>();
            queue.Enqueue((r, c));
            localVisited[r, c] = true;

            while (queue.Count > 0)
            {
                var (cr, cc) = queue.Dequeue();
                cells.Add((cr, cc));
                if (grid[cr, cc] == fruit)
                    fruitVisited[cr, cc] = true;

                foreach (var (nr, nc) in Neighbors(cr, cc))
                {
                    if (localVisited[nr, nc])
                        continue;
                    var ns = grid[nr, nc];
                    if (ns is FruitlandSymbol.Empty or FruitlandSymbol.Scatter)
                        continue;
                    if (ns == fruit || ns == FruitlandSymbol.Lightning)
                    {
                        localVisited[nr, nc] = true;
                        queue.Enqueue((nr, nc));
                    }
                }
            }

            clusters.Add(new Cluster { Fruit = fruit, Cells = cells });
        }

        return clusters;
    }

    private static IEnumerable<(int Row, int Col)> Neighbors(int r, int c)
    {
        if (r > 0) yield return (r - 1, c);
        if (r < Rows - 1) yield return (r + 1, c);
        if (c > 0) yield return (r, c - 1);
        if (c < Cols - 1) yield return (r, c + 1);
    }

    private static decimal GetClusterPay(FruitlandSymbol fruit, int size, decimal stake, bool bonus = false)
    {
        var idx = Math.Clamp((int)fruit, 0, 7);
        var bucket = Math.Clamp(size - MinClusterSize, 0, Paytable[idx].Length - 1);
        var pay = Paytable[idx][bucket] * stake;
        pay *= RtpPayScale;
        return bonus ? pay * BonusPayScale : pay;
    }

    private static void FillGrid(FruitlandSymbol[,] grid, int?[,] lightningMults, bool bonus, Random rng)
    {
        for (var r = 0; r < Rows; r++)
        for (var c = 0; c < Cols; c++)
            PlaceSymbol(grid, lightningMults, r, c, RollSymbol(rng), bonus, rng);
    }

    private static void PlaceSymbol(
        FruitlandSymbol[,] grid,
        int?[,] lightningMults,
        int r,
        int c,
        FruitlandSymbol sym,
        bool bonus,
        Random rng)
    {
        grid[r, c] = sym;
        lightningMults[r, c] = sym == FruitlandSymbol.Lightning
            ? PickWeighted(bonus ? BonusLightningWeights : NormalLightningWeights, rng)
            : null;
    }

    private static void DropAndRefill(FruitlandSymbol[,] grid, int?[,] lightningMults, bool bonus, Random rng)
    {
        for (var c = 0; c < Cols; c++)
        {
            var scatterRows = new HashSet<int>();
            var symbols = new List<FruitlandSymbol>();
            var mults = new List<int?>();

            for (var r = 0; r < Rows; r++)
            {
                var sym = grid[r, c];
                if (sym == FruitlandSymbol.Scatter)
                    scatterRows.Add(r);
                else if (sym != FruitlandSymbol.Empty)
                {
                    symbols.Add(sym);
                    mults.Add(sym == FruitlandSymbol.Lightning ? lightningMults[r, c] : null);
                }
            }

            for (var r = 0; r < Rows; r++)
                lightningMults[r, c] = null;

            var targetRows = Enumerable.Range(0, Rows)
                .Where(r => !scatterRows.Contains(r))
                .OrderByDescending(r => r)
                .ToList();

            var symIdx = symbols.Count - 1;
            foreach (var r in targetRows)
            {
                if (symIdx >= 0)
                {
                    var sym = symbols[symIdx];
                    var mult = mults[symIdx];
                    symIdx--;
                    grid[r, c] = sym;
                    lightningMults[r, c] = sym == FruitlandSymbol.Lightning ? mult : null;
                }
                else
                {
                    PlaceSymbol(grid, lightningMults, r, c, RollSymbol(rng), bonus, rng);
                }
            }

            foreach (var r in scatterRows)
            {
                grid[r, c] = FruitlandSymbol.Scatter;
                lightningMults[r, c] = null;
            }
        }
    }

    private static int CountScatters(FruitlandSymbol[,] grid)
    {
        var n = 0;
        for (var r = 0; r < Rows; r++)
        for (var c = 0; c < Cols; c++)
            if (grid[r, c] == FruitlandSymbol.Scatter)
                n++;
        return n;
    }

    private static FruitlandSymbol RollSymbol(Random rng) =>
        PickWeighted(SpawnWeights, rng);

    private static T PickWeighted<T>((T Value, int Weight)[] weights, Random rng)
    {
        var total = weights.Sum(w => w.Weight);
        var roll = rng.Next(total);
        foreach (var (value, weight) in weights)
        {
            if (roll < weight)
                return value;
            roll -= weight;
        }

        return weights[^1].Value;
    }

    private static List<FruitlandCellDto> SnapshotGrid(FruitlandSymbol[,] grid, int?[,] lightningMults)
    {
        var list = new List<FruitlandCellDto>();
        for (var r = 0; r < Rows; r++)
        for (var c = 0; c < Cols; c++)
        {
            if (grid[r, c] == FruitlandSymbol.Empty)
                continue;
            list.Add(ToDto(r, c, grid[r, c], lightningMults[r, c]));
        }

        return list;
    }

    private static List<FruitlandCellDto> BuildClusterCellDtos(
        FruitlandSymbol[,] grid,
        int?[,] lightningMults,
        List<(int Row, int Col)> cells,
        List<(int R, int C, int Factor)> lightningHits)
    {
        var lightningMap = lightningHits.ToDictionary(h => (h.R, h.C), h => h.Factor);
        return cells.Select(p =>
        {
            var sym = grid[p.Row, p.Col];
            var presetMult = sym == FruitlandSymbol.Lightning ? lightningMults[p.Row, p.Col] : null;
            var dto = ToDto(p.Row, p.Col, sym, presetMult);
            if (lightningMap.TryGetValue((p.Row, p.Col), out var factor))
                dto.Multiplier = factor;
            return dto;
        }).ToList();
    }

    private static FruitlandCellDto ToDto(int r, int c, FruitlandSymbol sym, int? lightningMult) => new()
    {
        Row = r,
        Col = c,
        Kind = sym switch
        {
            FruitlandSymbol.Lightning => "lightning",
            FruitlandSymbol.Scatter => "scatter",
            _ => FruitNames[(int)sym]
        },
        Multiplier = sym == FruitlandSymbol.Lightning ? lightningMult : null
    };
}
