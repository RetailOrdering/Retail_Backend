namespace RetailOrdering.Models;

public class Order
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = "Pending";
    // Pending / Confirmed / Delivered / Cancelled

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Address { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = "COD";

    public string? CouponCode { get; set; }

    public decimal DiscountAmount { get; set; } = 0;

    // Navigation
    public ICollection<OrderItem>? Items { get; set; }
}