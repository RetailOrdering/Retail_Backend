using RetailOrdering.Models;
using RetailOrdering.Repositories;

namespace RetailOrdering.Services;

public interface ICartService
{
    Task<Cart> GetCartAsync(int userId);
    Task<Cart> AddItemAsync(int userId, int productId, int quantity);
    Task<Cart> UpdateItemQuantityAsync(int userId, int productId, int quantity);
    Task<Cart> RemoveItemAsync(int userId, int productId);
    Task ClearCartAsync(int userId);
}

public class CartService : ICartService
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;

    public CartService(ICartRepository cartRepo, IProductRepository productRepo)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
    }

    public async Task<Cart> GetCartAsync(int userId)
        => await _cartRepo.GetOrCreateCartAsync(userId);

    public async Task<Cart> AddItemAsync(int userId, int productId, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be at least 1.");

        var product = await _productRepo.GetByIdAsync(productId)
            ?? throw new KeyNotFoundException($"Product with ID {productId} not found.");

        if (product.Stock < quantity)
            throw new InvalidOperationException($"Only {product.Stock} units available for '{product.Name}'.");

        var cart = await _cartRepo.GetOrCreateCartAsync(userId);
        var existingItem = await _cartRepo.GetCartItemAsync(cart.Id, productId);

        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
            await _cartRepo.UpdateItemAsync(existingItem);
        }
        else
        {
            var newItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = product.Price
            };
            await _cartRepo.AddItemAsync(newItem);
        }

        return await _cartRepo.GetCartByUserIdAsync(userId) ?? cart;
    }

    public async Task<Cart> UpdateItemQuantityAsync(int userId, int productId, int quantity)
    {
        if (quantity < 0)
            throw new ArgumentException("Quantity cannot be negative.");

        var cart = await _cartRepo.GetOrCreateCartAsync(userId);
        var item = await _cartRepo.GetCartItemAsync(cart.Id, productId)
            ?? throw new KeyNotFoundException("Item not found in cart.");

        if (quantity == 0)
        {
            await _cartRepo.RemoveItemAsync(item);
        }
        else
        {
            var product = await _productRepo.GetByIdAsync(productId);
            if (product != null && product.Stock < quantity)
                throw new InvalidOperationException($"Only {product.Stock} units available.");

            item.Quantity = quantity;
            await _cartRepo.UpdateItemAsync(item);
        }

        return await _cartRepo.GetCartByUserIdAsync(userId) ?? cart;
    }

    public async Task<Cart> RemoveItemAsync(int userId, int productId)
    {
        var cart = await _cartRepo.GetOrCreateCartAsync(userId);
        var item = await _cartRepo.GetCartItemAsync(cart.Id, productId)
            ?? throw new KeyNotFoundException("Item not found in cart.");

        await _cartRepo.RemoveItemAsync(item);
        return await _cartRepo.GetCartByUserIdAsync(userId) ?? cart;
    }

    public async Task ClearCartAsync(int userId)
    {
        var cart = await _cartRepo.GetCartByUserIdAsync(userId);
        if (cart != null)
            await _cartRepo.ClearCartAsync(cart.Id);
    }
}