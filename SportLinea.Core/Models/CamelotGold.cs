namespace SportLinea.Models;

public enum CamelotCellKind
{
    Empty,
    Bronze,
    Silver,
    Gold,
    Collector,
    Scatter,
    Multiplier
}

public enum CamelotPhaseKind
{
    Main,
    Bonus
}

public class CamelotRevealEvent
{
    public int Row { get; set; }
    public int Col { get; set; }
    public string Kind { get; set; } = string.Empty;
    public decimal? Nominal { get; set; }
    public int? Multiplier { get; set; }
}

public class CamelotCollectorEvent
{
    public int Row { get; set; }
    public int Col { get; set; }
    public decimal CollectedNominal { get; set; }
}

public class CamelotMultiplyEvent
{
    public int Row { get; set; }
    public int Col { get; set; }
    public int Factor { get; set; }
}

public class CamelotPhaseResult
{
    public string Phase { get; set; } = string.Empty;
    public List<CamelotRevealEvent> Reveals { get; set; } = new();
    public List<CamelotCollectorEvent> Collectors { get; set; } = new();
    public List<CamelotMultiplyEvent> Multipliers { get; set; } = new();
    public List<CamelotAnimationStep> Steps { get; set; } = new();
    public decimal TotalNominal { get; set; }
    public decimal Winnings { get; set; }
    public int ScatterCount { get; set; }
    public bool TriggersBonus { get; set; }
    public bool RetriggersBonus { get; set; }
}

public class CamelotAnimationStep
{
    public string Type { get; set; } = string.Empty;
    public CamelotRevealEvent? Reveal { get; set; }
    public CamelotCollectorEvent? Collector { get; set; }
    public CamelotMultiplyEvent? Multiplier { get; set; }
    public List<CamelotCollectorEvent>? KeepCollectors { get; set; }
    public List<CamelotRevealEvent>? UpdatedNominals { get; set; }
}

public class CamelotSpinResult
{
    public decimal TotalWinnings { get; set; }
    public List<CamelotPhaseResult> Phases { get; set; } = new();
    public int BonusQueued { get; set; }

    public decimal GetMaxCollectorNominal() =>
        Phases.SelectMany(p => p.Collectors).Select(c => c.CollectedNominal).DefaultIfEmpty(0).Max();

    public bool IsSuccessful(bool bonusBuy) =>
        TotalWinnings > 0 || (!bonusBuy && Phases.Any(p => p.Phase == "main" && p.TriggersBonus));
}

/// <summary>
/// Camelot's Gold — поле 9×9, кластеры монет, коллектор, ×-монета, бонус 🏰. ~96% RTP.
/// </summary>
public static class CamelotGoldConfig
{
    public const int GridSize = 9;
    public const int MaxNormalCellsPerWave = 25;
    public const int NormalScatterTrigger = 3;
    public const int BonusScatterRetrigger = 5;
    public const int BonusBuyMultiplier = 150;
    public const int FeatureBuyMultiplier = 10;
    public const int TotalCells = GridSize * GridSize;

    public static readonly decimal[] AllowedStakes = CrazyTimeWheel.AllowedStakes;

    public static bool IsAllowedStake(decimal amount) => AllowedStakes.Contains(amount);

    public static decimal GetDefaultStake(decimal balance) =>
        AllowedStakes.Where(s => s <= balance).DefaultIfEmpty(AllowedStakes[0]).Max();

    public static decimal GetBonusBuyPrice(decimal stake) => stake * BonusBuyMultiplier;

    public static decimal GetFeatureBuyPrice(decimal stake) => stake * FeatureBuyMultiplier;

    private static readonly (decimal Nominal, int Weight)[] NormalCoinWeights =
    [
        (0.1m, 680), (0.25m, 200), (0.5m, 75),
        (1m, 22), (2m, 6), (5m, 2),
        (10m, 1), (15m, 1)
    ];

