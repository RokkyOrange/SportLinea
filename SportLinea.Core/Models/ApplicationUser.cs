using Microsoft.AspNetCore.Identity;

namespace SportLinea.Models;

public class ApplicationUser : IdentityUser
{
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? Patronymic { get; set; }
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    public CountryOfResidence CountryOfResidence { get; set; } = CountryOfResidence.USA;
    public decimal Balance { get; set; }
    public int BetBonusTier { get; set; }
    public int RouletteBonusTier { get; set; }
    public int PlinkoBonusTier { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public bool IsDeleted { get; set; }
    public string? BlockReason { get; set; }
    public string? BlockedByUserId { get; set; }
    public bool BalanceResetNoticePending { get; set; }

    public ApplicationUser? BlockedBy { get; set; }

    public ICollection<Bet> Bets { get; set; } = new List<Bet>();
    public ICollection<AccountOperation> Operations { get; set; } = new List<AccountOperation>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Bonus> Bonuses { get; set; } = new List<Bonus>();
    public ICollection<ActionLog> ActionLogs { get; set; } = new List<ActionLog>();

    public string FullName => $"{LastName} {FirstName} {Patronymic}".Trim();
}
