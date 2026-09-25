namespace SportLinea.Models;

public class WithdrawalRequest
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public WithdrawalRequestStatus Status { get; set; } = WithdrawalRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessedByUserId { get; set; }

    public ApplicationUser Player { get; set; } = null!;
    public ApplicationUser? ProcessedBy { get; set; }
}