    private static readonly (decimal Nominal, int Weight)[] BonusCoinWeights =
    [
        (1m, 7000), (2m, 700), (3m, 350),
        (5m, 100), (10m, 50), (15m, 25), (25m, 15),
        (50m, 14), (100m, 6), (1000m, 1)
    ];

    private static readonly (int Factor, int Weight)[] NormalMultWeights = [(2, 85), (3, 15)];
    private static readonly (int Factor, int Weight)[] BonusMultWeights = [(2, 55), (3, 25), (5, 14), (10, 6)];

    private static readonly (int Cells, int Weight)[] NormalWaveSizeWeights =
    [
        (3, 200), (5, 280), (8, 260), (12, 150), (16, 70), (20, 30), (25, 10)
    ];

    private const double NormalDeadSpinChance = 0.51;

    public static CamelotSpinResult RunSession(decimal stake, bool bonusBuy, bool featureBuy = false, Random? rng = null)
    {
        var random = rng ?? Random.Shared;
        var result = new CamelotSpinResult();

        if (!bonusBuy)
        {
            var main = RunNormalSpin(random, guaranteedCollector: featureBuy);
            main.Winnings = Math.Round(main.TotalNominal * stake, 2);
            result.Phases.Add(main);
            result.TotalWinnings += main.Winnings;

            var bonuses = 0;
            if (main.TriggersBonus)
                bonuses = 1;

            while (bonuses > 0)
            {
                var bonus = RunBonusSpin(random, purchasedBonus: false);
                bonus.Winnings = Math.Round(bonus.TotalNominal * stake, 2);
                result.Phases.Add(bonus);
                result.TotalWinnings += bonus.Winnings;
                bonuses--;
                if (bonus.RetriggersBonus)
                    bonuses++;
            }

            result.BonusQueued = result.Phases.Count(p => p.Phase == "bonus");
        }
        else
        {
            var bonus = RunBonusSpin(random, purchasedBonus: true);
            bonus.Winnings = Math.Round(bonus.TotalNominal * stake, 2);
            result.Phases.Add(bonus);
            result.TotalWinnings = bonus.Winnings;
            var bonuses = bonus.RetriggersBonus ? 1 : 0;
            while (bonuses > 0)
            {
                var extra = RunBonusSpin(random, purchasedBonus: true);
                extra.Winnings = Math.Round(extra.TotalNominal * stake, 2);
                result.Phases.Add(extra);
                result.TotalWinnings += extra.Winnings;
                bonuses--;
                if (extra.RetriggersBonus)
                    bonuses++;
            }
        }

        result.TotalWinnings = Math.Round(result.TotalWinnings, 2);
        return result;
    }

    public static decimal SimulateRtp(int sessions = 200_000, int seed = 42)
    {
        var rng = new Random(seed);
        decimal total = 0;
        var bonusHits = 0;
        var zeroWins = 0;
        decimal maxWin = 0;
        for (var i = 0; i < sessions; i++)
        {
            var session = RunSession(1m, bonusBuy: false, rng: rng);
            total += session.TotalWinnings;
            if (session.BonusQueued > 0)
                bonusHits++;
            if (session.TotalWinnings <= 0)
                zeroWins++;
            if (session.TotalWinnings > maxWin)
                maxWin = session.TotalWinnings;
        }

        Console.WriteLine($"Bonus rate: {bonusHits * 100m / sessions:F3}% | Zero wins: {zeroWins * 100m / sessions:F1}% | Max win: {maxWin:F0}x");
        return total / sessions;
    }

    public static decimal SimulateBonusBuyRtp(int sessions = 50_000, int seed = 42)
    {
        var rng = new Random(seed);
        decimal total = 0;
        for (var i = 0; i < sessions; i++)
            total += RunSession(1m, bonusBuy: true, rng: rng).TotalWinnings;
        return total / (sessions * BonusBuyMultiplier);
    }

    private const int MaxCollectorWaves = 10;

