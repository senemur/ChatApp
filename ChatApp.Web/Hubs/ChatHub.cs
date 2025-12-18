using ChatApp.Domain.Entities;
using ChatApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Collections.Concurrent;

namespace ChatApp.Web.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ChatDbContext _context;
        private static readonly ConcurrentDictionary<string, string> _userConnections = new();

        public ChatHub(ChatDbContext context)
        {
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId != null)
            {
                _userConnections.AddOrUpdate(userId, Context.ConnectionId, (_, _) => Context.ConnectionId);

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
                _userConnections.TryRemove(userId, out _);

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
        
        // ... (Other methods remain largely the same, just ensuring TryGetValue works with ConcurrentDictionary which mimics Dictionary here)

           public async Task SendVoiceMessage(int conversationId, int messageId)
        {
            try 
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var message = await _context.Messages
                    .Include(m => m.Sender)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null || message.SenderId != userId)
                    return;

                var participants = await _context.ConversationParticipants
                    .Where(cp => cp.ConversationId == conversationId)
                    .Select(cp => cp.UserId)
                    .ToListAsync();

                var messageDto = new
                {
                    id = message.Id,
                    conversationId = message.ConversationId,
                    senderId = message.SenderId,
                    senderName = message.Sender?.DisplayName ?? "Bilinmeyen",
                    content = message.Content, // Audio URL
                    sentAt = message.SentAt.ToString("HH:mm"),
                    isRead = false,
                    type = "audio"
                };

                foreach (var participantId in participants)
                {
                    if (_userConnections.TryGetValue(participantId, out var connectionId))
                    {
                        await Clients.Client(connectionId).SendAsync("ReceiveMessage", messageDto);

                        await Clients.Client(connectionId).SendAsync("UpdateConversationList", new
                        {
                            conversationId = conversationId,
                            lastMessage = "🎤 Sesli Mesaj",
                            lastMessageTime = message.SentAt.ToString("HH:mm"),
                            senderId = message.SenderId,
                            senderName = message.Sender?.DisplayName ?? "Bilinmeyen",
                            isUnread = participantId != userId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Hata oluşursa logla ama client'a patlama
                Console.WriteLine($"SendVoiceMessage Error: {ex.Message}");
                throw;
            }
        }

        public async Task SendMessage(int conversationId, string content)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(content) || userId == null)
                return;

            var hasAccess = await _context.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);

            if (!hasAccess)
                return;

            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = userId,
                Content = content.Trim(),
                SentAt = DateTime.UtcNow,
                IsRead = false,
                Type = MessageType.Text,
                ReadAt = null
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            await _context.Entry(message).Reference(m => m.Sender).LoadAsync();

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
                isRead = false,
                readAt = (DateTime?)null
            };

            foreach (var participantId in participants)
            {
                if (_userConnections.TryGetValue(participantId, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", messageDto);

                    // ✨ Sohbet listesi güncellemesi (tüm katılımcılara)
                    await Clients.Client(connectionId).SendAsync("UpdateConversationList", new
                    {
                        conversationId = conversationId,
                        lastMessage = message.Content,
                        lastMessageTime = message.SentAt.ToString("HH:mm"),
                        senderId = message.SenderId,
                        senderName = message.Sender.DisplayName,
                        isUnread = participantId != userId // Gönderen için unread değil
                    });
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

        public async Task DeleteMessage(int messageId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var message = await _context.Messages.FindAsync(messageId);

            if (message == null || message.SenderId != userId)
                return;

            message.IsDeleted = true;
            await _context.SaveChangesAsync();

            // Konuşmadaki herkese bildir
            var participants = await _context.ConversationParticipants
                .Where(cp => cp.ConversationId == message.ConversationId)
                .Select(cp => cp.UserId)
                .ToListAsync();

            foreach (var participantId in participants)
            {
                if (_userConnections.TryGetValue(participantId, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("MessageDeleted", messageId);
                }
            }
        }

        public async Task EditMessage(int messageId, string newContent)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(newContent))
                return;

            var message = await _context.Messages.FindAsync(messageId);

            if (message == null || message.SenderId != userId)
                return;

            message.Content = newContent.Trim();
            message.IsEdited = true;
            message.EditedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Konuşmadaki herkese bildir
            var participants = await _context.ConversationParticipants
                .Where(cp => cp.ConversationId == message.ConversationId)
                .Select(cp => cp.UserId)
                .ToListAsync();

            var messageDto = new
            {
                id = message.Id,
                content = message.Content,
                isEdited = true,
                editedAt = message.EditedAt?.ToString("HH:mm"),
                 isRead = message.IsRead
            };

            foreach (var participantId in participants)
            {
                if (_userConnections.TryGetValue(participantId, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("MessageEdited", messageDto);
                }
            }
        }

        // Mesaj okundu olarak işaretle
        public async Task MarkMessageAsRead(int messageId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var message = await _context.Messages.FindAsync(messageId);

            if (message == null || message.SenderId == userId)
                return; // Kendi mesajını okuma olarak işaretleyemez

            if (!message.IsRead)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Gönderene bildir (mavi tik için)
                if (_userConnections.TryGetValue(message.SenderId, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("MessageRead", messageId);
                }
            }
        }
        // Konuşmadaki tüm mesajları okundu olarak işaretle
        public async Task MarkConversationAsRead(int conversationId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var unreadMessages = await _context.Messages
                .Where(m => m.ConversationId == conversationId
                            && m.SenderId != userId
                            && !m.IsRead
                            && !m.IsDeleted)
                .ToListAsync();

            if (unreadMessages.Any())
            {
                var now = DateTime.UtcNow;

                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                    message.ReadAt = now;
                }

                await _context.SaveChangesAsync();

                // Her mesajın gönderenine bildir
                var senderIds = unreadMessages.Select(m => m.SenderId).Distinct();

                foreach (var senderId in senderIds)
                {
                    if (_userConnections.TryGetValue(senderId, out var connectionId))
                    {
                        var messageIds = unreadMessages
                            .Where(m => m.SenderId == senderId)
                            .Select(m => m.Id)
                            .ToList();

                        await Clients.Client(connectionId).SendAsync("MessagesRead", messageIds);
                    }
                }

                // ✨ YENİ - Sohbet listesini güncelle
                if (_userConnections.TryGetValue(userId, out var userConnectionId))
                {
                    await Clients.Client(userConnectionId).SendAsync("ConversationRead", conversationId);
                }
            }
        }

        public async Task SendImageMessage(int conversationId, int messageId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var message = await _context.Messages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null || message.SenderId != userId)
                return;

            // Konuşmadaki diğer kullanıcılara resmi gönder
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
                content = message.Content, // Image URL
                sentAt = message.SentAt.ToString("HH:mm"),
                isRead = false,
                type = "image"
            };

            foreach (var participantId in participants)
            {
                if (_userConnections.TryGetValue(participantId, out var connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", messageDto);

                    // Sohbet listesi güncellemesi
                    await Clients.Client(connectionId).SendAsync("UpdateConversationList", new
                    {
                        conversationId = conversationId,
                        lastMessage = "📷 Fotoğraf",
                        lastMessageTime = message.SentAt.ToString("HH:mm"),
                        senderId = message.SenderId,
                        senderName = message.Sender.DisplayName,
                        isUnread = participantId != userId
                    });
                }
            }
        }

    }
}