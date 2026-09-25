namespace SportLinea.Models;

public class Bet
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public int SportEventId { get; set; }
    public int CoefficientId { get; set; }
    public decimal CoefficientValue { get; set; }
    public string OutcomeDescription { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public BetStatus Status { get; set; } = BetStatus.Accepted;
    public decimal Winnings { get; set; }
    public int? BonusId { get; set; }

    public ApplicationUser Player { get; set; } = null!;
    public SportEvent SportEvent { get; set; } = null!;
    public Coefficient Coefficient { get; set; } = null!;
    public Bonus? Bonus { get; set; }
}
