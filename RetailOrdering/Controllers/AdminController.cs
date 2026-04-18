using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailOrdering.Data;

namespace RetailOrdering.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalProducts = await _context.Products.CountAsync();
        var totalOrders = await _context.Orders.CountAsync();
        var pendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending");

        var revenue = await _context.Orders
            .Where(o => o.Status == "Delivered")
            .SumAsync(o => o.TotalAmount);

        var recentOrders = await _context.Orders
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new
            {
                o.Id,
                CustomerName = o.User != null ? o.User.Name : string.Empty,
                o.TotalAmount,
                o.Status,
                o.CreatedAt
            })
            .ToListAsync();

        var topProducts = await _context.OrderItems
            .GroupBy(oi => oi.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                TotalSold = g.Sum(oi => oi.Quantity)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(5)
            .Join(_context.Products,
                x => x.ProductId,
                p => p.Id,
                (x, p) => new
                {
                    p.Name,
                    x.TotalSold,
                    p.Price
                })
            .ToListAsync();

        return Ok(new
        {
            totalUsers,
            totalProducts,
            totalOrders,
            pendingOrders,
            revenue,
            recentOrders,
            topProducts
        });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _context.Users
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.CreatedAt,
                OrderCount = u.Orders != null ? u.Orders.Count : 0,
                LoyaltyPoints = u.LoyaltyPoint != null ? u.LoyaltyPoint.Points : 0
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> UpdateUserRole(int userId, [FromBody] UpdateRoleRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound();

        if (request.Role != "Admin" && request.Role != "Customer")
            return BadRequest(new { message = "Invalid role" });

        user.Role = request.Role;
        await _context.SaveChangesAsync();

        return Ok(new { message = "User role updated" });
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStockProducts([FromQuery] int threshold = 10)
    {
        var products = await _context.Products
            .Where(p => p.Stock <= threshold && p.IsAvailable)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Stock,
                p.Price
            })
            .ToListAsync();

        return Ok(products);
    }
}

public class UpdateRoleRequest
{
    public string Role { get; set; } = string.Empty;
}