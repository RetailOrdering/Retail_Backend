using Microsoft.EntityFrameworkCore;
using RetailOrdering.Data;
using RetailOrdering.Models;

namespace RetailOrdering.Services;

public interface ILoyaltyService
{
    Task<int> GetPointsAsync(int userId);
    Task AddPointsAsync(int userId, decimal orderAmount, int orderId);
    Task<bool> RedeemPointsAsync(int userId, int points);
    Task<IEnumerable<LoyaltyPoint>> GetHistoryAsync(int userId);
}

public class LoyaltyService : ILoyaltyService
{
    private readonly AppDbContext _db;
    // 1 point per ₹10 spent; 100 points = ₹10 discount
    private const decimal PointsPerRupee = 0.1m;

    public LoyaltyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetPointsAsync(int userId)
    {
        var points = await _db.LoyaltyPoints
            .Where(lp => lp.UserId == userId)
            .SumAsync(lp => lp.Points);
        return points;
    }

    public async Task AddPointsAsync(int userId, decimal orderAmount, int orderId)
    {
        var earnedPoints = (int)(orderAmount * PointsPerRupee);
        if (earnedPoints <= 0) return;

        var record = new LoyaltyPoint
        {
            UserId = userId,
            Points = earnedPoints,
            Description = $"Earned from Order #{orderId}",
            Type = "Credit",
            CreatedAt = DateTime.UtcNow
        };

        _db.LoyaltyPoints.Add(record);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> RedeemPointsAsync(int userId, int points)
    {
        if (points <= 0)
            throw new ArgumentException("Points to redeem must be greater than zero.");

        var available = await GetPointsAsync(userId);
        if (available < points)
            throw new InvalidOperationException($"Insufficient loyalty points. You have {available} points.");

        var record = new LoyaltyPoint
        {
            UserId = userId,
            Points = -points,
            Description = $"Redeemed {points} points",
            Type = "Debit",
            CreatedAt = DateTime.UtcNow
        };

        _db.LoyaltyPoints.Add(record);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<LoyaltyPoint>> GetHistoryAsync(int userId)
        => await _db.LoyaltyPoints
            .Where(lp => lp.UserId == userId)
            .OrderByDescending(lp => lp.CreatedAt)
            .ToListAsync();
}