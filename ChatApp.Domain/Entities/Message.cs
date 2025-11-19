using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApp.Domain.Entities
{
    public class Message
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public MessageType Type { get; set; }

        public bool IsEdited { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? ReadAt { get; set; }


        // Foreign Keys
        public int ConversationId { get; set; }
        public string SenderId { get; set; }

        // Navigation Properties
        public Conversation Conversation { get; set; }
        public User Sender { get; set; }
    }
}
