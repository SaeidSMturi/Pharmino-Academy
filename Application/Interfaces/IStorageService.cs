using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string directory, string fileName);
        Task DeleteFileAsync(string directory, string fileName);
        Task<bool> FileExistsAsync(string directory, string fileName);
        Task<string> GetSecureFileUrlAsync(string directory, string fileName, TimeSpan expiresIn);
        Task<long> GetFileSizeAsync(string directory, string fileName);
        // دریافت مسیری که FFmpeg بتواند آن را بخواند (مسیر فیزیکی در لوکال، یا لینک موقت طولانی در باکت)
        Task<string> GetFileUrlForProcessingAsync(string directory, string fileName);

        // پاکسازی یک دایرکتوری (یا Prefix) به صورت کامل برای حذف HLS های قدیمی
        Task DeleteDirectoryAsync(string directory);
        // داخل IStorageService:
        Task UploadPhysicalFileAsync(string physicalPath, string directory, string fileName);
        Task<byte[]> ReadFileBytesAsync(string directory, string fileName);
        Task<string> ReadFileTextAsync(string directory, string fileName);
        string GetCdnUrl(string directory, string fileName);
        Task UploadMultiplePhysicalFilesAsync(IEnumerable<string> physicalPaths, string directory);
        // داخل IStorageService
        Task<(Stream Stream, long ContentLength)> GetFileStreamAsync(string directory, string fileName, long? start = null, long? end = null);
    }
}
