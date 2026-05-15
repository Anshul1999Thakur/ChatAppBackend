using ChatApp.Data;
using ChatApp.DTOs;
using ChatApp.Helpers;
using ChatApp.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(
            MongoDbContext context,
            IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            var existingUser = await _context.Users
                .Find(x => x.Email == dto.Email)
                .FirstOrDefaultAsync();

            if (existingUser != null)
                return BadRequest("Email already exists");

            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsOnline = false
            };

            await _context.Users.InsertOneAsync(user);

            return Ok("Registered Successfully");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var user = await _context.Users
                .Find(x => x.Email == dto.Email)
                .FirstOrDefaultAsync();

            if (user == null)
                return BadRequest("User not found");

            bool valid = BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash);

            if (!valid)
                return BadRequest("Wrong password");

            var token = JwtHelper.GenerateToken(
                user.Id,
                user.FullName,
                user.Email,
                _config["Jwt:Key"]);

            return Ok(new
            {
                token,
                userId = user.Id,
                name = user.FullName
            });
        }
    }
}