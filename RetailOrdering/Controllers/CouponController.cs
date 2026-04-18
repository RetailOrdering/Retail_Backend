using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailOrdering.Data;
using RetailOrdering.DTOs;
using RetailOrdering.Models;

namespace RetailOrdering.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CouponController : ControllerBase
{
    private readonly AppDbContext _context;

    public CouponController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveCoupons()
    {
        var coupons = await _context.Coupons
            .Where(c => c.IsActive && c.ExpiryDate > DateTime.UtcNow)
            .Select(c => new CouponDto
            {
                Id = c.Id,
                Code = c.Code,
                DiscountPercentage = c.DiscountPercentage,
                ExpiryDate = c.ExpiryDate,
                IsActive = c.IsActive,
                MinimumOrderAmount = c.MinimumOrderAmount
            })
            .ToListAsync();

        return Ok(coupons);
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateCoupon(ApplyCouponDto dto)
    {
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Code == dto.Code);

        if (coupon == null)
        {
            return Ok(new CouponValidationResultDto
            {
                IsValid = false,
                Message = "Invalid coupon code"
            });
        }

        if (!coupon.IsActive)
        {
            return Ok(new CouponValidationResultDto
            {
                IsValid = false,
                Message = "Coupon is not active"
            });
        }

        if (coupon.ExpiryDate < DateTime.UtcNow)
        {
            return Ok(new CouponValidationResultDto
            {
                IsValid = false,
                Message = "Coupon has expired"
            });
        }

        if (dto.OrderAmount < coupon.MinimumOrderAmount)
        {
            return Ok(new CouponValidationResultDto
            {
                IsValid = false,
                Message = $"Minimum order amount of ${coupon.MinimumOrderAmount} required"
            });
        }

        var discountAmount = dto.OrderAmount * coupon.DiscountPercentage / 100;

        return Ok(new CouponValidationResultDto
        {
            IsValid = true,
            Message = "Coupon applied successfully",
            DiscountPercentage = coupon.DiscountPercentage,
            DiscountAmount = discountAmount
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateCoupon(CreateCouponDto dto)
    {
        var existingCoupon = await _context.Coupons.AnyAsync(c => c.Code == dto.Code);
        if (existingCoupon)
            return BadRequest(new { message = "Coupon code already exists" });

        var coupon = new Coupon
        {
            Code = dto.Code.ToUpper(),
            DiscountPercentage = dto.DiscountPercentage,
            ExpiryDate = dto.ExpiryDate,
            IsActive = true,
            MinimumOrderAmount = dto.MinimumOrderAmount
        };

        _context.Coupons.Add(coupon);
        await _context.SaveChangesAsync();

        return Ok(coupon);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCoupon(int id, CreateCouponDto dto)
    {
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon == null)
            return NotFound();

        coupon.Code = dto.Code.ToUpper();
        coupon.DiscountPercentage = dto.DiscountPercentage;
        coupon.ExpiryDate = dto.ExpiryDate;
        coupon.MinimumOrderAmount = dto.MinimumOrderAmount;

        await _context.SaveChangesAsync();

        return Ok(coupon);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCoupon(int id)
    {
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon == null)
            return NotFound();

        _context.Coupons.Remove(coupon);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Coupon deleted" });
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> ToggleCouponStatus(int id)
    {
        var coupon = await _context.Coupons.FindAsync(id);
        if (coupon == null)
            return NotFound();

        coupon.IsActive = !coupon.IsActive;
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Coupon {(coupon.IsActive ? "activated" : "deactivated")}" });
    }
}
