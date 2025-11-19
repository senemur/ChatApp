using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ChatApp.Application.Services
{
    public interface IFileUploadService
    {
        Task<string> UploadProfilePictureAsync(IFormFile file, string userId);
        Task<string> UploadMessageImageAsync(IFormFile file);
        bool DeleteFile(string filePath);
    }

    public class FileUploadService : IFileUploadService
    {
        private readonly string _profilesPath;
        private readonly string _messagesPath;
        private readonly long _maxFileSize = 5 * 1024 * 1024; // 5MB
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        public FileUploadService(IWebHostEnvironment env)
        {
            _profilesPath = Path.Combine(env.WebRootPath, "uploads", "profiles");
            _messagesPath = Path.Combine(env.WebRootPath, "uploads", "messages");

            // Klasörleri oluştur (yoksa)
            Directory.CreateDirectory(_profilesPath);
            Directory.CreateDirectory(_messagesPath);
        }

        public async Task<string> UploadProfilePictureAsync(IFormFile file, string userId)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Dosya boş olamaz");

            if (file.Length > _maxFileSize)
                throw new ArgumentException("Dosya boyutu 5MB'dan büyük olamaz");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
                throw new ArgumentException("Sadece resim dosyaları yüklenebilir");

            // Eski profil fotoğrafını sil
            var oldFiles = Directory.GetFiles(_profilesPath, $"{userId}_*");
            foreach (var oldFile in oldFiles)
            {
                File.Delete(oldFile);
            }

            // Yeni dosya adı: userId_timestamp.extension
            var fileName = $"{userId}_{DateTime.UtcNow.Ticks}{extension}";
            var filePath = Path.Combine(_profilesPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/profiles/{fileName}";
        }

        public async Task<string> UploadMessageImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Dosya boş olamaz");

            if (file.Length > _maxFileSize)
                throw new ArgumentException("Dosya boyutu 5MB'dan büyük olamaz");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
                throw new ArgumentException("Sadece resim dosyaları yüklenebilir");

            // Dosya adı: guid_timestamp.extension
            var fileName = $"{Guid.NewGuid()}_{DateTime.UtcNow.Ticks}{extension}";
            var filePath = Path.Combine(_messagesPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/messages/{fileName}";
        }

        public bool DeleteFile(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return false;

                var fileName = Path.GetFileName(fileUrl);
                var profilePath = Path.Combine(_profilesPath, fileName);
                var messagePath = Path.Combine(_messagesPath, fileName);

                if (File.Exists(profilePath))
                {
                    File.Delete(profilePath);
                    return true;
                }
                else if (File.Exists(messagePath))
                {
                    File.Delete(messagePath);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}