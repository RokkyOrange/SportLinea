namespace SportLinea.Models;

public class Coefficient
{
    public int Id { get; set; }
    public int SportEventId { get; set; }
    public string OutcomeDescription { get; set; } = string.Empty;
    public decimal Value { get; set; }

    public SportEvent SportEvent { get; set; } = null!;
    public ICollection<Bet> Bets { get; set; } = new List<Bet>();
}
