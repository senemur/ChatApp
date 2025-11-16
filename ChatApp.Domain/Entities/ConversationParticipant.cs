using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatApp.Domain.Entities
{
    public class ConversationParticipant
    {
        public int ConversationId { get; set; }
        public string UserId { get; set; }
        public DateTime JoinedAt { get; set; }

        // Navigation Properties
        public Conversation Conversation { get; set; }
        public User User { get; set; }
    }
}