    private static CamelotPhaseResult RunNormalSpin(Random rng, bool guaranteedCollector = false)
    {
        var phase = new CamelotPhaseResult { Phase = "main" };
        if (!guaranteedCollector && rng.NextDouble() < NormalDeadSpinChance)
            return phase;

        var grid = CreateEmptyGrid();
        var scatterCount = 0;
        var collectorQueue = new Queue<(int R, int C)>();
        var wavesPlayed = 0;

        var waveSize = Math.Min(PickWeighted(NormalWaveSizeWeights, rng), MaxNormalCellsPerWave);
        var placements = PlaceClusterWave(grid, waveSize, bonus: false, rng, forceFirstCollector: guaranteedCollector);
        if (placements.Count == 0)
            return phase;

        wavesPlayed++;
        var applied = ProcessNormalWave(phase, grid, placements, rng, ref scatterCount);
        EnqueueCollectors(applied, collectorQueue);
        RunCollectorRefillLoop(phase, grid, collectorQueue, ref scatterCount, rng, bonus: false, purchasedBonus: false, ref wavesPlayed);

        phase.ScatterCount = scatterCount;
        phase.TotalNominal = SumAllNominals(grid);

        if (phase.TriggersBonus)
            phase.Steps.Add(new CamelotAnimationStep { Type = "clearall" });

        return phase;
    }

    private static void RunCollectorRefillLoop(
        CamelotPhaseResult phase,
        CamelotCell[][] grid,
        Queue<(int R, int C)> collectorQueue,
        ref int scatterCount,
        Random rng,
        bool bonus,
        bool purchasedBonus,
        ref int wavesPlayed)
    {
        while (collectorQueue.Count > 0)
        {
            var (collectorR, collectorC) = collectorQueue.Dequeue();
            if (grid[collectorR][collectorC].Kind != CamelotCellKind.Collector)
                continue;

            var collected = SumForCollector(grid, collectorR, collectorC);
            grid[collectorR][collectorC] = new CamelotCell { Kind = CamelotCellKind.Collector, Nominal = collected };
            var collectorEvent = new CamelotCollectorEvent
            {
                Row = collectorR,
                Col = collectorC,
                CollectedNominal = collected
            };
            phase.Collectors.Add(collectorEvent);
            phase.Steps.Add(new CamelotAnimationStep { Type = "collect", Collector = collectorEvent });

            ClearCoins(grid);
            phase.Steps.Add(new CamelotAnimationStep
            {
                Type = "clear",
                KeepCollectors = GetActiveCollectors(grid)
            });

            if (wavesPlayed >= MaxCollectorWaves)
                break;

            var waveSize = Math.Min(PickWeighted(NormalWaveSizeWeights, rng), MaxNormalCellsPerWave);
            var placements = PlaceClusterWave(grid, waveSize, bonus, rng, purchasedBonus);
            if (placements.Count == 0)
                break;

            wavesPlayed++;
            var applied = ProcessNormalWave(phase, grid, placements, rng, ref scatterCount);
            EnqueueCollectors(applied, collectorQueue);
        }
    }

    private static List<(int R, int C, int Factor)> ExtractWaveMults(List<Placement> placements) =>
        placements
            .Where(p => p.Kind == CamelotCellKind.Multiplier)
            .Select(p => (p.R, p.C, p.Mult))
            .ToList();

    private static void ApplyWaveMultipliersToGrid(
        CamelotCell[][] grid,
        List<(int R, int C, int Factor)> waveMults,
        Random rng)
    {
        foreach (var (r, c, factor) in waveMults.OrderBy(_ => rng.Next()))
        {
            MultiplyAllNominals(grid, factor);
            ApplyPlacement(grid, new Placement(r, c, CamelotCellKind.Multiplier, 0, factor));
        }
    }

