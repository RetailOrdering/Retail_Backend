namespace RetailOrdering.Models;

public class LoyaltyPoint
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public int Points { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
