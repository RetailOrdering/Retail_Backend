namespace RetailOrdering.Models;

public class Coupon
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public int DiscountPercentage { get; set; }

    public DateTime ExpiryDate { get; set; }

    public bool IsActive { get; set; } = true;

    public decimal MinimumOrderAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  
    public DateTime? LastUpdated { get; set; }

}