    private static void AddMultiplyAnimationSteps(
        CamelotPhaseResult phase,
        List<(int R, int C, int Factor)> waveMults,
        Random rng)
    {
        foreach (var (r, c, factor) in waveMults.OrderBy(_ => rng.Next()))
        {
            var multEvent = new CamelotMultiplyEvent { Row = r, Col = c, Factor = factor };
            var reveal = new CamelotRevealEvent
            {
                Row = r,
                Col = c,
                Kind = "multiplier",
                Multiplier = factor
            };
            phase.Multipliers.Add(multEvent);
            phase.Reveals.Add(reveal);
            phase.Steps.Add(new CamelotAnimationStep
            {
                Type = "multiply",
                Multiplier = multEvent,
                Reveal = reveal
            });
        }
    }

    private static void ApplyWaveMultipliers(
        CamelotPhaseResult phase,
        CamelotCell[][] grid,
        List<(int R, int C, int Factor)> waveMults,
        Random rng,
        bool includeNominalUpdates = true)
    {
        foreach (var (r, c, factor) in waveMults.OrderBy(_ => rng.Next()))
        {
            MultiplyAllNominals(grid, factor);
            ApplyPlacement(grid, new Placement(r, c, CamelotCellKind.Multiplier, 0, factor));

            var multEvent = new CamelotMultiplyEvent { Row = r, Col = c, Factor = factor };
            var reveal = new CamelotRevealEvent
            {
                Row = r,
                Col = c,
                Kind = "multiplier",
                Multiplier = factor
            };
            phase.Multipliers.Add(multEvent);
            phase.Reveals.Add(reveal);
            phase.Steps.Add(new CamelotAnimationStep
            {
                Type = "multiply",
                Multiplier = multEvent,
                Reveal = reveal,
                UpdatedNominals = includeNominalUpdates ? SnapshotGridNominals(grid) : null
            });
        }
    }

    private static List<CamelotRevealEvent> SnapshotGridNominals(CamelotCell[][] grid)
    {
        var snapshots = new List<CamelotRevealEvent>();
        for (var r = 0; r < GridSize; r++)
        for (var c = 0; c < GridSize; c++)
        {
            var cell = grid[r][c];
            if (cell.Kind == CamelotCellKind.Empty || cell.Kind == CamelotCellKind.Scatter ||
                cell.Kind == CamelotCellKind.Multiplier)
                continue;

            if (cell.Kind == CamelotCellKind.Collector && cell.Nominal <= 0)
                continue;

            snapshots.Add(new CamelotRevealEvent
            {
                Row = r,
                Col = c,
                Kind = cell.Kind switch
                {
                    CamelotCellKind.Bronze => "bronze",
                    CamelotCellKind.Silver => "silver",
                    CamelotCellKind.Gold => "gold",
                    CamelotCellKind.Collector => "collector",
                    _ => "empty"
                },
                Nominal = cell.Nominal
            });
        }

        return snapshots;
    }

    private static void EnqueueCollectors(IEnumerable<Placement> placements, Queue<(int R, int C)> collectorQueue)
    {
        foreach (var placement in placements.Where(p => p.Kind == CamelotCellKind.Collector))
            collectorQueue.Enqueue((placement.R, placement.C));
    }

    /// <summary>
    /// Normal-spin wave: apply cells, ×-monеты, reveal with final nominals.
    /// </summary>
    private static List<Placement> ProcessNormalWave(
        CamelotPhaseResult phase,
        CamelotCell[][] grid,
        List<Placement> placements,
        Random rng,
        ref int scatterCount)
    {
        var revealOrder = BuildRevealOrder(placements, rng)
            .Where(p => p.Kind != CamelotCellKind.Multiplier)
            .ToList();
        var applied = new List<Placement>();

        foreach (var placement in revealOrder)
        {
            ApplyPlacement(grid, placement);
            applied.Add(placement);

            if (placement.Kind != CamelotCellKind.Scatter)
                continue;

            scatterCount++;
            if (scatterCount >= NormalScatterTrigger)
                phase.TriggersBonus = true;
        }

        ApplyWaveMultipliersToGrid(grid, ExtractWaveMults(placements), rng);

        foreach (var placement in applied.OrderBy(_ => rng.Next()))
        {
            var reveal = ToRevealFromCell(grid, placement.R, placement.C);
            phase.Reveals.Add(reveal);
            phase.Steps.Add(new CamelotAnimationStep { Type = "reveal", Reveal = reveal });
        }

        AddMultiplyAnimationSteps(phase, ExtractWaveMults(placements), rng);

        return applied;
    }

