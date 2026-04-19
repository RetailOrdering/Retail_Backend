using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using RetailOrdering.Data;
using RetailOrdering.DTOs;
using RetailOrdering.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
namespace RetailOrdering.Controllers;



    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequestDto request)
        {
            // Check if user exists
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { message = "User already exists" });
            }

            // Create new user
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Create cart for user
            var cart = new Cart
            {
                UserId = user.Id,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Carts.Add(cart);

            // Create loyalty points for user
            var loyaltyPoints = new LoyaltyPoint
            {
                UserId = user.Id,
                Points = 0,
                Description = "Account Created",
                Type = "Credit", // ✅ FIX
                LastUpdated = DateTime.UtcNow
            };
            _context.LoyaltyPoints.Add(loyaltyPoints);

            await _context.SaveChangesAsync();

            // Generate token
            var token = GenerateJwtToken(user);

            return Ok(new AuthResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Token = token
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequestDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            var token = GenerateJwtToken(user);

            return Ok(new AuthResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Token = token
            });
        }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");

        var key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]);

        var tokenHandler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        }),

            Expires = DateTime.UtcNow.AddHours(Convert.ToDouble(jwtSettings["ExpiryHours"])),

            Issuer = jwtSettings["Issuer"],      // ✅ REQUIRED
            Audience = jwtSettings["Audience"],  // ✅ REQUIRED

            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

