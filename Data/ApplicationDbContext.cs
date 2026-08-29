using CloudAdvisor.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace CloudAdvisor.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<AnalysisSession> AnalysisSessions { get; set; } = null!;
        public DbSet<ExtractedFeature> ExtractedFeatures { get; set; } = null!;
        public DbSet<Recommendation> Recommendations { get; set; } = null!;
        public DbSet<CostEstimate> CostEstimates { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User Email as Unique Index
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Configure Decimal precision for CostEstimate
            modelBuilder.Entity<CostEstimate>()
                .Property(c => c.MonthlyCostUSD)
                .HasPrecision(18, 2);

            // Setup relations and cascade behaviors
            modelBuilder.Entity<AnalysisSession>()
                .HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExtractedFeature>()
                .HasOne(f => f.Session)
                .WithMany(s => s.ExtractedFeatures)
                .HasForeignKey(f => f.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Recommendation>()
                .HasOne(r => r.Session)
                .WithMany(s => s.Recommendations)
                .HasForeignKey(r => r.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CostEstimate>()
                .HasOne(c => c.Session)
                .WithMany(s => s.CostEstimates)
                .HasForeignKey(c => c.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CostEstimate>()
                .HasOne(c => c.Recommendation)
                .WithMany()
                .HasForeignKey(c => c.RecommendationId)
                .OnDelete(DeleteBehavior.NoAction); // Avoid multiple cascade paths
        }
    }
}