    private static List<Placement> RevealWave(
        CamelotPhaseResult phase,
        CamelotCell[][] grid,
        List<Placement> placements,
        Random rng)
    {
        var revealOrder = BuildRevealOrder(placements, rng);
        foreach (var placement in revealOrder.Where(p => p.Kind != CamelotCellKind.Multiplier))
        {
            var reveal = ToReveal(placement);
            phase.Reveals.Add(reveal);
            phase.Steps.Add(new CamelotAnimationStep { Type = "reveal", Reveal = reveal });
        }

        foreach (var placement in revealOrder.Where(p => p.Kind != CamelotCellKind.Multiplier))
            ApplyPlacement(grid, placement);

        return revealOrder;
    }

    private static List<CamelotCollectorEvent> GetActiveCollectors(CamelotCell[][] grid)
    {
        var collectors = new List<CamelotCollectorEvent>();
        for (var r = 0; r < GridSize; r++)
        for (var c = 0; c < GridSize; c++)
        {
            if (grid[r][c].Kind != CamelotCellKind.Collector)
                continue;
            collectors.Add(new CamelotCollectorEvent
            {
                Row = r,
                Col = c,
                CollectedNominal = grid[r][c].Nominal
            });
        }

        return collectors;
    }

    private static decimal SumForCollector(CamelotCell[][] grid, int collectorR, int collectorC)
    {
        decimal sum = 0;
        for (var r = 0; r < GridSize; r++)
        for (var c = 0; c < GridSize; c++)
        {
            var cell = grid[r][c];
            if (cell.Kind is CamelotCellKind.Bronze or CamelotCellKind.Silver or CamelotCellKind.Gold)
                sum += cell.Nominal;
            else if (cell.Kind == CamelotCellKind.Collector && (r != collectorR || c != collectorC))
                sum += cell.Nominal;
        }

        return sum;
    }

    private static CamelotPhaseResult RunBonusSpin(Random rng, bool purchasedBonus = false)
    {
        var phase = new CamelotPhaseResult { Phase = "bonus" };
        var grid = CreateEmptyGrid();
        var scatterCount = 0;
        var collectorQueue = new Queue<(int R, int C)>();
        var wavesPlayed = 0;

        var placements = CreateBonusBoardPlacementsForEmptyCells(grid, rng, purchasedBonus);
        wavesPlayed++;
        var revealOrder = RevealFullBonusBoard(phase, grid, placements, ref scatterCount, rng);
        ApplyWaveMultipliers(phase, grid, ExtractWaveMults(placements), rng);
        EnqueueCollectors(revealOrder, collectorQueue);
        RunBonusCollectorRefillLoop(phase, grid, collectorQueue, ref scatterCount, rng, purchasedBonus, ref wavesPlayed);

        phase.ScatterCount = scatterCount;
        phase.RetriggersBonus = scatterCount >= BonusScatterRetrigger;
        phase.TotalNominal = SumAllNominals(grid);
        return phase;
    }

