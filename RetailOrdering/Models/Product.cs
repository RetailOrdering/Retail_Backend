namespace RetailOrdering.Models;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public string Packaging { get; set; } = string.Empty;

    public string Brand { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public int Stock { get; set; }


    public bool IsAvailable { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<OrderItem>? OrderItems { get; set; }
    public ICollection<CartItem>?CartItems { get; set; }
}
