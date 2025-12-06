using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using HotelManagement.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HotelManagement.Services
{
    public class GoogleCalendarHelper
    {
        private readonly IConfiguration _config;
        private readonly ILogger<GoogleCalendarHelper> _logger;

        public GoogleCalendarHelper(
            IConfiguration config,
            ILogger<GoogleCalendarHelper> logger)
        {
            _config = config;
            _logger = logger;
        }

        // ================== REFRESH TOKEN ==================

        public async Task<bool> RefreshAccessTokenAsync(ApplicationUser user)
        {
            try
            {
                if (string.IsNullOrEmpty(user.GoogleRefreshToken))
                {
                    _logger.LogWarning("Brak refresh tokena Google dla użytkownika {UserId}", user.Id);
                    return false;
                }

                var clientId = _config["Authentication:Google:ClientId"];
                var clientSecret = _config["Authentication:Google:ClientSecret"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("Brak konfiguracji Google ClientId/ClientSecret.");
                    return false;
                }

                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = clientId,
                        ClientSecret = clientSecret
                    }
                });

                var token = new TokenResponse
                {
                    RefreshToken = user.GoogleRefreshToken
                };

                var newToken = await flow.RefreshTokenAsync(user.Id, token.RefreshToken, System.Threading.CancellationToken.None);

                if (newToken == null || string.IsNullOrEmpty(newToken.AccessToken))
                {
                    _logger.LogWarning("Nie udało się odświeżyć tokena Google dla użytkownika {UserId}", user.Id);
                    return false;
                }

                user.GoogleAccessToken = newToken.AccessToken;
                user.GoogleTokenExpiry = newToken.ExpiresInSeconds.HasValue
                    ? DateTime.UtcNow.AddSeconds(newToken.ExpiresInSeconds.Value)
                    : (DateTime?)null;

                _logger.LogInformation("Odświeżono token Google dla użytkownika {UserId}", user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas odświeżania tokena Google dla użytkownika {UserId}", user.Id);
                return false;
            }
        }

        // ================== SYNC ZMIAN ==================

        public async Task<bool> SyncShiftsAsync(ApplicationUser user, IEnumerable<WorkShift> shifts, string accessToken)
        {
            try
            {
                var service = CreateCalendarService(accessToken);
                var calendarId = "primary";

                // sortujemy po dacie i godzinie
                var shiftList = shifts
                    .OrderBy(s => s.Date)
                    .ThenBy(s => s.StartTime)
                    .ToList();

                foreach (var shift in shiftList)
                {
                    // Lokalna data/godzina – zakładam, że Date jest w czasie lokalnym hotelu
                    var startLocal = shift.Date.Date + shift.StartTime;
                    var endLocal = shift.Date.Date + shift.EndTime;

                    var summary = $"{user.FullName} ({startLocal:HH\\:mm}-{endLocal:HH\\:mm})";
                    var description = $"Zmiana w hotelu dla {user.FullName}";

                    var ev = new Event
                    {
                        Summary = summary,
                        Description = description,
                        Start = new EventDateTime
                        {
                            DateTimeDateTimeOffset = new DateTimeOffset(startLocal, TimeSpan.Zero)
                        },
                        End = new EventDateTime
                        {
                            DateTimeDateTimeOffset = new DateTimeOffset(endLocal, TimeSpan.Zero)
                        }
                    };

                    try
                    {
                        Event result;

                        if (!string.IsNullOrEmpty(shift.GoogleEventId))
                        {
                            // aktualizacja istniejącego eventu
                            result = await service.Events.Update(ev, calendarId, shift.GoogleEventId).ExecuteAsync();
                        }
                        else
                        {
                            // tworzenie nowego eventu
                            result = await service.Events.Insert(ev, calendarId).ExecuteAsync();
                            shift.GoogleEventId = result.Id;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd synchronizacji zmiany {ShiftId} do Google Calendar", shift.Id);
                        // lecimy dalej, ale na końcu zwrócimy false
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas synchronizacji zmian do Google Calendar");
                return false;
            }
        }

        // ================== POMOCNICZE ==================

        private CalendarService CreateCalendarService(string accessToken)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);

            var service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "HotelManagement"
            });

            return service;
        }
    }
}