    private static void RunBonusCollectorRefillLoop(
        CamelotPhaseResult phase,
        CamelotCell[][] grid,
        Queue<(int R, int C)> collectorQueue,
        ref int scatterCount,
        Random rng,
        bool purchasedBonus,
        ref int wavesPlayed)
    {
        while (collectorQueue.Count > 0)
        {
            var (collectorR, collectorC) = collectorQueue.Dequeue();
            if (grid[collectorR][collectorC].Kind != CamelotCellKind.Collector)
                continue;

            var collected = SumForCollector(grid, collectorR, collectorC);
            grid[collectorR][collectorC] = new CamelotCell { Kind = CamelotCellKind.Collector, Nominal = collected };
            var collectorEvent = new CamelotCollectorEvent
            {
                Row = collectorR,
                Col = collectorC,
                CollectedNominal = collected
            };
            phase.Collectors.Add(collectorEvent);
            phase.Steps.Add(new CamelotAnimationStep { Type = "collect", Collector = collectorEvent });

            ClearCoins(grid);
            phase.Steps.Add(new CamelotAnimationStep
            {
                Type = "clear",
                KeepCollectors = GetActiveCollectors(grid)
            });

            if (wavesPlayed >= MaxCollectorWaves)
                break;

            var placements = CreateBonusBoardPlacementsForEmptyCells(grid, rng, purchasedBonus);
            if (placements.Count == 0)
                break;

            wavesPlayed++;
            var revealOrder = RevealFullBonusBoard(phase, grid, placements, ref scatterCount, rng);
            ApplyWaveMultipliers(phase, grid, ExtractWaveMults(placements), rng);
            EnqueueCollectors(revealOrder, collectorQueue);
        }
    }

    private static List<Placement> CreateBonusBoardPlacementsForEmptyCells(
        CamelotCell[][] grid,
        Random rng,
        bool purchasedBonus)
    {
        var placements = new List<Placement>();
        for (var r = 0; r < GridSize; r++)
        for (var c = 0; c < GridSize; c++)
        {
            if (grid[r][c].Kind != CamelotCellKind.Empty)
                continue;
            placements.Add(RollCell(r, c, bonus: true, rng, purchasedBonus));
        }

        return placements;
    }

    private static List<Placement> RevealFullBonusBoard(
        CamelotPhaseResult phase,
        CamelotCell[][] grid,
        List<Placement> placements,
        ref int scatterCount,
        Random rng)
    {
        var revealOrder = placements
            .Where(p => p.Kind != CamelotCellKind.Multiplier)
            .OrderBy(_ => rng.Next())
            .ToList();

        foreach (var p in revealOrder)
        {
            if (p.Kind == CamelotCellKind.Scatter)
                scatterCount++;
            ApplyPlacement(grid, p);
            var reveal = ToReveal(p);
            phase.Reveals.Add(reveal);
            phase.Steps.Add(new CamelotAnimationStep { Type = "reveal", Reveal = reveal });
        }

        return revealOrder;
    }

    private class CamelotCell
    {
        public CamelotCellKind Kind { get; set; } = CamelotCellKind.Empty;
        public decimal Nominal { get; set; }
        public int Mult { get; set; }
    }

    private record Placement(int R, int C, CamelotCellKind Kind, decimal Nominal, int Mult);

    private static CamelotCell[][] CreateEmptyGrid()
    {
        var grid = new CamelotCell[GridSize][];
        for (var r = 0; r < GridSize; r++)
        {
            grid[r] = new CamelotCell[GridSize];
            for (var c = 0; c < GridSize; c++)
                grid[r][c] = new CamelotCell();
        }

        return grid;
    }

    private static List<Placement> PlaceClusterWave(
        CamelotCell[][] grid,
        int count,
        bool bonus,
        Random rng,
        bool purchasedBonus = false,
        bool forceFirstCollector = false)
    {
        var result = new List<Placement>();
        var empty = GetEmptyCells(grid);
        if (empty.Count == 0)
            return result;

        var pendingForcedCollector = forceFirstCollector;

        while (result.Count < count && empty.Count > 0)
        {
            var start = empty[rng.Next(empty.Count)];
            var cluster = GrowCluster(start, grid, Math.Min(rng.Next(2, 7), count - result.Count), rng);
            foreach (var (r, c) in cluster)
            {
                if (grid[r][c].Kind != CamelotCellKind.Empty)
                    continue;

                result.Add(pendingForcedCollector
                    ? new Placement(r, c, CamelotCellKind.Collector, 0, 0)
                    : RollCell(r, c, bonus, rng, purchasedBonus));
                pendingForcedCollector = false;
                empty.Remove((r, c));
            }

            empty = GetEmptyCells(grid);
        }

        return result;
    }

