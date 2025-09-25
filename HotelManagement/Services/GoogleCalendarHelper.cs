using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using HotelManagement.Models;

namespace HotelManagement.Services
{
    public class GoogleCalendarHelper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GoogleCalendarHelper(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
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

        public async Task SyncShiftsAsync(ApplicationUser user, IEnumerable<WorkShift> shifts, string accessToken)
        {
            var service = GetCalendarService(accessToken);
            string calendarId = "primary";

            foreach (var shift in shifts)
            {
                await AddOrUpdateShiftAsync(service, calendarId, shift);
            }

            await RemoveDeletedShiftsAsync(service, calendarId, shifts);
        }

        private async Task AddOrUpdateShiftAsync(CalendarService service, string calendarId, WorkShift shift)
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
                Start = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(start) },
                End = new EventDateTime { DateTimeDateTimeOffset = new DateTimeOffset(end) }
            };

            if (!string.IsNullOrEmpty(shift.GoogleEventId))
            {
                try
                {
                    await service.Events.Update(newEvent, calendarId, shift.GoogleEventId).ExecuteAsync();
                    return; // zaktualizowane
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // jeśli w Google Calendar nie ma → dodamy nowe poniżej
                }
            }

            // Wstaw nowe wydarzenie
            var created = await service.Events.Insert(newEvent, calendarId).ExecuteAsync();
            shift.GoogleEventId = created.Id; // zapisz ID z Google
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
    }
}
