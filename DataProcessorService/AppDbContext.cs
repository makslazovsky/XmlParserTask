using Microsoft.EntityFrameworkCore;
using SharedLibrary.Models;

namespace DataProcessorService
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Module> Modules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Module>().ToTable("Modules");
        }
    }
}