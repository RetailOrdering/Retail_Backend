using Microsoft.EntityFrameworkCore;
using RetailOrdering.Data;
using RetailOrdering.Models;

namespace RetailOrdering.Repositories;

public interface ICouponRepository
{
    Task<IEnumerable<Coupon>> GetAllAsync();
    Task<Coupon?> GetByCodeAsync(string code);
    Task<Coupon?> GetByIdAsync(int id);
    Task<Coupon> CreateAsync(Coupon coupon);
    Task<Coupon?> UpdateAsync(int id, Coupon coupon);
    Task<bool> DeleteAsync(int id);
    Task<bool> IncrementUsageAsync(int couponId);
}

public class CouponRepository : ICouponRepository
{
    private readonly AppDbContext _db;

    public CouponRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Coupon>> GetAllAsync()
        => await _db.Coupons.OrderBy(c => c.Code).ToListAsync();

    public async Task<Coupon?> GetByCodeAsync(string code)
        => await _db.Coupons.FirstOrDefaultAsync(c => c.Code.ToLower() == code.ToLower() && c.IsActive);

    public async Task<Coupon?> GetByIdAsync(int id)
        => await _db.Coupons.FindAsync(id);

    public async Task<Coupon> CreateAsync(Coupon coupon)
    {
        coupon.CreatedAt = DateTime.UtcNow;
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync();
        return coupon;
    }

    public async Task<Coupon?> UpdateAsync(int id, Coupon updated)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null) return null;

        coupon.DiscountPercent = updated.DiscountPercent;
        coupon.MinOrderAmount = updated.MinOrderAmount;
        coupon.MaxUsageCount = updated.MaxUsageCount;
        coupon.ExpiryDate = updated.ExpiryDate;
        coupon.IsActive = updated.IsActive;
        coupon.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return coupon;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null) return false;

        coupon.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IncrementUsageAsync(int couponId)
    {
        var coupon = await _db.Coupons.FindAsync(couponId);
        if (coupon == null) return false;

        coupon.UsedCount++;
        await _db.SaveChangesAsync();
        return true;
    }
}