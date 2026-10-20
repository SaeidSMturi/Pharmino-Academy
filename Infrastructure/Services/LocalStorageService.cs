using Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class LocalStorageService : IStorageService
    {
        private readonly IWebHostEnvironment _env;

        public LocalStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string directory, string fileName)
        {
            // 🔒 نکته امنیتی: بهتر است در حالت لوکال پوشه آپلود خارج از wwwroot باشد، 
            // اما برای سادگی توسعه فعلا همان مسیر قبلی را حفظ می‌کنیم.
            var uploadsFolder = Path.Combine(_env.WebRootPath, directory);
            Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, fileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
            return fileName;
        }

        public Task DeleteFileAsync(string directory, string fileName)
        {
            var path = Path.Combine(_env.WebRootPath, directory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return Task.CompletedTask;
        }

        public Task<bool> FileExistsAsync(string directory, string fileName)
        {
            var path = Path.Combine(_env.WebRootPath, directory, fileName);
            return Task.FromResult(File.Exists(path));
        }

        // 🔒 ارجاع به کنترلر داخلی برای استریم ویدیو در حالت لوکال
        public Task<string> GetSecureFileUrlAsync(string directory, string fileName, TimeSpan expiresIn)
        {
            // ما اینجا فرض می‌کنیم شما یک کنترلر به نام VideoStreamController با متد GetVideo دارید
            // در S3 این یک لینک واقعی دیجیتال اوشن است، در لوکال یک مسیر داخلی است.
            var url = $"/VideoStream/GetVideo?directory={directory}&fileName={fileName}";
            return Task.FromResult(url);
        }
        public Task<long> GetFileSizeAsync(string directory, string fileName)
        {
            var path = Path.Combine(_env.WebRootPath, directory, fileName);
            if (File.Exists(path))
            {
                return Task.FromResult(new FileInfo(path).Length);
            }
            return Task.FromResult(0L);
        }
        public Task<string> GetFileUrlForProcessingAsync(string directory, string fileName)
        {
            // در سیستم لوکال، مسیر فیزیکی بهترین گزینه برای FFmpeg است
            var path = Path.Combine(_env.WebRootPath, directory, fileName);
            return Task.FromResult(path);
        }

        public Task DeleteDirectoryAsync(string directory)
        {
            var path = Path.Combine(_env.WebRootPath, directory);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            return Task.CompletedTask;
        }
        public async Task UploadPhysicalFileAsync(string physicalPath, string directory, string fileName)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, directory);
            Directory.CreateDirectory(uploadsFolder);
            var destPath = Path.Combine(uploadsFolder, fileName);
            // کپی کردن فایل از پوشه موقت به پوشه نهایی
            File.Copy(physicalPath, destPath, overwrite: true);
            await Task.CompletedTask;
        }

        public async Task<byte[]> ReadFileBytesAsync(string directory, string fileName)
        {
            var path = Path.Combine(_env.WebRootPath, directory, fileName);
            return await File.ReadAllBytesAsync(path);
        }

        public async Task<string> ReadFileTextAsync(string directory, string fileName)
        {
            var path = Path.Combine(_env.WebRootPath, directory, fileName);
            return await File.ReadAllTextAsync(path);
        }

        // در فایل LocalStorageService.cs

        public string GetCdnUrl(string directory, string fileName)
        {
            // در حالت لوکال، آدرس همان مسیر فایل در وب‌سرور است
            // می‌توانید بسته به تنظیمات سایت، آدرس کامل دامنه را هم اضافه کنید
            return $"/{directory}/{fileName}";
        }

        public async Task UploadMultiplePhysicalFilesAsync(IEnumerable<string> physicalPaths, string directory)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, directory);
            Directory.CreateDirectory(uploadsFolder);

            // استفاده از Task.WhenAll برای کپی موازی فایل‌ها در لوکال
            var copyTasks = physicalPaths.Select(async physicalPath =>
            {
                var fileName = Path.GetFileName(physicalPath);
                var destPath = Path.Combine(uploadsFolder, fileName);

                // کپی کردن فایل به صورت Async
                using (Stream sourceStream = File.Open(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (Stream destStream = File.Create(destPath))
                {
                    await sourceStream.CopyToAsync(destStream);
                }
            });

            await Task.WhenAll(copyTasks);
        }

        public Task<(Stream Stream, long ContentLength)> GetFileStreamAsync(string directory, string fileName, long? start = null, long? end = null)
        {
            var path = Path.Combine(_env.WebRootPath, directory, fileName);
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
            // در لوکال نیازی به برش دستی نیست، خود ASP.NET آن را مدیریت می‌کند
            return Task.FromResult<(Stream, long)>((stream, stream.Length));
        }
    }
}
