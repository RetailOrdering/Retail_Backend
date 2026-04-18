using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailOrdering.Data;
using RetailOrdering.Models;
using System.Security.Claims;

namespace RetailOrdering.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LoyaltyController : ControllerBase
{
    private readonly AppDbContext _context;

    public LoyaltyController(AppDbContext context)
    {
        _context = context;
    }

    private int GetUserId()
    {
        return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    }

    [HttpGet("points")]
    public async Task<IActionResult> GetMyPoints()
    {
        var userId = GetUserId();

        var loyaltyPoints = await _context.LoyaltyPoints
            .FirstOrDefaultAsync(lp => lp.UserId == userId);

        if (loyaltyPoints == null)
        {
            loyaltyPoints = new LoyaltyPoint
            {
                UserId = userId,
                Points = 0,
                LastUpdated = DateTime.UtcNow
            };
            _context.LoyaltyPoints.Add(loyaltyPoints);
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            points = loyaltyPoints.Points,
            lastUpdated = loyaltyPoints.LastUpdated,
            // Calculate tier based on points
            tier = GetTier(loyaltyPoints.Points)
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetPointsHistory()
    {
        var userId = GetUserId();

        // Get user's orders to show points earned
        var orders = await _context.Orders
            .Where(o => o.UserId == userId && o.Status == "Delivered")
            .Select(o => new
            {
                orderId = o.Id,
                amount = o.TotalAmount,
                pointsEarned = (int)(o.TotalAmount / 10),
                date = o.CreatedAt
            })
            .ToListAsync();

        var loyaltyPoints = await _context.LoyaltyPoints
            .FirstOrDefaultAsync(lp => lp.UserId == userId);

        return Ok(new
        {
            totalPoints = loyaltyPoints?.Points ?? 0,
            pointsHistory = orders
        });
    }

    [HttpPost("redeem")]
    public async Task<IActionResult> RedeemPoints([FromBody] RedeemPointsRequest request)
    {
        var userId = GetUserId();

        var loyaltyPoints = await _context.LoyaltyPoints
            .FirstOrDefaultAsync(lp => lp.UserId == userId);

        if (loyaltyPoints == null || loyaltyPoints.Points < request.PointsToRedeem)
            return BadRequest(new { message = "Insufficient points" });

        // Calculate discount (e.g., 100 points = $1 discount)
        decimal discountAmount = request.PointsToRedeem / 100m;

        loyaltyPoints.Points -= request.PointsToRedeem;
        loyaltyPoints.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Redeemed {request.PointsToRedeem} points for ${discountAmount} discount",
            remainingPoints = loyaltyPoints.Points,
            discountAmount = discountAmount
        });
    }

    private string GetTier(int points)
    {
        if (points >= 1000) return "Platinum";
        if (points >= 500) return "Gold";
        if (points >= 200) return "Silver";
        if (points >= 50) return "Bronze";
        return "Regular";
    }
}

public class RedeemPointsRequest
{
    public int PointsToRedeem { get; set; }
}