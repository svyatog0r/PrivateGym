using Microsoft.EntityFrameworkCore;
using PrivateGym.Api.Models;

namespace PrivateGym.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Room>()
            .Property(r => r.Equipment)
            .HasColumnType("text[]");

        modelBuilder.Entity<Room>()
            .Property(r => r.MuscleGroups)
            .HasColumnType("text[]");

    }
}