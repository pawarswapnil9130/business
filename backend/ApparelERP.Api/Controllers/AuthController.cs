using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;
using ApparelERP.Api.Models.Dto;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApparelDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApparelDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("signup")]
        public async Task<ActionResult<string>> Signup(UserRegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            var existingUser = await _context.Users.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower());
            if (existingUser)
            {
                return BadRequest(new { message = "Username already exists." });
            }

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "USER", // Default role
                IsApproved = false, // Must be approved by super admin
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Registration successful. Please wait for Super Admin approval before logging in." });
        }

        [HttpPost("signin")]
        public async Task<ActionResult<UserAuthResponseDto>> Signin(UserLoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
            }

             var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == dto.Username.ToLower());
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            if (!user.IsApproved)
            {
                return Unauthorized(new { message = "Your account has not been approved by the Super Admin yet." });
            }

            // Generate JWT Token
            var token = GenerateJwtToken(user);

            return Ok(new UserAuthResponseDto
            {
                Token = token,
                Username = user.Username,
                Role = user.Role,
                IsApproved = user.IsApproved
            });
        }

        // ==========================================
        // SUPER ADMIN ONLY ENDPOINTS
        // ==========================================

        [HttpGet("users")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        public async Task<ActionResult<IEnumerable<UserSummaryDto>>> GetUsers()
        {
            // Fetch all users except the current super admin (to prevent self-deletion/modification)
            var users = await _context.Users
                .Where(u => u.Username.ToLower() != "admin")
                .Select(u => new UserSummaryDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Role = u.Role,
                    IsApproved = u.IsApproved,
                    CreatedAt = u.CreatedAt
                })
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("users/{id}/approve")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        public async Task<IActionResult> ApproveUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            user.IsApproved = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User '{user.Username}' approved successfully." });
        }

        [HttpDelete("users/{id}")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        public async Task<IActionResult> RemoveUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            if (user.Username.ToLower() == "admin")
            {
                return BadRequest(new { message = "Cannot delete the Super Admin account." });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User '{user.Username}' removed successfully." });
        }

        [HttpPost("onboard")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        public async Task<IActionResult> OnboardUser(UserOnboardDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.Role))
            {
                return BadRequest(new { message = "Username, password and role are required." });
            }

            var existingUser = await _context.Users.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower());
            if (existingUser)
            {
                return BadRequest(new { message = "Username already exists." });
            }

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User '{dto.Username}' onboarded successfully as '{dto.Role}'." });
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            
            // Get secret key from configuration or use fallback
            var secretKey = _configuration["JwtSettings:Secret"] ?? "SuperSecretKeyForApparelERPSystem_2026_SecureKey_MustBeLongEnough";
            var key = Encoding.ASCII.GetBytes(secretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("userId", user.Id.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
