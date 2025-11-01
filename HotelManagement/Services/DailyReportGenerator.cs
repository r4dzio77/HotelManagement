using System;
using System.IO;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HotelManagement.Services
{
    /// <summary>
    /// Minimalny generator Raportu Dobowego (placeholder).
    /// Pliki trafiają do: wwwroot/reports/daily/DailyReport_yyyyMMdd.pdf
    /// </summary>
    public class DailyReportGenerator : IDailyReportGenerator
    {
        public Task<string> GenerateAsync(DateTime businessDate)
        {
            var fileName = $"DailyReport_{businessDate:yyyyMMdd}.pdf";
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", "daily");
            Directory.CreateDirectory(folder);
            var fullPath = Path.Combine(folder, fileName);
            var publicUrl = $"/reports/daily/{fileName}";

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().AlignCenter().Text(t =>
                        t.Span($"Raport Dobowy – {businessDate:dd.MM.yyyy}").Bold().FontSize(18)
                    );
                    page.Content().PaddingVertical(10)
                        .Text("Zawartość raportu uzupełnimy według Twoich wytycznych.").FontSize(12);
                    page.Footer().AlignCenter().Text($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}");
                });
            });

            doc.GeneratePdf(fullPath);
            return Task.FromResult(publicUrl);
        }
    }
}
