namespace SportLinea.Models;

public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public NotificationStatus Status { get; set; } = NotificationStatus.Sent;

    public ApplicationUser User { get; set; } = null!;
}
