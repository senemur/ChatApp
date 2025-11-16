using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.VisualBasic;

namespace ChatApp.Domain.Entities
{
    public class User : IdentityUser
    {
        public string DisplayName { get; set; }
        public string? ProfilePicture { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSeenAt { get; set; }

        // Navigation Properties
        public ICollection<Message> SentMessages { get; set; }
        public ICollection<ConversationParticipant> ConversationParticipants { get; set; }
        public ICollection<Conversation> CreatedConversations { get; set; }
    }
}