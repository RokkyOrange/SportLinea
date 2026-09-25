namespace SportLinea.Models;

public enum PlinkoSymbol
{
    Joker,
    Whiskey,
    Revolver,
    Hat,
    Flag,
    SheriffStar
}

public enum PlinkoGamePhase
{
    AwaitingPicks,
    BountyHunter
}

public enum BountyPlateKind
{
    Multiplier,
    Bullet
}

public class BountyPlateOutcome
{
    public BountyPlateKind Kind { get; init; }
    public int Multiplier { get; init; }
    public int Bullets { get; init; } = 1;
}

/// <summary>
/// Bingo Plinko Wild West — ~96% RTP, теор. max ~6000× ставки (Bounty Hunter).
/// </summary>
public static class PlinkoWildWestConfig
{
    public const int TileCount = 12;
    public const int PicksRequired = 3;
    public const int BountyPlates = 10;
    public const int BountyPlateColumns = 5;
    public const int BulletsToAdvance = 6;
    public const int Level1Spins = 5;
    public const int PurchasedBountyLevel1Spins = 7;
    public const int SpinsOnLevelUp = 3;
    public const int FinalLevelBonusSpins = 1;
    public const int BountyBuyPriceMultiplier = 5;
    public const int MaxBountyLevel1Multiplier = 6;
    public const int MaxBountyLevel2Multiplier = 32;
    public const int MaxBountyLevel3Multiplier = 722;
    public const decimal MainFieldMaxMultiplier = 220m;

    public static decimal GetBountyBuyPrice(decimal stakeAmount) =>
        stakeAmount * BountyBuyPriceMultiplier;

    public static readonly decimal[] AllowedStakes = CrazyTimeWheel.AllowedStakes;

    public static bool IsAllowedStake(decimal amount) => AllowedStakes.Contains(amount);

    public static decimal GetDefaultStake(decimal balance) =>
        AllowedStakes.Where(s => s <= balance).DefaultIfEmpty(AllowedStakes[0]).Max();

  // ~8% шанс Bonus Hunt; на поле 4 звезды шерифа
    public const double BonusHuntChance = 0.08;
    public const int SheriffTilesOnHunt = 4;

    private static readonly (PlinkoSymbol Symbol, int Weight)[] BaseTileWeights =
    [
        (PlinkoSymbol.Joker, 400),
        (PlinkoSymbol.Whiskey, 290),
        (PlinkoSymbol.Revolver, 175),
        (PlinkoSymbol.Hat, 95),
        (PlinkoSymbol.Flag, 40)
    ];

    private static readonly (int Multiplier, int Weight)[] Level1ShotWeights =
    [
        (0, 54), (1, 26), (2, 14), (6, 6)
    ];

    private static readonly (int Multiplier, int Weight)[] Level2ShotWeights =
    [
        (0, 50), (10, 30), (20, 14), (32, 6)
    ];

    private static readonly (int Multiplier, int Weight)[] Level3ShotWeights =
    [
        (0, 54), (50, 26), (150, 12), (400, 6), (722, 2)
    ];

    private const int BulletWeightPerSpin = 23;

    public static int RollBulletReward(Random? rng = null)
    {
        var roll = (rng ?? Random.Shared).Next(100);
        return roll switch
        {
            < 45 => 1,
            < 80 => 2,
            _ => 3
        };
    }

    public static PlinkoSymbol DrawBaseSymbol(Random? rng = null) => PickWeighted(BaseTileWeights, rng);

    public static bool RollBonusHunt(Random? rng = null) =>
        (rng ?? Random.Shared).NextDouble() < BonusHuntChance;

    public static PlinkoSymbol[] BuildBoard(bool bonusHunt, Random? rng = null)
    {
        var random = rng ?? Random.Shared;
        var board = new PlinkoSymbol[TileCount];
        if (!bonusHunt)
        {
            for (var i = 0; i < TileCount; i++)
                board[i] = DrawBaseSymbol(random);
            return board;
        }

        var sheriffPositions = Enumerable.Range(0, TileCount)
            .OrderBy(_ => random.Next())
            .Take(SheriffTilesOnHunt)
            .ToHashSet();

        for (var i = 0; i < TileCount; i++)
            board[i] = sheriffPositions.Contains(i) ? PlinkoSymbol.SheriffStar : DrawBaseSymbol(random);

        return board;
    }

