using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using CloudAdvisor.Data;
using CloudAdvisor.Models.Domain;
using CloudAdvisor.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CloudAdvisor.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            ApplicationDbContext context,
            IConfiguration config,
            ILogger<AuthService> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        public async Task<AuthResult> RegisterAsync(string fullName, string email, string password)
        {
            try
            {
                // Check if email already in use
                var existingUser = await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
                if (existingUser)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Error = "Email already in use"
                    };
                }

                // Hash password using BCrypt
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

                var newUser = new User
                {
                    UserId = Guid.NewGuid(),
                    FullName = fullName,
                    Email = email,
                    PasswordHash = passwordHash,
                    Role = "User",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                return new AuthResult
                {
                    Success = true,
                    User = new UserDto
                    {
                        Id = newUser.UserId,
                        FullName = newUser.FullName,
                        Email = newUser.Email,
                        Role = newUser.Role,
                        IsActive = newUser.IsActive,
                        CreatedAt = newUser.CreatedAt
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register user {Email}", email);
                return new AuthResult
                {
                    Success = false,
                    Error = "An unexpected error occurred during registration."
                };
            }
        }

        public async Task<AuthResult> LoginAsync(string email, string password)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
                if (user == null)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Error = "Invalid credentials"
                    };
                }

                // Verify suspension status
                if (!user.IsActive)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Error = "Your account has been suspended"
                    };
                }

                // Verify BCrypt password hash
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                if (!isPasswordValid)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Error = "Invalid credentials"
                    };
                }

                // Generate token
                string token = GenerateJwtToken(user);

                return new AuthResult
                {
                    Success = true,
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.UserId,
                        FullName = user.FullName,
                        Email = user.Email,
                        Role = user.Role,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log in user {Email}", email);
                return new AuthResult
                {
                    Success = false,
                    Error = "An unexpected error occurred during login."
                };
            }
        }

        public async Task<AuthResult> GoogleLoginAsync(string googleToken)
        {
            try
            {
                var settings = new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _config["CloudAdvisor:GoogleClientId"] }
                };

                var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(googleToken, settings);

                // Find user by email
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == payload.Email.ToLower());

                if (user == null)
                {
                    // Create new user if they don't exist
                    user = new User
                    {
                        UserId = Guid.NewGuid(),
                        FullName = payload.Name ?? "Google User",
                        Email = payload.Email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // Random password hash
                        Role = "User",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                else if (!user.IsActive)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Error = "Your account has been suspended"
                    };
                }

                // Generate token
                string token = GenerateJwtToken(user);

                return new AuthResult
                {
                    Success = true,
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.UserId,
                        FullName = user.FullName,
                        Email = user.Email,
                        Role = user.Role,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt
                    }
                };
            }
            catch (Google.Apis.Auth.InvalidJwtException)
            {
                return new AuthResult { Success = false, Error = "Invalid Google token" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to login with Google");
                return new AuthResult { Success = false, Error = "An unexpected error occurred during Google login." };
            }
        }

        public string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("isAdmin", (user.Role == "Admin").ToString().ToLower())
            };

            string keyString = _config["Jwt:Key"] ?? "CloudAdvisorSecretKey2025XYZABCDEFGHIJKLsecure";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            
            double expiryHours = 24;
            if (double.TryParse(_config["Jwt:ExpiryHours"], out double parsedHours))
            {
                expiryHours = parsedHours;
            }

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "CloudAdvisor",
                audience: _config["Jwt:Audience"] ?? "CloudAdvisorUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expiryHours),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