    private static List<(int R, int C)> GrowCluster((int R, int C) start, CamelotCell[][] grid, int size, Random rng)
    {
        var cluster = new List<(int, int)> { start };
        var frontier = new List<(int, int)> { start };

        while (cluster.Count < size && frontier.Count > 0)
        {
            var idx = rng.Next(frontier.Count);
            var (r, c) = frontier[idx];
            var neighbors = GetNeighbors(r, c)
                .Where(n => grid[n.R][n.C].Kind == CamelotCellKind.Empty && !cluster.Contains((n.R, n.C)))
                .OrderBy(_ => rng.Next())
                .ToList();

            if (neighbors.Count == 0)
            {
                frontier.RemoveAt(idx);
                continue;
            }

            var next = neighbors[0];
            cluster.Add(next);
            frontier.Add(next);
        }

        return cluster;
    }

    private static Placement RollCell(int r, int c, bool bonus, Random rng, bool purchasedBonus = false)
    {
        var roll = rng.Next(10000);
        var scatterChance = bonus ? 12 : 258;
        var collectorChance = bonus
            ? (purchasedBonus ? 13 : 2)
            : 2;
        var multChance = bonus ? 3 : 2;

        if (roll < scatterChance)
            return new Placement(r, c, CamelotCellKind.Scatter, 0, 0);
        if (roll < scatterChance + collectorChance)
            return new Placement(r, c, CamelotCellKind.Collector, 0, 0);
        if (roll < scatterChance + collectorChance + multChance)
        {
            var factor = PickWeighted(bonus ? BonusMultWeights : NormalMultWeights, rng);
            return new Placement(r, c, CamelotCellKind.Multiplier, 0, factor);
        }

        var nominal = PickWeightedNominal(bonus ? BonusCoinWeights : NormalCoinWeights, rng);
        var kind = GetCoinKind(nominal, bonus);
        return new Placement(r, c, kind, nominal, 0);
    }

    private static CamelotCellKind GetCoinKind(decimal nominal, bool bonus)
    {
        if (bonus)
        {
            return nominal switch
            {
                <= 3m => CamelotCellKind.Bronze,
                <= 25m => CamelotCellKind.Silver,
                _ => CamelotCellKind.Gold
            };
        }

        return nominal switch
        {
            <= 0.5m => CamelotCellKind.Bronze,
            <= 5m => CamelotCellKind.Silver,
            _ => CamelotCellKind.Gold
        };
    }

    private static List<Placement> BuildRevealOrder(List<Placement> placements, Random rng) =>
        placements.Where(p => p.Kind != CamelotCellKind.Multiplier).OrderBy(_ => rng.Next()).ToList();

    private static void ApplyPlacement(CamelotCell[][] grid, Placement p)
    {
        grid[p.R][p.C] = new CamelotCell
        {
            Kind = p.Kind,
            Nominal = p.Nominal,
            Mult = p.Mult
        };
    }

    private static CamelotRevealEvent ToReveal(Placement p) => new()
    {
        Row = p.R,
        Col = p.C,
        Kind = p.Kind switch
        {
            CamelotCellKind.Bronze => "bronze",
            CamelotCellKind.Silver => "silver",
            CamelotCellKind.Gold => "gold",
            CamelotCellKind.Collector => "collector",
            CamelotCellKind.Scatter => "scatter",
            CamelotCellKind.Multiplier => "multiplier",
            _ => "empty"
        },
        Nominal = p.Kind is CamelotCellKind.Bronze or CamelotCellKind.Silver or CamelotCellKind.Gold ? p.Nominal : null,
        Multiplier = p.Kind == CamelotCellKind.Multiplier ? p.Mult : null
    };

