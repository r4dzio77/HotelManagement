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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ApplicationUser (dawniej User)
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Reservations)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Comments)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Shifts)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.LoyaltyPoints)
                .WithOne(lp => lp.User)
                .HasForeignKey(lp => lp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Room
            modelBuilder.Entity<Room>()
                .HasMany(r => r.Reservations)
                .WithOne(res => res.Room)
                .HasForeignKey(res => res.RoomId);

            modelBuilder.Entity<Room>()
                .HasOne(r => r.RoomType)
                .WithMany(rt => rt.Rooms)
                .HasForeignKey(r => r.RoomTypeId);

            // Reservation
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

            // Service
            modelBuilder.Entity<Service>()
                .HasMany(s => s.Usages)
                .WithOne(su => su.Service)
                .HasForeignKey(su => su.ServiceId);

            // WorkShift
            modelBuilder.Entity<WorkShift>()
                .HasMany(ws => ws.Comments)
                .WithOne(c => c.WorkShift)
                .HasForeignKey(c => c.WorkShiftId);

            // Comment
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.WorkShift)
                .WithMany(ws => ws.Comments)
                .HasForeignKey(c => c.WorkShiftId);
        }
    }
}
