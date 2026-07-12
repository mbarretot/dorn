using CleanArchWebApi.Application.Common.Persistence;
using CleanArchWebApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CleanArchWebApi.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<TodoItem> Items => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>(builder =>
        {
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Title).IsRequired().HasMaxLength(200);
        });

        base.OnModelCreating(modelBuilder);
    }
}
