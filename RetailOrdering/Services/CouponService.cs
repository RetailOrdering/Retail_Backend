
using RetailOrdering.DTOs;
using RetailOrdering.Models;
using RetailOrdering.Repositories;

namespace RetailOrdering.Services;

public interface ICouponService
{
    Task<IEnumerable<CouponDto>> GetAllCouponsAsync();
    Task<CouponDto> GetCouponByCodeAsync(string code);
    Task<CouponDto> CreateCouponAsync(CouponDto dto);
    Task<CouponDto> UpdateCouponAsync(int id, CouponDto dto);
    Task DeleteCouponAsync(int id);
    Task<(bool IsValid, string Message, decimal Discount)> ValidateAndApplyAsync(string code, decimal orderTotal);
}

public class CouponService : ICouponService
{
    private readonly ICouponRepository _repo;

    public CouponService(ICouponRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<CouponDto>> GetAllCouponsAsync()
    {
        var coupons = await _repo.GetAllAsync();
        return coupons.Select(MapToDto);
    }

    public async Task<CouponDto> GetCouponByCodeAsync(string code)
    {
        var coupon = await _repo.GetByCodeAsync(code)
            ?? throw new KeyNotFoundException($"Coupon '{code}' not found or is inactive.");
        return MapToDto(coupon);
    }

    public async Task<CouponDto> CreateCouponAsync(CouponDto dto)
    {
        var coupon = MapToEntity(dto);
        var created = await _repo.CreateAsync(coupon);
        return MapToDto(created);
    }

    public async Task<CouponDto> UpdateCouponAsync(int id, CouponDto dto)
    {
        var entity = MapToEntity(dto);
        var updated = await _repo.UpdateAsync(id, entity)
            ?? throw new KeyNotFoundException($"Coupon with ID {id} not found.");
        return MapToDto(updated);
    }

    public async Task DeleteCouponAsync(int id)
    {
        var deleted = await _repo.DeleteAsync(id);
        if (!deleted) throw new KeyNotFoundException($"Coupon with ID {id} not found.");
    }

    public async Task<(bool IsValid, string Message, decimal Discount)> ValidateAndApplyAsync(string code, decimal orderTotal)
    {
        var coupon = await _repo.GetByCodeAsync(code);

        if (coupon == null)
            return (false, "Invalid or inactive coupon code.", 0);

        if (coupon.ExpiryDate < DateTime.UtcNow)
            return (false, "This coupon has expired.", 0);

        //if (coupon.MaxUsageCount > 0 && coupon.UsedCount >= coupon.MaxUsageCount)
        //    return (false, "This coupon has reached its maximum usage limit.", 0);

        if (coupon.MinimumOrderAmount > 0 && orderTotal < coupon.MinimumOrderAmount)
            return (false, $"Minimum order amount of ₹{coupon.MinimumOrderAmount:F2} required for this coupon.", 0);

        var discount = Math.Round(orderTotal * (coupon.DiscountPercentage / 100m), 2);
        await _repo.IncrementUsageAsync(coupon.Id);

        return (true, $"Coupon applied! You saved ₹{discount:F2}.", discount);
    }

    private static CouponDto MapToDto(Coupon c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        DiscountPercentage = c.DiscountPercentage,
        MinimumOrderAmount = c.MinimumOrderAmount,
        ExpiryDate = c.ExpiryDate,
        IsActive = c.IsActive
    };

    private static Coupon MapToEntity(CouponDto dto) => new()
    {
        Code = dto.Code.ToUpper(),
        DiscountPercentage = dto.DiscountPercentage,
        MinimumOrderAmount = dto.MinimumOrderAmount,
        ExpiryDate = dto.ExpiryDate,
        IsActive = dto.IsActive
    };
}