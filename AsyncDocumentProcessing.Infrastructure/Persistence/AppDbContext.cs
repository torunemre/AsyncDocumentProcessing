using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncDocumentProcessing.Domain.Entities;


namespace AsyncDocumentProcessing.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents => Set<Document>();

        protected override void OnModelCreating(
    ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(AppDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