    public static BountyPlateOutcome[] BuildBountyPlates(int level, Random? rng = null)
    {
        var random = rng ?? Random.Shared;
        var multWeights = level switch
        {
            2 => Level2ShotWeights,
            3 => Level3ShotWeights,
            _ => Level1ShotWeights
        };

        var totalMultWeight = multWeights.Sum(w => w.Weight);
        var totalWeight = totalMultWeight + BulletWeightPerSpin;
        var plates = new BountyPlateOutcome[BountyPlates];

        for (var i = 0; i < BountyPlates; i++)
        {
            var roll = random.Next(totalWeight);
            if (roll >= totalMultWeight)
            {
                plates[i] = new BountyPlateOutcome { Kind = BountyPlateKind.Bullet, Bullets = RollBulletReward(random) };
                continue;
            }

            var cumulative = 0;
            foreach (var (mult, weight) in multWeights)
            {
                cumulative += weight;
                if (roll < cumulative)
                {
                    plates[i] = new BountyPlateOutcome { Kind = BountyPlateKind.Multiplier, Multiplier = mult };
                    break;
                }
            }
        }

        return plates;
    }

    public static decimal EvaluateMainPayout(PlinkoSymbol[] picks, decimal stake)
    {
        if (picks.Length != PicksRequired)
            return 0;

        var counts = picks.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());

        if (counts.GetValueOrDefault(PlinkoSymbol.Flag) == 3) return stake * MainFieldMaxMultiplier;
        if (counts.GetValueOrDefault(PlinkoSymbol.Flag) == 2) return stake * 65m;
        if (counts.GetValueOrDefault(PlinkoSymbol.Hat) == 3) return stake * 40m;
        if (counts.GetValueOrDefault(PlinkoSymbol.Revolver) == 3) return stake * 18m;
        if (counts.GetValueOrDefault(PlinkoSymbol.Whiskey) == 3) return stake * 7m;
        if (counts.GetValueOrDefault(PlinkoSymbol.Joker) == 3) return stake * 2.5m;
        if (counts.GetValueOrDefault(PlinkoSymbol.Joker) == 2) return stake * 0.8m;

