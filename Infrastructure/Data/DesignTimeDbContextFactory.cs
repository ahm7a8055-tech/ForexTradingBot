// In Infrastructure/Data/DesignTimeDbContextFactory.cs
using Infrastructure.Data; // Already here, which is good.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// --- FIX APPLIED HERE: Add the namespace ---
namespace Infrastructure.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // HARDCODE YOUR POSTGRES CONNECTION STRING HERE:
            const string directConnectionString = "Server=localhost;Port=5432;Database=ForexBotDb;User Id=postgres;Password=re110121";

            if (string.IsNullOrEmpty(directConnectionString))
            {
                throw new InvalidOperationException("The direct connection string is empty. Please set it in DesignTimeDbContextFactory.cs.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // This is the crucial line that tells EF Core to use PostgreSQL.
            optionsBuilder.UseNpgsql(directConnectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
// --- END FIX ---