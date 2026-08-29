using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CloudAdvisor.Models.Domain
{
    public class User
    {
        [Key]
        public Guid UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "User"; // "User" or "Admin"

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<AnalysisSession> Sessions { get; set; } = new List<AnalysisSession>();
    }
}
