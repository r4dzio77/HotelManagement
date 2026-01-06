using System;
using System.Linq;
using System.Threading.Tasks;
using HotelManagement.Data;
using HotelManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly HotelManagementContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatController(
            HotelManagementContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

      
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // ostatnia rozmowa użytkownika
            var conversation = await _context.ChatConversations
                .Where(c => c.UserId == user.Id)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            // ❗ JEŚLI NIE MA – TWORZYMY AUTOMATYCZNIE
            if (conversation == null)
            {
                conversation = new ChatConversation
                {
                    UserId = user.Id
                };

                _context.ChatConversations.Add(conversation);
                await _context.SaveChangesAsync();

                // opcjonalna wiadomość powitalna (od razu "stara")
                _context.ChatMessages.Add(new ChatMessage
                {
                    ChatConversationId = conversation.Id,
                    SenderUserId = user.Id,
                    IsFromStaff = true,
                    Content = "Dziękujemy za kontakt z recepcją. Jak możemy pomóc?",
                    SentAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Conversation), new { id = conversation.Id });
        }


      

       
        [Authorize(Roles = "Admin,Pracownik,Kierownik")]
        public async Task<IActionResult> Staff(int? conversationId)
        {
            var chats = await _context.ChatConversations
                .Include(c => c.User)
                .Include(c => c.Messages)
                .ToListAsync();

            // 🟢 NOWE – ostatnia wiadomość od GOŚCIA
            var newChats = chats
                .Where(c =>
                {
                    var last = c.Messages
                        .OrderByDescending(m => m.SentAt)
                        .FirstOrDefault();

                    return last != null && !last.IsFromStaff;
                })
                .OrderByDescending(c => c.Messages.Max(m => m.SentAt))
                .ToList();

            // ⚪ STARE – reszta
            var oldChats = chats
                .Except(newChats)
                .OrderByDescending(c => c.Messages.Max(m => m.SentAt))
                .ToList();

            ViewBag.NewChatsCount = newChats.Count;

            ChatConversation? activeConversation = null;

            if (conversationId.HasValue)
            {
                activeConversation = await _context.ChatConversations
                    .Include(c => c.User)
                    .Include(c => c.Reservation)
                        .ThenInclude(r => r.Guest)
                    .Include(c => c.Messages)
                        .ThenInclude(m => m.SenderUser)
                    .FirstOrDefaultAsync(c => c.Id == conversationId.Value);
            }

            ViewBag.NewChats = newChats;
            ViewBag.OldChats = oldChats;
            ViewBag.ActiveConversation = activeConversation;

            return View();
        }

        // =========================
        // WIDOK ROZMOWY (GOŚĆ)
        // =========================
        public async Task<IActionResult> Conversation(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            var conversation = await _context.ChatConversations
                .Include(c => c.User)
                .Include(c => c.Reservation)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.SenderUser)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conversation == null)
                return NotFound();

            if (conversation.UserId != user.Id)
                return Forbid();

            return View(conversation);
        }

        // =========================
        // WYSYŁANIE WIADOMOŚCI
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(int conversationId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return RedirectToAction(nameof(Index));

            var user = await _userManager.GetUserAsync(User);
            var isStaff =
                User.IsInRole("Admin") ||
                User.IsInRole("Pracownik") ||
                User.IsInRole("Kierownik");

            var message = new ChatMessage
            {
                ChatConversationId = conversationId,
                SenderUserId = user.Id,
                IsFromStaff = isStaff,
                Content = content,
                SentAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            if (isStaff)
                return RedirectToAction(nameof(Staff), new { conversationId });

            return RedirectToAction(nameof(Conversation), new { id = conversationId });
        }

        // =========================
        // NOWA ROZMOWA (GOŚĆ)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string firstMessage, int? reservationId)
        {
            if (string.IsNullOrWhiteSpace(firstMessage))
                return RedirectToAction(nameof(Index));

            var user = await _userManager.GetUserAsync(User);

            var conversation = new ChatConversation
            {
                UserId = user.Id,
                ReservationId = reservationId
            };

            _context.ChatConversations.Add(conversation);
            await _context.SaveChangesAsync();

            var guestMessage = new ChatMessage
            {
                ChatConversationId = conversation.Id,
                SenderUserId = user.Id,
                IsFromStaff = false,
                Content = firstMessage,
                SentAt = DateTime.UtcNow
            };

            var welcomeMessage = new ChatMessage
            {
                ChatConversationId = conversation.Id,
                SenderUserId = user.Id,
                IsFromStaff = true,
                Content = "Dziękujemy za kontakt z recepcją. Odpowiemy najszybciej jak to możliwe.",
                SentAt = DateTime.UtcNow.AddSeconds(1)
            };

            _context.ChatMessages.AddRange(guestMessage, welcomeMessage);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Conversation), new { id = conversation.Id });
        }
    }
}
