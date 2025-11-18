using ChatApp.Domain.Entities;
using ChatApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatApp.Web.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ChatDbContext _context;
        private static readonly Dictionary<string, string> _userConnections = new();

        public ChatHub(ChatDbContext context)
        {
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId != null)
            {
                _userConnections[userId] = Context.ConnectionId;

                // Kullanıcının LastSeenAt'ini güncelle
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.LastSeenAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // Tüm kullanıcılara online olduğunu bildir
                await Clients.Others.SendAsync("UserOnline", userId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId != null)
            {
                _userConnections.Remove(userId);

                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.LastSeenAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                await Clients.Others.SendAsync("UserOffline", userId, DateTime.UtcNow);
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(int conversationId, string content)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(content) || userId == null)
                return;

            // Kullanıcının bu konuşmaya erişimi var mı kontrol et
            var hasAccess = await _context.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);

            if (!hasAccess)
                return;

            // Mesajı veritabanına kaydet
            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = userId,
                Content = content.Trim(),
                SentAt = DateTime.UtcNow,
                IsRead = false,
                Type = MessageType.Text
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Gönderici bilgisini yükle
            await _context.Entry(message).Reference(m => m.Sender).LoadAsync();

            // Konuşmadaki diğer kullanıcılara mesajı gönder
            var participants = await _context.ConversationParticipants
                .Where(cp => cp.ConversationId == conversationId)
                .Select(cp => cp.UserId)
                .ToListAsync();

            var messageDto = new
            {
                id = message.Id,
                conversationId = message.ConversationId,
                senderId = message.SenderId,
                senderName = message.Sender.DisplayName,
                content = message.Content,
                sentAt = message.SentAt.ToString("HH:mm"),
                isRead = message.IsRead
            };

            // Tüm katılımcılara mesajı gönder (grup için)
            foreach (var participantId in participants)
            {
                if (_userConnections.TryGetValue(participantId, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", messageDto);
                }
            }
        }

        public async Task JoinConversation(int conversationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        }

        public async Task StartTyping(int conversationId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = Context.User?.Identity?.Name;

            await Clients.OthersInGroup($"conversation_{conversationId}")
                .SendAsync("UserTyping", userId, userName);
        }

        public async Task StopTyping(int conversationId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            await Clients.OthersInGroup($"conversation_{conversationId}")
                .SendAsync("UserStoppedTyping", userId);
        }
    }
}