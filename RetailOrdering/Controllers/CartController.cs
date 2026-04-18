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
public class CartController : ControllerBase
{
    private readonly AppDbContext _context;

    public CartController(AppDbContext context)
    {
        _context = context;
    }

    private int GetUserId()
    {
        return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    }

    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var userId = GetUserId();

        var cart = await _context.Carts
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId, UpdatedAt = DateTime.UtcNow };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        var cartDto = new CartDto
        {
            Id = cart.Id,
            UserId = cart.UserId,
            UpdatedAt = cart.UpdatedAt,
            Items = cart.Items?.Select(i => new CartItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? string.Empty,
                ProductImageUrl = i.Product?.ImageUrl ?? string.Empty,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList() ?? new List<CartItemDto>(),
            TotalAmount = cart.Items?.Sum(i => i.Quantity * i.Price) ?? 0
        };

        return Ok(cartDto);
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddToCart(AddToCartDto dto)
    {
        var userId = GetUserId();

        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null)
            return BadRequest(new { message = "Product not found" });

        if (product.Stock < dto.Quantity)
            return BadRequest(new { message = "Insufficient stock" });

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId, UpdatedAt = DateTime.UtcNow };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();
        }

        var cartItem = cart.Items?.FirstOrDefault(i => i.ProductId == dto.ProductId);

        if (cartItem == null)
        {
            cartItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                Price = product.Price
            };
            _context.CartItems.Add(cartItem);
        }
        else
        {
            cartItem.Quantity += dto.Quantity;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Item added to cart" });
    }

    [HttpPut("item/{itemId}")]
    public async Task<IActionResult> UpdateCartItem(int itemId, UpdateCartItemDto dto)
    {
        var userId = GetUserId();

        var cartItem = await _context.CartItems
            .Include(ci => ci.Cart)
            .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.UserId == userId);

        if (cartItem == null)
            return NotFound();

        if (dto.Quantity <= 0)
        {
            _context.CartItems.Remove(cartItem);
        }
        else
        {
            var product = await _context.Products.FindAsync(cartItem.ProductId);
            if (product != null && product.Stock < dto.Quantity)
                return BadRequest(new { message = "Insufficient stock" });

            cartItem.Quantity = dto.Quantity;
        }

        if (cartItem.Cart != null)
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Cart updated" });
    }

    [HttpDelete("item/{itemId}")]
    public async Task<IActionResult> RemoveFromCart(int itemId)
    {
        var userId = GetUserId();

        var cartItem = await _context.CartItems
            .Include(ci => ci.Cart)
            .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.Cart.UserId == userId);

        if (cartItem == null)
            return NotFound();

        _context.CartItems.Remove(cartItem);

        if (cartItem.Cart != null)
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Item removed from cart" });
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearCart()
    {
        var userId = GetUserId();

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart != null && cart.Items != null)
        {
            _context.CartItems.RemoveRange(cart.Items);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Cart cleared" });
    }
}
