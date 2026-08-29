using System.Threading.Tasks;
using CloudAdvisor.Models.Domain;
using CloudAdvisor.Models.DTOs;

namespace CloudAdvisor.Services
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string Token { get; set; } = string.Empty;
        public UserDto? User { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public interface IAuthService
    {
        /// <summary>
        /// Registers a new user, hashes their password, and saves them to the database.
        /// </summary>
        Task<AuthResult> RegisterAsync(string fullName, string email, string password);

        /// <summary>
        /// Validates login credentials, checks suspension status, updates last active date, and generates a JWT.
        /// </summary>
        Task<AuthResult> LoginAsync(string email, string password);

        /// <summary>
        /// Validates a Google ID Token, registers the user if necessary, and generates a JWT.
        /// </summary>
        Task<AuthResult> GoogleLoginAsync(string googleToken);

        /// <summary>
        /// Helper to generate a JWT token for a authenticated user.
        /// </summary>
        string GenerateJwtToken(User user);
    }
}
