namespace SportLinea.Models;

public class AccountOperation
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public OperationType OperationType { get; set; }
    public decimal Amount { get; set; }
    public bool IsFreeBonus { get; set; }
    public bool IsBonusPurchase { get; set; }
    public RouletteSpinKind? RouletteSpinKind { get; set; }
    public PlinkoSpinKind? PlinkoSpinKind { get; set; }
    public ZeusSpinKind? ZeusSpinKind { get; set; }
    public CamelotSpinKind? CamelotSpinKind { get; set; }
    public decimal? CamelotCollectorNominal { get; set; }
    public FruitlandSpinKind? FruitlandSpinKind { get; set; }
    public int? FruitlandGlobalMultiplier { get; set; }
    public decimal? ZeusSideWinAmount { get; set; }
    public decimal? HadesSideWinAmount { get; set; }
    public bool RouletteSpinWon { get; set; }
    public int? PlinkoBountyMultiplier { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? BetId { get; set; }

    public ApplicationUser Player { get; set; } = null!;
    public Bet? Bet { get; set; }
}
