using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using HotelManagement.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace HotelManagement.Services
{
    public class GoogleCalendarHelper
    {
        private readonly IConfiguration _config;

        public GoogleCalendarHelper(IConfiguration config)
        {
            _config = config;
        }

        private CalendarService GetCalendarService(string accessToken)
        {
            var initializer = new BaseClientService.Initializer()
            {
                HttpClientInitializer = GoogleCredential.FromAccessToken(accessToken),
                ApplicationName = "HotelManagement"
            };

            return new CalendarService(initializer);
        }

        /// <summary>
        /// Synchronizuje wszystkie zmiany do Google Calendar.
        /// </summary>
        public async Task<bool> SyncShiftsAsync(ApplicationUser user, IEnumerable<WorkShift> shifts, string accessToken)
        {
            var service = GetCalendarService(accessToken);
            string calendarId = "primary";

            bool allOk = true;

            foreach (var shift in shifts)
            {
                var success = await AddOrUpdateShiftAsync(service, calendarId, shift);
                if (!success) allOk = false;
            }

            await RemoveDeletedShiftsAsync(service, calendarId, shifts);
            return allOk;
        }

        private async Task<bool> AddOrUpdateShiftAsync(CalendarService service, string calendarId, WorkShift shift)
        {
            var start = shift.ShiftType == "Day"
                ? shift.Date.AddHours(7)
                : shift.Date.AddHours(19);

            var end = shift.ShiftType == "Day"
                ? shift.Date.AddHours(19)
                : shift.Date.AddDays(1).AddHours(7);

            var newEvent = new Event
            {
                Summary = $"Zmiana {shift.ShiftType} - {shift.User.FullName}",
                Description = $"Zmiana pracownika {shift.User.FullName}",
                Start = new EventDateTime
                {
                    DateTime = start,
                    TimeZone = "Europe/Warsaw"
                },
                End = new EventDateTime
                {
                    DateTime = end,
                    TimeZone = "Europe/Warsaw"
                }
            };

            try
            {
                if (!string.IsNullOrEmpty(shift.GoogleEventId))
                {
                    await service.Events.Update(newEvent, calendarId, shift.GoogleEventId).ExecuteAsync();
                }
                else
                {
                    var created = await service.Events.Insert(newEvent, calendarId).ExecuteAsync();
                    shift.GoogleEventId = created.Id; // zapisz ID z Google
                }
                return true;
            }
            catch (Google.GoogleApiException ex)
            {
                // 🔎 logowanie szczegółów błędu
                Console.WriteLine($"[Google API Error] {ex.Error?.Code} - {ex.Error?.Message}");
                if (ex.Error?.Errors != null)
                {
                    foreach (var err in ex.Error.Errors)
                    {
                        Console.WriteLine($"Reason: {err.Reason}, Message: {err.Message}");
                    }
                }
                return false;
            }
        }

        private async Task RemoveDeletedShiftsAsync(CalendarService service, string calendarId, IEnumerable<WorkShift> currentShifts)
        {
            var validIds = currentShifts
                .Where(s => !string.IsNullOrEmpty(s.GoogleEventId))
                .Select(s => s.GoogleEventId)
                .ToHashSet();

            var request = service.Events.List(calendarId);
            request.TimeMin = DateTime.UtcNow.AddMonths(-2);
            request.TimeMax = DateTime.UtcNow.AddMonths(2);
            request.ShowDeleted = false;

            var events = await request.ExecuteAsync();

            if (events.Items != null)
            {
                foreach (var ev in events.Items.Where(e => e.Id != null && e.Summary?.StartsWith("Zmiana ") == true))
                {
                    if (!validIds.Contains(ev.Id))
                    {
                        await service.Events.Delete(calendarId, ev.Id).ExecuteAsync();
                    }
                }
            }
        }

        // ==================== REFRESH TOKEN ====================

        public async Task<bool> RefreshAccessTokenAsync(ApplicationUser user)
        {
            if (string.IsNullOrEmpty(user.GoogleRefreshToken))
                return false;

            var clientId = _config["Authentication:Google:ClientId"];
            var clientSecret = _config["Authentication:Google:ClientSecret"];

            using var client = new HttpClient();

            var body = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", user.GoogleRefreshToken },
                { "grant_type", "refresh_token" }
            };

            var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(body));
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Google Token Refresh Error] {error}");
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);

            var accessToken = json.RootElement.GetProperty("access_token").GetString();
            var expiresIn = json.RootElement.GetProperty("expires_in").GetInt32();

            user.GoogleAccessToken = accessToken;
            user.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

            return true;
        }
    }
}
