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

            // Relacja między Reservation a Guest
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Guest)
                .WithMany(g => g.Reservations)
                .HasForeignKey(r => r.GuestId)
                .OnDelete(DeleteBehavior.Cascade); // Usuwanie rezerwacji, gdy gość jest usuwany

            // Relacja między Reservation a Room
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Room)
                .WithMany()
                .HasForeignKey(r => r.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relacja między Reservation a RoomType
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.RoomType)
                .WithMany()
                .HasForeignKey(r => r.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relacja między Guest a Reservation (dwukierunkowa)
            modelBuilder.Entity<Guest>()
                .HasMany(g => g.Reservations)
                .WithOne(r => r.Guest)
                .HasForeignKey(r => r.GuestId);

            // Relacja między Payment a Guest
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Guest)
                .WithMany(g => g.Payments)
                .HasForeignKey(p => p.GuestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relacje dla powiązanych tabel (Services, Payments, Documents)
            modelBuilder.Entity<Reservation>()
                .HasMany(r => r.ServicesUsed)
                .WithOne(su => su.Reservation)
                .HasForeignKey(su => su.ReservationId);

            modelBuilder.Entity<Reservation>()
                .HasMany(r => r.Payments)
                .WithOne(p => p.Reservation)
                .HasForeignKey(p => p.ReservationId);

            modelBuilder.Entity<Reservation>()
                .HasMany(r => r.Documents)
                .WithOne(d => d.Reservation)
                .HasForeignKey(d => d.ReservationId);
        }

    }
}
