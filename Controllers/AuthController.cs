using System;
using System.Security.Claims;
using System.Threading.Tasks;
using CloudAdvisor.Models.DTOs;
using CloudAdvisor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudAdvisor.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { error = "Request body is empty" });
            }

            if (string.IsNullOrWhiteSpace(dto.FullName) || dto.FullName.Trim().Length < 2)
            {
                return BadRequest(new { error = "Full Name must be at least 2 characters." });
            }

            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains("@"))
            {
                return BadRequest(new { error = "Please enter a valid email address." });
            }

            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
            {
                return BadRequest(new { error = "Password must be at least 8 characters." });
            }

            // Strong password check: uppercase and number required
            bool hasUpper = false;
            bool hasDigit = false;
            foreach (char c in dto.Password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                if (char.IsDigit(c)) hasDigit = true;
            }

            if (!hasUpper || !hasDigit)
            {
                return BadRequest(new { error = "Password must contain at least one uppercase letter and one number." });
            }

            var result = await _authService.RegisterAsync(dto.FullName, dto.Email, dto.Password);
            if (!result.Success)
            {
                if (result.Error.Contains("use"))
                {
                    return Conflict(new { error = result.Error });
                }
                return BadRequest(new { error = result.Error });
            }

            return Ok(new { message = "Registration successful" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { error = "Email and password are required." });
            }

            var result = await _authService.LoginAsync(dto.Email, dto.Password);
            if (!result.Success)
            {
                if (result.Error.Contains("suspended"))
                {
                    return StatusCode(403, new { error = result.Error });
                }
                return Unauthorized(new { error = result.Error });
            }

            return Ok(new
            {
                token = result.Token,
                user = new
                {
                    id = result.User!.Id,
                    fullName = result.User.FullName,
                    email = result.User.Email,
                    role = result.User.Role
                }
            });
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Credential))
            {
                return BadRequest(new { error = "Google credential is required." });
            }

            var result = await _authService.GoogleLoginAsync(dto.Credential);
            if (!result.Success)
            {
                if (result.Error.Contains("suspended"))
                {
                    return StatusCode(403, new { error = result.Error });
                }
                return Unauthorized(new { error = result.Error });
            }

            return Ok(new
            {
                token = result.Token,
                user = new
                {
                    id = result.User!.Id,
                    fullName = result.User.FullName,
                    email = result.User.Email,
                    role = result.User.Role
                }
            });
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var name = User.FindFirst(ClaimTypes.Name)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userIdStr))
            {
                return Unauthorized();
            }

            return Ok(new
            {
                id = Guid.Parse(userIdStr),
                fullName = name ?? string.Empty,
                email = email ?? string.Empty,
                role = role ?? "User"
            });
        }
    }
}
