using System;

namespace HotelManagement.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        public int ChatConversationId { get; set; }
        public ChatConversation Conversation { get; set; } = null!;

        // Nadawca (gość LUB pracownik)
        public string SenderUserId { get; set; } = null!;
        public ApplicationUser SenderUser { get; set; } = null!;

        // true = pracownik hotelu, false = gość
        public bool IsFromStaff { get; set; }

        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;
    }
}
