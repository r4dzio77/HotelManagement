using HotelManagement.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Data
{
    public class HotelManagementContext : IdentityDbContext<ApplicationUser>
    {
        public HotelManagementContext(DbContextOptions<HotelManagementContext> options)
            : base(options)
        {
        }

        public DbSet<RoomType> RoomTypes { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<ServiceUsage> ServiceUsages { get; set; }
        public DbSet<LoyaltyPoint> LoyaltyPoints { get; set; }
        public DbSet<WorkShift> WorkShifts { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Guest> Guests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Reservation ↔ Guest
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Guest)
                .WithMany(g => g.Reservations)
                .HasForeignKey(r => r.GuestId)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ Reservation ↔ Room (naprawione)
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Room)
                .WithMany(r => r.Reservations)
                .HasForeignKey(r => r.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // Reservation ↔ RoomType
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.RoomType)
                .WithMany()
                .HasForeignKey(r => r.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Guest ↔ Payment
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Guest)
                .WithMany(g => g.Payments)
                .HasForeignKey(p => p.GuestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reservation ↔ ServiceUsage
            modelBuilder.Entity<Reservation>()
                .HasMany(r => r.ServicesUsed)
                .WithOne(su => su.Reservation)
                .HasForeignKey(su => su.ReservationId);

            // Reservation ↔ Payments
            modelBuilder.Entity<Reservation>()
                .HasMany(r => r.Payments)
                .WithOne(p => p.Reservation)
                .HasForeignKey(p => p.ReservationId);

            // Reservation ↔ Documents
            modelBuilder.Entity<Reservation>()
                .HasMany(r => r.Documents)
                .WithOne(d => d.Reservation)
                .HasForeignKey(d => d.ReservationId);
        }
    }
}