    private static CamelotRevealEvent ToRevealFromCell(CamelotCell[][] grid, int r, int c)
    {
        var cell = grid[r][c];
        return new CamelotRevealEvent
        {
            Row = r,
            Col = c,
            Kind = cell.Kind switch
            {
                CamelotCellKind.Bronze => "bronze",
                CamelotCellKind.Silver => "silver",
                CamelotCellKind.Gold => "gold",
                CamelotCellKind.Collector => "collector",
                CamelotCellKind.Scatter => "scatter",
                CamelotCellKind.Multiplier => "multiplier",
                _ => "empty"
            },
            Nominal = cell.Kind is CamelotCellKind.Bronze or CamelotCellKind.Silver or CamelotCellKind.Gold
                ? cell.Nominal
                : null,
            Multiplier = cell.Kind == CamelotCellKind.Multiplier ? cell.Mult : null
        };
    }

    private static void ClearCoins(CamelotCell[][] grid)
    {
        for (var r = 0; r < GridSize; r++)
        for (var c = 0; c < GridSize; c++)
        {
            if (grid[r][c].Kind is CamelotCellKind.Collector or CamelotCellKind.Scatter)
                continue;
            grid[r][c] = new CamelotCell();
        }
    }

    private static decimal SumCoinNominals(CamelotCell[][] grid, bool excludeCollector)
    {
        decimal sum = 0;
        for (var r = 0; r < GridSize; r++)
        for (var c = 0; c < GridSize; c++)
        {
            var cell = grid[r][c];
            if (cell.Kind is CamelotCellKind.Bronze or CamelotCellKind.Silver or CamelotCellKind.Gold)
                sum += cell.Nominal;
            if (!excludeCollector && cell.Kind == CamelotCellKind.Collector)
                sum += cell.Nominal;
        }

        return sum;
    }

    private static decimal SumAllNominals(CamelotCell[][] grid)
    {
        decimal sum = 0;
        for (var r = 0; r < GridSize; r++)
        for (var c = 0; c < GridSize; c++)
            sum += grid[r][c].Nominal;
        return Math.Round(sum, 2);
    }

    private static void MultiplyAllNominals(CamelotCell[][] grid, int factor)
    {
        for (var r = 0; r < GridSize; r++)
        for (var c = 0; c < GridSize; c++)
        {
            var cell = grid[r][c];
            if (cell.Nominal <= 0)
                continue;
            if (cell.Kind is not (CamelotCellKind.Bronze or CamelotCellKind.Silver or CamelotCellKind.Gold
                or CamelotCellKind.Collector))
                continue;
            cell.Nominal = Math.Round(cell.Nominal * factor, 2);
        }
    }

    private static List<(int R, int C)> GetEmptyCells(CamelotCell[][] grid)
    {
        var list = new List<(int, int)>();
        for (var r = 0; r < GridSize; r++)
        for (var c = 0; c < GridSize; c++)
            if (grid[r][c].Kind == CamelotCellKind.Empty)
                list.Add((r, c));
        return list;
    }

    private static IEnumerable<(int R, int C)> GetNeighbors(int r, int c)
    {
        if (r > 0) yield return (r - 1, c);
        if (r < GridSize - 1) yield return (r + 1, c);
        if (c > 0) yield return (r, c - 1);
        if (c < GridSize - 1) yield return (r, c + 1);
    }

    private static decimal PickWeightedNominal((decimal Nominal, int Weight)[] weights, Random rng)
    {
        var total = weights.Sum(w => w.Weight);
        var roll = rng.Next(total);
        var acc = 0;
        foreach (var (nominal, weight) in weights)
        {
            acc += weight;
            if (roll < acc)
                return nominal;
        }

        return weights[^1].Nominal;
    }

    private static int PickWeighted((int Value, int Weight)[] weights, Random rng)
    {
        var total = weights.Sum(w => w.Weight);
        var roll = rng.Next(total);
        var acc = 0;
        foreach (var (value, weight) in weights)
        {
            acc += weight;
            if (roll < acc)
                return value;
        }

        return weights[^1].Value;
    }
}
