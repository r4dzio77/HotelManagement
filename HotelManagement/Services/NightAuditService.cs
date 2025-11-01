using System;
using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelManagement.Services
{
    public class NightAuditService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly NightAuditProgressStore _progressStore;

        public NightAuditService(
            IServiceScopeFactory scopeFactory,
            NightAuditProgressStore progressStore)
        {
            _scopeFactory = scopeFactory;
            _progressStore = progressStore;
        }

        /// <summary>
        /// Uruchamia nocny audyt w tle i zwraca identyfikator postępu.
        /// </summary>
        public Guid StartAudit(string? userId)
        {
            var ap = _progressStore.Create();

            // natychmiastowy komunikat, żeby UI nie stał „na 0”
            _progressStore.Update(ap.Id, p =>
            {
                p.Messages.Add("▶ Start audytu…");
                p.Percent = 0;
                p.CurrentStep = 1;
            });

            // uruchom w tle z osobnym DI scope
            _ = Task.Run(() => RunAuditAsync(ap.Id, userId));
            return ap.Id;
        }

        private async Task StepAsync(Guid id, int stepIndex, Func<Task> action, string okMessage, int totalSteps = 6)
        {
            _progressStore.Update(id, ap =>
            {
                ap.CurrentStep = stepIndex;
                ap.Percent = (int)Math.Round(((stepIndex - 1) / (double)totalSteps) * 100.0);
                ap.Messages.Add($"▶ {ap.Steps[stepIndex - 1]}…");
            });

            await action();

            _progressStore.Update(id, ap =>
            {
                ap.Messages.Add($"✔ {okMessage}");
            });

            // pauza tylko dla czytelności UI
            await Task.Delay(250);
        }

        private async Task RunAuditAsync(Guid id, string? userId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                // świeże scoped serwisy
                var context = scope.ServiceProvider.GetRequiredService<HotelManagementContext>();
                var businessDate = scope.ServiceProvider.GetRequiredService<IBusinessDateProvider>();
                var dailyReport = scope.ServiceProvider.GetRequiredService<IDailyReportGenerator>();

                _progressStore.Update(id, p => p.Messages.Add("ℹ Uzyskano zależności DI, rozpoczynam kroki…"));

                var today = await businessDate.GetCurrentBusinessDateAsync();

                // 1) Zamknij otwarte rachunki
                await StepAsync(id, 1, async () =>
                {
                    var toClose = await context.Reservations
                        .Where(r => r.Status == ReservationStatus.CheckedOut && !r.IsClosed)
                        .ToListAsync();

                    foreach (var r in toClose)
                        r.IsClosed = true;

                    await context.SaveChangesAsync();
                }, "Otwarte rachunki zamknięte.");

                // 2) No-show
                await StepAsync(id, 2, async () =>
                {
                    var yesterday = today.AddDays(-1);
                    var noShows = await context.Reservations
                        .Where(r => r.Status == ReservationStatus.Confirmed && r.CheckIn.Date <= yesterday)
                        .ToListAsync();

                    foreach (var r in noShows)
                        r.Status = ReservationStatus.NoShow;

                    await context.SaveChangesAsync();
                }, "No-show oznaczone.");

                // 3) Zamknięcie doby (placeholder)
                await StepAsync(id, 3, async () => { await Task.CompletedTask; }, "Doba rozliczona.");

                // 4) Aktualizacja dostępności (placeholder)
                await StepAsync(id, 4, async () => { await Task.CompletedTask; }, "Dostępność pokoi zaktualizowana.");

                // 5) Przesunięcie daty operacyjnej
                await StepAsync(id, 5, async () =>
                {
                    await businessDate.RollToNextDayAsync(userId);
                }, "Data operacyjna przesunięta.");

                // 6) Raport dobowy
                await StepAsync(id, 6, async () =>
                {
                    var newBusinessDate = await businessDate.GetCurrentBusinessDateAsync();
                    var url = await dailyReport.GenerateAsync(newBusinessDate);
                    _progressStore.Update(id, ap => ap.ReportPath = url);
                }, "Raport dobowy wygenerowany.");

                _progressStore.Update(id, ap =>
                {
                    ap.Percent = 100;
                    ap.IsCompleted = true;
                    ap.IsSuccess = true;
                    ap.FinishedAt = DateTime.UtcNow;
                    ap.Messages.Add("✅ Audyt zakończony pomyślnie.");
                });
            }
            catch (Exception ex)
            {
                _progressStore.Update(id, ap =>
                {
                    ap.Messages.Add($"❌ Błąd audytu: {ex.Message}");
                    ap.IsCompleted = true;
                    ap.IsSuccess = false;
                    ap.FinishedAt = DateTime.UtcNow;
                });
            }
        }
    }
}
