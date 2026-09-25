namespace SportLinea.Models;

public class SportEvent
{
    public int Id { get; set; }
    public string SportType { get; set; } = string.Empty;
    public string SportCategory { get; set; } = SportCategories.Professional;
    public string Title { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public EventStatus Status { get; set; } = EventStatus.AcceptingBets;
    public string? Result { get; set; }
    public int? WinningCoefficientId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Coefficient> Coefficients { get; set; } = new List<Coefficient>();
    public ICollection<Bet> Bets { get; set; } = new List<Bet>();
}
