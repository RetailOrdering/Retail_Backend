using RetailOrdering.Repositories;
using RetailOrdering.DTOs;
using RetailOrdering.Models;
using RetailOrdering.Repositories;

namespace RetailOrdering.Services;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllProductsAsync(int? categoryId = null, string? brand = null);
    Task<ProductDto> GetProductByIdAsync(int id);
    Task<ProductDto> CreateProductAsync(ProductDto dto);
    Task<ProductDto> UpdateProductAsync(int id, ProductDto dto);
    Task DeleteProductAsync(int id);
}

public class ProductService : IProductService
{
    private readonly IProductRepository _repo;

    public ProductService(IProductRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync(int? categoryId = null, string? brand = null)
    {
        var products = await _repo.GetAllAsync(categoryId, brand);
        return products.Select(MapToDto);
    }

    public async Task<ProductDto> GetProductByIdAsync(int id)
    {
        var product = await _repo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Product with ID {id} not found.");
        return MapToDto(product);
    }

    public async Task<ProductDto> CreateProductAsync(ProductDto dto)
    {
        var product = MapToEntity(dto);
        var created = await _repo.CreateAsync(product);
        return MapToDto(created);
    }

    public async Task<ProductDto> UpdateProductAsync(int id, ProductDto dto)
    {
        var product = MapToEntity(dto);
        var updated = await _repo.UpdateAsync(id, product)
            ?? throw new KeyNotFoundException($"Product with ID {id} not found.");
        return MapToDto(updated);
    }

    public async Task DeleteProductAsync(int id)
    {
        var deleted = await _repo.DeleteAsync(id);
        if (!deleted) throw new KeyNotFoundException($"Product with ID {id} not found.");
    }

    private static ProductDto MapToDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Price = p.Price,
        Brand = p.Brand,
        Stock = p.Stock,
        CategoryId = p.CategoryId,
        CategoryName = p.Category?.Name,
        ImageUrl = p.ImageUrl,
        Packaging = p.Packaging
    };

    private static Product MapToEntity(ProductDto dto) => new()
    {
        Name = dto.Name,
        Description = dto.Description,
        Price = dto.Price,
        Brand = dto.Brand,
        Stock = dto.Stock,
        CategoryId = dto.CategoryId,
        ImageUrl = dto.ImageUrl,
        Packaging = dto.Packaging
    };
}