        return 0;
    }

    public static bool TriggersBountyHunter(PlinkoSymbol[] picks, bool bonusHunt) =>
        bonusHunt && picks.Length == PicksRequired &&
        picks.All(s => s == PlinkoSymbol.SheriffStar);

    public static string GetSymbolLabel(PlinkoSymbol symbol) => symbol switch
    {
        PlinkoSymbol.Joker => "Джокер",
        PlinkoSymbol.Whiskey => "Виски",
        PlinkoSymbol.Revolver => "Револьвер",
        PlinkoSymbol.Hat => "Шляпа",
        PlinkoSymbol.Flag => "Флаг",
        PlinkoSymbol.SheriffStar => "Звезда шерифа",
        _ => symbol.ToString()
    };

    public static string GetSymbolKey(PlinkoSymbol symbol) => symbol switch
    {
        PlinkoSymbol.Joker => "joker",
        PlinkoSymbol.Whiskey => "whiskey",
        PlinkoSymbol.Revolver => "revolver",
        PlinkoSymbol.Hat => "hat",
        PlinkoSymbol.Flag => "flag",
        PlinkoSymbol.SheriffStar => "sheriff",
        _ => "unknown"
    };

    public static string GetSymbolEmoji(PlinkoSymbol symbol) => symbol switch
    {
        PlinkoSymbol.Joker => "🃏",
        PlinkoSymbol.Whiskey => "🥃",
        PlinkoSymbol.Revolver => "🔫",
        PlinkoSymbol.Hat => "🤠",
        PlinkoSymbol.Flag => "🇺🇸",
        PlinkoSymbol.SheriffStar => "⭐",
        _ => "?"
    };

    public static int GetTheoreticalMaxBountyMultiplierSum(bool purchased)
    {
        var l1Spins = purchased ? PurchasedBountyLevel1Spins : Level1Spins;
        var l1MultShots = Math.Max(0, l1Spins - 2);
        var spinsAfterL1 = l1Spins - 2 + SpinsOnLevelUp;
        var l2MultShots = Math.Max(0, spinsAfterL1 - 2);
        var spinsAfterL2 = spinsAfterL1 - 2 + SpinsOnLevelUp + FinalLevelBonusSpins;
        var l3MultShots = Math.Max(0, spinsAfterL2 - 2);

        return l1MultShots * MaxBountyLevel1Multiplier
               + l2MultShots * MaxBountyLevel2Multiplier
               + l3MultShots * MaxBountyLevel3Multiplier;
    }

    public static decimal GetTheoreticalMaxWinMultiplier() =>
        Math.Max(MainFieldMaxMultiplier, GetTheoreticalMaxBountyMultiplierSum(purchased: true));

    public static decimal SimulateRtp(int sessions = 300_000, int seed = 42)
    {
        var rng = new Random(seed);
        decimal total = 0;
        for (var i = 0; i < sessions; i++)
            total += SimulateMainRound(stake: 1m, rng);
        return total / sessions;
    }

    private static decimal SimulateMainRound(decimal stake, Random rng)
    {
        var bonusHunt = RollBonusHunt(rng);
        var board = BuildBoard(bonusHunt, rng);
        var picks = PickRandomTiles(board, rng);

        if (TriggersBountyHunter(picks, bonusHunt))
            return stake * SimulateBountyMultiplierSum(purchased: false, rng);

        return EvaluateMainPayout(picks, stake);
    }

    private static PlinkoSymbol[] PickRandomTiles(PlinkoSymbol[] board, Random rng)
    {
        var indices = Enumerable.Range(0, board.Length).OrderBy(_ => rng.Next()).Take(PicksRequired).ToArray();
        return indices.Select(i => board[i]).ToArray();
    }

    private static int SimulateBountyMultiplierSum(bool purchased, Random rng)
    {
        var level = 1;
        var spinsRemaining = purchased ? PurchasedBountyLevel1Spins : Level1Spins;
        var bulletsOnLevel = 0;
        var totalMultiplierSum = 0;
        BountyPlateOutcome[]? plates = null;
        var revealed = new HashSet<int>();

        while (true)
        {
            if (spinsRemaining <= 0)
                break;

            if (level >= 3 && bulletsOnLevel >= BulletsToAdvance)
                break;

            plates ??= BuildBountyPlates(level, rng);
            var available = Enumerable.Range(0, BountyPlates).Where(i => !revealed.Contains(i)).ToList();
            if (available.Count == 0)
            {
                plates = BuildBountyPlates(level, rng);
                revealed.Clear();
                available = Enumerable.Range(0, BountyPlates).ToList();
            }

            var targetIndex = available[rng.Next(available.Count)];
            var outcome = plates[targetIndex];
            revealed.Add(targetIndex);
            spinsRemaining--;

            var levelAdvanced = false;
            if (outcome.Kind == BountyPlateKind.Bullet)
            {
                bulletsOnLevel += outcome.Bullets;
            }
            else
            {
                totalMultiplierSum += outcome.Multiplier;
            }

            if (bulletsOnLevel >= BulletsToAdvance && level < 3)
            {
                levelAdvanced = true;
                level++;
                bulletsOnLevel = 0;
                spinsRemaining += SpinsOnLevelUp;
                if (level == 3)
                    spinsRemaining += FinalLevelBonusSpins;
                plates = BuildBountyPlates(level, rng);
                revealed.Clear();
            }
            else if (revealed.Count >= BountyPlates && !levelAdvanced)
            {
                plates = BuildBountyPlates(level, rng);
                revealed.Clear();
            }
        }

        return totalMultiplierSum;
    }

    private static PlinkoSymbol PickWeighted((PlinkoSymbol Symbol, int Weight)[] weights, Random? rng = null)
    {
        var random = rng ?? Random.Shared;
        var total = weights.Sum(w => w.Weight);
        var roll = random.Next(total);
        var cumulative = 0;
        foreach (var (symbol, weight) in weights)
        {
            cumulative += weight;
            if (roll < cumulative)
                return symbol;
        }

        return weights[0].Symbol;
    }
}

public class PendingPlinkoGame
{
    public string PlayerId { get; set; } = string.Empty;
    public decimal StakeAmount { get; set; }
    public PlinkoGamePhase Phase { get; set; } = PlinkoGamePhase.AwaitingPicks;
    public bool BonusHunt { get; set; }
    public PlinkoSymbol[] Board { get; set; } = Array.Empty<PlinkoSymbol>();
    public List<int> PickedIndices { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BountyLevel { get; set; } = 1;
    public int SpinsRemaining { get; set; }
    public int BulletsOnLevel { get; set; }
    public int TotalMultiplierSum { get; set; }
    public BountyPlateOutcome[]? CurrentPlates { get; set; }
    public HashSet<int> RevealedPlates { get; set; } = new();
    public bool BountyPurchased { get; set; }
}
