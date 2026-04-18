using Microsoft.EntityFrameworkCore;
using RetailOrdering.Data;
using RetailOrdering.Models;

namespace RetailOrdering.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetCartByUserIdAsync(int userId);
    Task<Cart> GetOrCreateCartAsync(int userId);
    Task<CartItem?> GetCartItemAsync(int cartId, int productId);
    Task AddItemAsync(CartItem item);
    Task UpdateItemAsync(CartItem item);
    Task RemoveItemAsync(CartItem item);
    Task ClearCartAsync(int cartId);
    Task SaveAsync();
}

public class CartRepository : ICartRepository
{
    private readonly AppDbContext _db;

    public CartRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Cart?> GetCartByUserIdAsync(int userId)
        => await _db.Carts
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

    public async Task<Cart> GetOrCreateCartAsync(int userId)
    {
        var cart = await GetCartByUserIdAsync(userId);
        if (cart != null) return cart;

        cart = new Cart { UserId = userId, CreatedAt = DateTime.UtcNow };
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync();
        return cart;
    }

    public async Task<CartItem?> GetCartItemAsync(int cartId, int productId)
        => await _db.CartItems.FirstOrDefaultAsync(i => i.CartId == cartId && i.ProductId == productId);

    public async Task AddItemAsync(CartItem item)
    {
        item.AddedAt = DateTime.UtcNow;
        _db.CartItems.Add(item);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateItemAsync(CartItem item)
    {
        _db.CartItems.Update(item);
        await _db.SaveChangesAsync();
    }

    public async Task RemoveItemAsync(CartItem item)
    {
        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();
    }

    public async Task ClearCartAsync(int cartId)
    {
        var items = await _db.CartItems.Where(i => i.CartId == cartId).ToListAsync();
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync();
    }

    public async Task SaveAsync() => await _db.SaveChangesAsync();
}