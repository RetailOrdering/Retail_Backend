using RetailOrdering.DTOs;
using RetailOrdering.Models;
using RetailOrdering.Repositories;

namespace RetailOrdering.Services;

public interface IOrderService
{
    Task<IEnumerable<OrderDto>> GetAllOrdersAsync();
    Task<IEnumerable<OrderDto>> GetUserOrdersAsync(int userId);
    Task<OrderDto> GetOrderByIdAsync(int id, int userId, bool isAdmin = false);
    Task<OrderDto> PlaceOrderAsync(int userId, string deliveryAddress, string? couponcode = null);
    Task<OrderDto> UpdateOrderStatusAsync(int id, string status);
    Task<List<int>> GetReorderProductIdsAsync(int userId, int orderId);
}

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepo;
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IEmailService _emailService;

    public OrderService(
        IOrderRepository orderRepo,
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IEmailService emailService)
    {
        _orderRepo = orderRepo;
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _emailService = emailService;
    }

    public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync()
    {
        var orders = await _orderRepo.GetAllAsync();
        return orders.Select(MapToDto);
    }

    public async Task<IEnumerable<OrderDto>> GetUserOrdersAsync(int userId)
    {
        var orders = await _orderRepo.GetByUserIdAsync(userId);
        return orders.Select(MapToDto);
    }

    public async Task<OrderDto> GetOrderByIdAsync(int id, int userId, bool isAdmin = false)
    {
        Order? order = isAdmin
            ? await _orderRepo.GetByIdAsync(id)
            : await _orderRepo.GetByIdForUserAsync(id, userId);

        if (order == null)
            throw new KeyNotFoundException($"Order with ID {id} not found.");

        return MapToDto(order);
    }

    public async Task<OrderDto> PlaceOrderAsync(int userId, string deliveryAddress, string? couponcode = null)
    {
        var cart = await _cartRepo.GetCartByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Cart not found.");

        if (!cart.Items.Any())
            throw new InvalidOperationException("Your cart is empty. Add items before placing an order.");

        // Validate stock for all items before deducting
        foreach (var item in cart.Items)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId)
                ?? throw new InvalidOperationException($"Product ID {item.ProductId} is no longer available.");

            if (product.Stock < item.Quantity)
                throw new InvalidOperationException($"Insufficient stock for '{product.Name}'. Available: {product.Stock}.");
        }

        // Deduct stock atomically
        foreach (var item in cart.Items)
            await _productRepo.DeductStockAsync(item.ProductId, item.Quantity);

        var orderItems = cart.Items.Select(i => new OrderItem
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            Price = i.Price
        }).ToList();

        decimal totalAmount = orderItems.Sum(i => i.Price * i.Quantity);

        // Coupon discount is applied by CouponService before calling this; 
        // pass discounted amount if provided, otherwise use full total
        var order = new Order
        {
            UserId = userId,
            Items = orderItems,
            TotalAmount = totalAmount,
            Address = deliveryAddress,
            CouponCode = couponcode
        };

        var created = await _orderRepo.CreateAsync(order);

        // Clear the cart
        await _cartRepo.ClearCartAsync(cart.Id);

        // Send confirmation email (stretch feature - non-blocking)
        _ = Task.Run(async () =>
        {
            try
            {
                if (cart.User != null)
                    await _emailService.SendOrderConfirmationAsync(cart.User.Email, created);
            }
            catch { /* Log but don't break order flow */ }
        });

        return MapToDto(created);
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(int id, string status)
    {
        var validStatuses = new[] { "Pending", "Confirmed", "Processing", "Shipped", "Delivered", "Cancelled" };
        if (!validStatuses.Contains(status))
            throw new ArgumentException($"Invalid status. Valid values: {string.Join(", ", validStatuses)}");

        var updated = await _orderRepo.UpdateStatusAsync(id, status)
            ?? throw new KeyNotFoundException($"Order with ID {id} not found.");

        return MapToDto(updated);
    }

    public async Task<List<int>> GetReorderProductIdsAsync(int userId, int orderId)
    {
        var order = await _orderRepo.GetByIdForUserAsync(orderId, userId)
            ?? throw new KeyNotFoundException($"Order with ID {orderId} not found.");

        return order.Items.Select(i => i.ProductId).ToList();
    }

    private static OrderDto MapToDto(Order o) => new()
    {
        Id = o.Id,
        UserId = o.UserId,
        Status = o.Status,
        TotalAmount = o.TotalAmount,
        Address = o.Address,
        CreatedAt = o.CreatedAt,
        Items = o.Items.Select(i => new OrderItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? string.Empty,
            Quantity = i.Quantity,
            Price = i.Price,
        }).ToList()
    };
}