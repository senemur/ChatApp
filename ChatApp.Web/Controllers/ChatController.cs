using ChatApp.Application.Services;
using ChatApp.Domain.Entities;
using ChatApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatApp.Web.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ChatDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IFileUploadService _fileUploadService;

        public ChatController(ChatDbContext context, UserManager<User> userManager, IFileUploadService fileUploadService)
        {
            _context = context;
            _userManager = userManager;
            _fileUploadService = fileUploadService;
        }
        // GET: /Chat
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Kullanıcının katıldığı tüm konuşmaları getir
            var conversations = await _context.ConversationParticipants
                .Where(cp => cp.UserId == userId)
                .Include(cp => cp.Conversation)
                    .ThenInclude(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(cp => cp.Conversation)
                    .ThenInclude(c => c.Messages.Where(m => !m.IsDeleted)
                        .OrderByDescending(m => m.SentAt)
                        .Take(1))
                    .ThenInclude(m => m.Sender)
                .Select(cp => cp.Conversation)
                .OrderByDescending(c => c.Messages.Where(m => !m.IsDeleted).Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt)
                .ToListAsync();

            // Her konuşma için okunmamış mesaj sayısını hesapla
            ViewBag.UnreadCounts = new Dictionary<int, int>();
            foreach (var conversation in conversations)
            {
                var unreadCount = await _context.Messages
                    .CountAsync(m => m.ConversationId == conversation.Id
                                    && m.SenderId != userId
                                    && !m.IsRead
                                    && !m.IsDeleted);

                ViewBag.UnreadCounts[conversation.Id] = unreadCount;
            }

            return View(conversations);
        }

        // GET: /Chat/Conversation/5
        public async Task<IActionResult> Conversation(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Kullanıcının bu konuşmaya erişimi var mı kontrol et
            var hasAccess = await _context.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == id && cp.UserId == userId);

            if (!hasAccess)
            {
                return Forbid();
            }

            var conversation = await _context.Conversations
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(c => c.Messages
                    .OrderBy(m => m.SentAt))
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (conversation == null)
            {
                return NotFound();
            }

            return View(conversation);
        }

        // GET: /Chat/NewConversation
        public async Task<IActionResult> NewConversation()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Tüm kullanıcıları getir (kendisi hariç)
            var users = await _context.Users
                .Where(u => u.Id != userId)
                .Select(u => new { u.Id, u.DisplayName, u.UserName })
                .ToListAsync();

            return View(users);
        }

        // POST: /Chat/CreateConversation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateConversation(string participantId, bool isGroup = false, string groupName = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Bire-bir sohbet için zaten var mı kontrol et
            if (!isGroup)
            {
                var existingConversation = await _context.ConversationParticipants
                    .Where(cp => cp.UserId == userId)
                    .Select(cp => cp.Conversation)
                    .Where(c => !c.IsGroup && c.Participants.Any(p => p.UserId == participantId))
                    .FirstOrDefaultAsync();

                if (existingConversation != null)
                {
                    return RedirectToAction("Conversation", new { id = existingConversation.Id });
                }
            }

            // Yeni konuşma oluştur
            var conversation = new Conversation
            {
                Name = isGroup ? groupName : null,
                IsGroup = isGroup,
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            // Katılımcıları ekle
            var participants = new List<ConversationParticipant>
            {
                new ConversationParticipant
                {
                    ConversationId = conversation.Id,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow
                },
                new ConversationParticipant
                {
                    ConversationId = conversation.Id,
                    UserId = participantId,
                    JoinedAt = DateTime.UtcNow
                }
            };

            _context.ConversationParticipants.AddRange(participants);
            await _context.SaveChangesAsync();

            return RedirectToAction("Conversation", new { id = conversation.Id });
        }

        // POST: /Chat/DeleteMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var message = await _context.Messages.FindAsync(messageId);

            if (message == null)
                return NotFound();

            // Sadece kendi mesajını silebilir
            if (message.SenderId != userId)
                return Forbid();

            _context.Messages.Remove(message);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // POST: /Chat/EditMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMessage(int messageId, string content)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var message = await _context.Messages.FindAsync(messageId);

            if (message == null)
                return NotFound();

            // Sadece kendi mesajını düzenleyebilir
            if (message.SenderId != userId)
                return Forbid();

            if (string.IsNullOrWhiteSpace(content))
                return BadRequest("Mesaj boş olamaz");

            message.Content = content.Trim();
            message.IsEdited = true; // Yeni property ekleyeceğiz
            message.EditedAt = DateTime.UtcNow; // Yeni property

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = message.Id,
                content = message.Content,
                isEdited = message.IsEdited,
                editedAt = message.EditedAt?.ToLocalTime().ToString("HH:mm")
            });
        }

        // POST: /Chat/UploadImage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(IFormFile file, int conversationId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Konuşmaya erişim kontrolü
                var hasAccess = await _context.ConversationParticipants
                    .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);

                if (!hasAccess)
                    return Forbid();

                // Resmi yükle
                var imageUrl = await _fileUploadService.UploadMessageImageAsync(file);

                // Mesajı kaydet
                var message = new Message
                {
                    ConversationId = conversationId,
                    SenderId = userId,
                    Content = imageUrl, // Resim URL'ini content'e kaydet
                    SentAt = DateTime.UtcNow,
                    IsRead = false,
                    Type = MessageType.Image, // Resim tipi
                    ReadAt = null
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    messageId = message.Id,
                    imageUrl = imageUrl,
                    sentAt = message.SentAt.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}