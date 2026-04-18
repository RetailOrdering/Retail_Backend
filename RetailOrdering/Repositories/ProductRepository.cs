using Microsoft.EntityFrameworkCore;
using RetailOrdering.Data;
using RetailOrdering.Models;

namespace RetailOrdering.Repositories;

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllAsync(int? categoryId = null, string? brand = null);
    Task<Product?> GetByIdAsync(int id);
    Task<Product> CreateAsync(Product product);
    Task<Product?> UpdateAsync(int id, Product product);
    Task<bool> DeleteAsync(int id);
    Task<bool> DeductStockAsync(int productId, int quantity);
    Task<bool> RestoreStockAsync(int productId, int quantity);
}

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Product>> GetAllAsync(int? categoryId = null, string? brand = null)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsAvailable)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(brand))
            query = query.Where(p => p.Brand.ToLower().Contains(brand.ToLower()));

        return await query.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
        => await _db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id && p.IsAvailable);

    public async Task<Product> CreateAsync(Product product)
    {
        product.CreatedAt = DateTime.UtcNow;
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<Product?> UpdateAsync(int id, Product updated)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return null;

        product.Name = updated.Name;
        product.Description = updated.Description;
        product.Price = updated.Price;
        product.Brand = updated.Brand;
        product.Stock = updated.Stock;
        product.CategoryId = updated.CategoryId;
        product.Packaging = updated.Packaging;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return false;

        product.IsAvailable = false; // Soft delete
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeductStockAsync(int productId, int quantity)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null || product.Stock < quantity) return false;

        product.Stock -= quantity;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreStockAsync(int productId, int quantity)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return false;

        product.Stock += quantity;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}