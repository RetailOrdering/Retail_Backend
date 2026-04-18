using Microsoft.EntityFrameworkCore;
using RetailOrdering.Data;
using RetailOrdering.Helpers;
using RetailOrdering.Data;
using RetailOrdering.DTOs;
using RetailOrdering.Helpers;
using RetailOrdering.Models;

namespace RetailOrdering.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(string username, string email, string password, string role = "Customer");
    Task<AuthResponseDto> LoginAsync(string email, string password);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly JwtHelper _jwt;

    public AuthService(AppDbContext db, JwtHelper jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<AuthResponseDto> RegisterAsync(string username, string email, string password, string role = "Customer")
    {
        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new InvalidOperationException("A user with this email already exists.");

        if (await _db.Users.AnyAsync(u => u.Username == username))
            throw new InvalidOperationException("This username is already taken.");

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = _jwt.GenerateToken(user),
            Username = user.Username,
            Email = user.Email,
            Role = user.Role
        };
    }

    public async Task<AuthResponseDto> LoginAsync(string email, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        return new AuthResponseDto
        {
            Token = _jwt.GenerateToken(user),
            Username = user.Username,
            Email = user.Email,
            Role = user.Role
        };
    }
}