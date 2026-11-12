using Microsoft.EntityFrameworkCore;
using PrivateGym.Api.Models;

namespace PrivateGym.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingGuest> BookingGuests => Set<BookingGuest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Room>()
            .Property(r => r.Equipment)
            .HasColumnType("text[]");

        modelBuilder.Entity<Room>()
            .Property(r => r.MuscleGroups)
            .HasColumnType("text[]");

        // Настройка каскадного удаления: если удаляем бронь, удаляются и её гости
        modelBuilder.Entity<BookingGuest>()
            .HasOne(bg => bg.Booking)
            .WithMany(b => b.Guests)
            .HasForeignKey(bg => bg.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BookingGuest>()
            .HasOne(bg => bg.User)
            .WithMany()
            .HasForeignKey(bg => bg.UserId)
            .OnDelete(DeleteBehavior.SetNull);

    }
}