using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApp.Domain.Entities
{
    public class Conversation
    {
        public int Id { get; set; }
        public string? Name { get; set; } // Grup sohbetleri için
        public bool IsGroup { get; set; }
        public DateTime CreatedAt { get; set; }

        // Foreign Key
        public string CreatedById { get; set; }

        // Navigation Properties
        public User CreatedBy { get; set; }
        public ICollection<ConversationParticipant> Participants { get; set; }
        public ICollection<Message> Messages { get; set; }
    }
}