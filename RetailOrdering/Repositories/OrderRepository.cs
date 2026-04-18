using Microsoft.EntityFrameworkCore;
using RetailOrdering.Data;
using RetailOrdering.Models;

namespace RetailOrdering.Repositories;

public interface IOrderRepository
{
    Task<IEnumerable<Order>> GetAllAsync();
    Task<IEnumerable<Order>> GetByUserIdAsync(int userId);
    Task<Order?> GetByIdAsync(int id);
    Task<Order?> GetByIdForUserAsync(int id, int userId);
    Task<Order> CreateAsync(Order order);
    Task<Order?> UpdateStatusAsync(int id, string status);
}

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Order>> GetAllAsync()
        => await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Order>> GetByUserIdAsync(int userId)
        => await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public async Task<Order?> GetByIdAsync(int id)
        => await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<Order?> GetByIdForUserAsync(int id, int userId)
        => await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

    public async Task<Order> CreateAsync(Order order)
    {
        order.CreatedAt = DateTime.UtcNow;
        order.Status = "Pending";
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<Order?> UpdateStatusAsync(int id, string status)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return null;

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return order;
    }
}