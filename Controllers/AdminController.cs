using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CloudAdvisor.Data;
using CloudAdvisor.Models.Domain;
using CloudAdvisor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CloudAdvisor.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, IConfiguration config, ILogger<AdminController> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        // Helper schemas for JSON config loading
        private class RuleDefinition
        {
            public string RuleId { get; set; } = string.Empty;
            public string TriggerFeature { get; set; } = string.Empty;
            public string Condition { get; set; } = string.Empty;
            public string AwsService { get; set; } = string.Empty;
            public string ServiceCategory { get; set; } = string.Empty;
            public string Tier { get; set; } = string.Empty;
            public string Priority { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
        }

        private class RuleConfig
        {
            public List<RuleDefinition> Rules { get; set; } = new List<RuleDefinition>();
        }

        private class PricingItem
        {
            public string Service { get; set; } = string.Empty;
            public string Tier { get; set; } = string.Empty;
            public decimal MonthlyUSD { get; set; }
            public string Unit { get; set; } = string.Empty;
        }

        private class PricingConfig
        {
            public List<PricingItem> Pricing { get; set; } = new List<PricingItem>();
        }

        // ==========================================
        // CONFIG SERVICES API (FOR FRONTEND INTERFACES)
        // ==========================================

        [HttpGet("services")]
        public async Task<IActionResult> GetServices()
        {
            try
            {
                var rulesList = await LoadRulesAsync();
                var pricingList = await LoadPricingAsync();

                var services = new List<object>();
                for (int i = 0; i < rulesList.Count; i++)
                {
                    var rule = rulesList[i];
                    var pricing = pricingList.FirstOrDefault(p => 
                        p.Service.Equals(rule.AwsService, StringComparison.OrdinalIgnoreCase) && 
                        p.Tier.Equals(rule.Tier, StringComparison.OrdinalIgnoreCase));

                    // Fallback to service match
                    if (pricing == null)
                    {
                        pricing = pricingList.FirstOrDefault(p => p.Service.Equals(rule.AwsService, StringComparison.OrdinalIgnoreCase));
                    }

                    services.Add(new
                    {
                        id = i + 1,
                        serviceName = rule.AwsService,
                        description = rule.Reason,
                        monthlyCost = pricing?.MonthlyUSD ?? 0.00m,
                        triggerFeature = rule.TriggerFeature
                    });
                }

                return Ok(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching services list for admin");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("rules")]
        public async Task<IActionResult> GetRules()
        {
            try
            {
                var rules = await LoadRulesAsync();
                return Ok(rules);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class RuleUpdateInputDto
        {
            public string Justification { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public string Condition { get; set; } = string.Empty;
        }

        [HttpPut("rules/{ruleId}")]
        public async Task<IActionResult> UpdateRule(string ruleId, [FromBody] RuleUpdateInputDto dto)
        {
            if (dto == null) return BadRequest(new { error = "Request body is empty" });

            try
            {
                var rules = await LoadRulesAsync();
                RuleDefinition? targetRule = null;

                // support both integer ID index (1-based) and RULE_xxx string IDs
                if (int.TryParse(ruleId, out int index))
                {
                    if (index >= 1 && index <= rules.Count)
                    {
                        targetRule = rules[index - 1];
                    }
                }
                else
                {
                    targetRule = rules.FirstOrDefault(r => r.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase));
                }

                if (targetRule == null)
                {
                    return NotFound(new { error = $"Rule '{ruleId}' not found." });
                }

                // Update text
                string updatedText = !string.IsNullOrWhiteSpace(dto.Justification) ? dto.Justification : dto.Reason;
                if (!string.IsNullOrWhiteSpace(updatedText))
                {
                    targetRule.Reason = updatedText;
                }
                if (!string.IsNullOrWhiteSpace(dto.Condition))
                {
                    targetRule.Condition = dto.Condition;
                }

                await SaveRulesAsync(rules);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("pricing")]
        public async Task<IActionResult> GetPricing()
        {
            try
            {
                var pricing = await LoadPricingAsync();
                return Ok(pricing);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class PricingUpdateInputDto
        {
            public int Id { get; set; }
            public decimal Cost { get; set; }
            public string Service { get; set; } = string.Empty;
            public string Tier { get; set; } = string.Empty;
            public decimal MonthlyUSD { get; set; }
        }

        [HttpPut("pricing")]
        public async Task<IActionResult> UpdatePricing([FromBody] PricingUpdateInputDto dto)
        {
            if (dto == null) return BadRequest(new { error = "Request body is empty" });

            try
            {
                var pricingList = await LoadPricingAsync();
                PricingItem? targetItem = null;

                // If Id is passed and matches index 1..N
                if (dto.Id >= 1 && dto.Id <= pricingList.Count)
                {
                    // Wait, let's load rules to map Id to the correct service+tier!
                    var rules = await LoadRulesAsync();
                    if (dto.Id - 1 < rules.Count)
                    {
                        var rule = rules[dto.Id - 1];
                        targetItem = pricingList.FirstOrDefault(p => 
                            p.Service.Equals(rule.AwsService, StringComparison.OrdinalIgnoreCase) && 
                            p.Tier.Equals(rule.Tier, StringComparison.OrdinalIgnoreCase));
                    }
                }
                
                // Fallback to match by service name and tier
                if (targetItem == null && !string.IsNullOrWhiteSpace(dto.Service))
                {
                    targetItem = pricingList.FirstOrDefault(p => 
                        p.Service.Equals(dto.Service, StringComparison.OrdinalIgnoreCase) && 
                        (string.IsNullOrWhiteSpace(dto.Tier) || p.Tier.Equals(dto.Tier, StringComparison.OrdinalIgnoreCase)));
                }

                if (targetItem == null && dto.Id >= 1 && dto.Id <= pricingList.Count)
                {
                    targetItem = pricingList[dto.Id - 1];
                }

                if (targetItem == null)
                {
                    return NotFound(new { error = "Pricing configuration entry not found." });
                }

                decimal cost = dto.Cost != 0 ? dto.Cost : dto.MonthlyUSD;
                targetItem.MonthlyUSD = cost;

                await SavePricingAsync(pricingList);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==========================================
        // USER MANAGEMENT APIs
        // ==========================================

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    userId = u.UserId,
                    fullName = u.FullName,
                    email = u.Email,
                    role = u.Role,
                    createdAt = u.CreatedAt,
                    isActive = u.IsActive,
                    isSuspended = !u.IsActive,
                    analysisCount = _context.AnalysisSessions.Count(s => s.UserId == u.UserId)
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUserDetails(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found." });
            }

            var sessions = await _context.AnalysisSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UploadedAt)
                .Select(s => new
                {
                    sessionId = s.SessionId,
                    projectName = s.ProjectName,
                    status = s.Status.ToString(),
                    totalCost = s.TotalCost,
                    createdAt = s.UploadedAt
                })
                .ToListAsync();

            return Ok(new
            {
                userId = user.UserId,
                fullName = user.FullName,
                email = user.Email,
                role = user.Role,
                createdAt = user.CreatedAt,
                isActive = user.IsActive,
                isSuspended = !user.IsActive,
                sessions = sessions
            });
        }

        [HttpGet("users/{userId}/sessions")]
        public async Task<IActionResult> GetUserSessions(Guid userId)
        {
            var sessions = await _context.AnalysisSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UploadedAt)
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpPut("users/{userId}/suspend")]
        public async Task<IActionResult> SuspendUser(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found." });
            }

            if (user.Role == "Admin" || user.Email == "sulav010203@gmail.com")
            {
                return StatusCode(403, new { error = "Admin accounts cannot be suspended." });
            }

            user.IsActive = !user.IsActive; // Toggle active status
            await _context.SaveChangesAsync();

            return Ok(new { success = true, isActive = user.IsActive });
        }

        [HttpPatch("users/{userId}/deactivate")]
        public async Task<IActionResult> DeactivateUser(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found." });
            }

            if (user.Role == "Admin" || user.Email == "sulav010203@gmail.com")
            {
                return StatusCode(403, new { error = "Admin accounts cannot be deactivated." });
            }

            user.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, isActive = user.IsActive });
        }

        // ==========================================
        // PLATFORM SESSIONS LIST API
        // ==========================================

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions([FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var query = _context.AnalysisSessions
                .Include(s => s.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<SessionStatus>(status, true, out var statusEnum))
            {
                query = query.Where(s => s.Status == statusEnum);
            }

            if (from.HasValue)
            {
                query = query.Where(s => s.UploadedAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(s => s.UploadedAt <= to.Value);
            }

            var sessions = await query
                .OrderByDescending(s => s.UploadedAt)
                .Select(s => new
                {
                    id = s.SessionId,
                    sessionId = s.SessionId,
                    userId = s.UserId,
                    userName = s.User != null ? s.User.FullName : "Unknown User",
                    userEmail = s.User != null ? s.User.Email : string.Empty,
                    projectName = s.ProjectName,
                    status = s.Status.ToString(),
                    analysedAt = s.UploadedAt,
                    createdAt = s.UploadedAt,
                    featuresCount = _context.ExtractedFeatures.Count(f => f.SessionId == s.SessionId),
                    totalCost = s.TotalCost
                })
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var totalUsers = await _context.Users.CountAsync(u => u.Role != "Admin");
            var totalSessions = await _context.AnalysisSessions.CountAsync();
            
            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
            var sessionsThisWeek = await _context.AnalysisSessions.CountAsync(s => s.UploadedAt >= oneWeekAgo);

            // Calculate most common recommendation
            var recs = await _context.Recommendations.ToListAsync();
            var mostCommonRecommendation = recs.GroupBy(r => r.AwsService)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "Amazon EC2";

            // Compile sessions per day for the last 7 days
            var sessionsPerDay = new List<object>();
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-i);
                var count = await _context.AnalysisSessions.CountAsync(s => s.UploadedAt.Date == date);
                sessionsPerDay.Add(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    count = count
                });
            }

            return Ok(new
            {
                totalUsers = totalUsers,
                totalSessions = totalSessions,
                sessionsThisWeek = sessionsThisWeek,
                mostCommonRecommendation = mostCommonRecommendation,
                sessionsPerDay = sessionsPerDay
            });
        }

        // ==========================================
        // HELPERS TO LOAD/SAVE JSON CONFIG
        // ==========================================

        private async Task<List<RuleDefinition>> LoadRulesAsync()
        {
            string path = _config["CloudAdvisor:RulesFilePath"] ?? "Rules/RecommendationRules.json";
            if (!System.IO.File.Exists(path)) return new List<RuleDefinition>();

            string json = await System.IO.File.ReadAllTextAsync(path);
            var config = JsonSerializer.Deserialize<RuleConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return config?.Rules ?? new List<RuleDefinition>();
        }

        private async Task SaveRulesAsync(List<RuleDefinition> rules)
        {
            string path = _config["CloudAdvisor:RulesFilePath"] ?? "Rules/RecommendationRules.json";
            var config = new RuleConfig { Rules = rules };
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(path, json);
        }

        private async Task<List<PricingItem>> LoadPricingAsync()
        {
            string path = _config["CloudAdvisor:PricingFilePath"] ?? "Rules/PricingConfig.json";
            if (!System.IO.File.Exists(path)) return new List<PricingItem>();

            string json = await System.IO.File.ReadAllTextAsync(path);
            var config = JsonSerializer.Deserialize<PricingConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return config?.Pricing ?? new List<PricingItem>();
        }

        private async Task SavePricingAsync(List<PricingItem> pricing)
        {
            string path = _config["CloudAdvisor:PricingFilePath"] ?? "Rules/PricingConfig.json";
            var config = new PricingConfig { Pricing = pricing };
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(path, json);
        }
    }
}
