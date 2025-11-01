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
        public DbSet<ShiftPreference> ShiftPreferences { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<Company> Companies { get; set; }

        // grafiki zmian
        public DbSet<PublishedSchedule> PublishedSchedules { get; set; }

        // data operacyjna sterowana przez nocny audyt
        public DbSet<BusinessDateState> BusinessDateStates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Reservation ↔ Guest
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Guest)
                .WithMany(g => g.Reservations)
                .HasForeignKey(r => r.GuestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reservation ↔ Room
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Room)
                .WithMany(rm => rm.Reservations)
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
                .HasForeignKey(su => su.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reservation ↔ Payments
            modelBuilder.Entity<Reservation>()
                .HasMany(r => r.Payments)
                .WithOne(p => p.Reservation)
                .HasForeignKey(p => p.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reservation ↔ Documents
            modelBuilder.Entity<Reservation>()
                .HasMany(r => r.Documents)
                .WithOne(d => d.Reservation)
                .HasForeignKey(d => d.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Guest ↔ Company
            modelBuilder.Entity<Guest>()
                .HasOne(g => g.Company)
                .WithMany(c => c.Guests)
                .HasForeignKey(g => g.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);

            // ApplicationUser ↔ WorkShift
            modelBuilder.Entity<WorkShift>()
                .HasOne(ws => ws.User)
                .WithMany(u => u.Shifts)
                .HasForeignKey(ws => ws.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ApplicationUser ↔ ShiftPreference
            modelBuilder.Entity<ShiftPreference>()
                .HasOne(sp => sp.User)
                .WithMany(u => u.ShiftPreferences)
                .HasForeignKey(sp => sp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ApplicationUser ↔ LoyaltyPoint (zgodnie z modelem: tylko User)
            modelBuilder.Entity<LoyaltyPoint>()
                .HasOne(lp => lp.User)
                .WithMany(u => u.LoyaltyPoints)
                .HasForeignKey(lp => lp.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // Opis stanu daty operacyjnej (BusinessDateState)
            modelBuilder.Entity<BusinessDateState>()
                .HasKey(b => b.Id);

           
        }
    }
}
