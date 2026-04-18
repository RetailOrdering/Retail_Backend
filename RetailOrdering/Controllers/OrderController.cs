using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailOrdering.Data;
using RetailOrdering.DTOs;
using RetailOrdering.Models;
using System.Security.Claims;

namespace RetailOrdering.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly AppDbContext _context;

    public OrderController(AppDbContext context)
    {
        _context = context;
    }

    private int GetUserId()
    {
        return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin");
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderDto dto)
    {
        var userId = GetUserId();

        // Get user's cart
        var cart = await _context.Carts
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || cart.Items == null || !cart.Items.Any())
            return BadRequest(new { message = "Cart is empty" });

        // Calculate total
        decimal totalAmount = cart.Items.Sum(i => i.Quantity * i.Price);
        decimal discountAmount = 0;

        // Apply coupon if provided
        if (!string.IsNullOrEmpty(dto.CouponCode))
        {
            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c => c.Code == dto.CouponCode && c.IsActive && c.ExpiryDate > DateTime.UtcNow);

            if (coupon != null && totalAmount >= coupon.MinimumOrderAmount)
            {
                discountAmount = totalAmount * coupon.DiscountPercentage / 100;
                totalAmount -= discountAmount;
            }
        }

        // Check stock
        foreach (var item in cart.Items)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product == null || product.Stock < item.Quantity)
            {
                return BadRequest(new { message = $"Insufficient stock for product: {item.Product?.Name}" });
            }
        }

        // Create order
        var order = new Order
        {
            UserId = userId,
            TotalAmount = totalAmount,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            Address = dto.Address,
            PaymentMethod = dto.PaymentMethod,
            CouponCode = dto.CouponCode,
            DiscountAmount = discountAmount
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Create order items and update stock
        foreach (var item in cart.Items)
        {
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                Price = item.Price
            };
            _context.OrderItems.Add(orderItem);

            // Update stock
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.Stock -= item.Quantity;
            }
        }

        // Add loyalty points (10 points per $10 spent)
        var loyaltyPoints = await _context.LoyaltyPoints.FirstOrDefaultAsync(lp => lp.UserId == userId);
        if (loyaltyPoints != null)
        {
            int pointsEarned = (int)(order.TotalAmount / 10);
            loyaltyPoints.Points += pointsEarned;
            loyaltyPoints.LastUpdated = DateTime.UtcNow;
        }

        // Clear cart
        _context.CartItems.RemoveRange(cart.Items);

        await _context.SaveChangesAsync();

        return Ok(new { orderId = order.Id, message = "Order created successfully" });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = GetUserId();

        var orders = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                UserId = o.UserId,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                Address = o.Address,
                PaymentMethod = o.PaymentMethod,
                CouponCode = o.CouponCode,
                DiscountAmount = o.DiscountAmount,
                Items = o.Items.Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product != null ? i.Product.Name : string.Empty,
                    Quantity = i.Quantity,
                    Price = i.Price
                }).ToList()
            })
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var userId = GetUserId();
        var isAdmin = IsAdmin();

        var order = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        if (!isAdmin && order.UserId != userId)
            return Forbid();

        var orderDto = new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            UserName = order.User?.Name ?? string.Empty,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Address = order.Address,
            PaymentMethod = order.PaymentMethod,
            CouponCode = order.CouponCode,
            DiscountAmount = order.DiscountAmount,
            Items = order.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product != null ? i.Product.Name : string.Empty,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList()
        };

        return Ok(orderDto);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/all")]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                UserId = o.UserId,
                UserName = o.User != null ? o.User.Name : string.Empty,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                Address = o.Address,
                PaymentMethod = o.PaymentMethod,
                CouponCode = o.CouponCode,
                DiscountAmount = o.DiscountAmount
            })
            .ToListAsync();

        return Ok(orders);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateOrderStatus(int id, UpdateOrderStatusDto dto)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
            return NotFound();

        var validStatuses = new[] { "Pending", "Confirmed", "Delivered", "Cancelled" };
        if (!validStatuses.Contains(dto.Status))
            return BadRequest(new { message = "Invalid status" });

        order.Status = dto.Status;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Order status updated" });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound();

        _context.OrderItems.RemoveRange(order.Items);
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Order deleted" });
    }
}