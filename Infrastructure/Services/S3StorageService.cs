using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Infrastructure.Services
{
    public class S3StorageService : IStorageService
    {
        private readonly string _bucketName;
        private readonly string _cdnUrl; // اضافه کردن متغیر CDN
        private readonly IAmazonS3 _s3Client;
        private readonly ILogger<S3StorageService> _logger;

        public S3StorageService(IConfiguration config, ILogger<S3StorageService> logger)
        {
            _logger = logger;
            try
            {
                var s3Config = config.GetSection("StorageSettings:S3");
                _bucketName = s3Config["BucketName"];
                _cdnUrl = s3Config["CdnURL"]; // خواندن آدرس CDN از appsettings.json

                var s3ClientConfig = new AmazonS3Config
                {
                    ServiceURL = s3Config["ServiceURL"]
                };

                _s3Client = new AmazonS3Client(s3Config["AccessKey"], s3Config["SecretKey"], s3ClientConfig);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to initialize S3StorageService.");
                throw;
            }
        }

        private string GetNormalizedKey(string directory, string fileName)
        {
            // یکپارچه‌سازی مسیرها برای سرورهای لینوکسی و جلوگیری از خطاهای مسیردهی
            return $"{directory}/{fileName}".Replace("\\", "/").TrimStart('/');
        }

        public async Task<string> UploadFileAsync(IFormFile file, string directory, string fileName)
        {
            var key = GetNormalizedKey(directory, fileName);
            try
            {
                _logger.LogInformation("Starting upload for file {FileName} to {Key}", file.FileName, key);

                // راه حل: باز کردن مستقیم استریم از فایل بدون کپی کردن در رم
                using var stream = file.OpenReadStream();

                var fileTransferUtility = new TransferUtility(_s3Client);
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = stream, // پاس دادن مستقیم استریم به ابزار آپلود
                    Key = key,
                    BucketName = _bucketName,
                    CannedACL = S3CannedACL.Private // 🔒 فایل کاملا خصوصی است
                };

                // TransferUtility خودش مدیریت آپلود فایل‌های حجیم (Multipart) را بهینه انجام می‌دهد
                await fileTransferUtility.UploadAsync(uploadRequest);

                _logger.LogInformation("File {FileName} uploaded successfully to {Key}", file.FileName, key);
                return fileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while uploading file {FileName} to {Key}", file.FileName, key);
                throw;
            }
        }

        public async Task DeleteFileAsync(string directory, string fileName)
        {
            var key = GetNormalizedKey(directory, fileName);
            try
            {
                await _s3Client.DeleteObjectAsync(_bucketName, key);
                _logger.LogInformation("File {Key} deleted successfully from bucket {BucketName}", key, _bucketName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file {Key} from bucket {BucketName}", key, _bucketName);
                throw;
            }
        }

        public async Task<bool> FileExistsAsync(string directory, string fileName)
        {
            var key = GetNormalizedKey(directory, fileName);
            try
            {
                await _s3Client.GetObjectMetadataAsync(_bucketName, key);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug("File {Key} does not exist in bucket {BucketName}", key, _bucketName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of file {Key}", key);
                throw;
            }
        }

        public Task<string> GetSecureFileUrlAsync(string directory, string fileName, TimeSpan expiresIn)
        {
            var key = GetNormalizedKey(directory, fileName);
            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    Expires = DateTime.UtcNow.Add(expiresIn),
                    Protocol = Protocol.HTTPS
                };

                string url = _s3Client.GetPreSignedURL(request);
                _logger.LogInformation("Generated Pre-signed URL for {Key}. Expires in {ExpirationHours} hours.", key, expiresIn.TotalHours);

                return Task.FromResult(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate Pre-signed URL for {Key}", key);
                throw;
            }
        }

        public async Task<long> GetFileSizeAsync(string directory, string fileName)
        {
            var key = GetNormalizedKey(directory, fileName);
            try
            {
                var metadata = await _s3Client.GetObjectMetadataAsync(_bucketName, key);
                return metadata.ContentLength;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Attempted to get size of non-existent file {Key}", key);
                return 0L;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving file size for {Key}", key);
                throw;
            }
        }

        public Task<string> GetFileUrlForProcessingAsync(string directory, string fileName)
        {
            _logger.LogInformation("Generating long-lived URL for FFmpeg processing for {FileName}", fileName);
            return GetSecureFileUrlAsync(directory, fileName, TimeSpan.FromHours(12));
        }

        public async Task DeleteDirectoryAsync(string directory)
        {
            var prefix = directory.EndsWith("/") ? directory : directory + "/";
            try
            {
                _logger.LogInformation("Attempting to delete directory (prefix) {Prefix}", prefix);

                var request = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = prefix
                };

                var response = await _s3Client.ListObjectsV2Async(request);
                if (response.S3Objects.Any())
                {
                    var deleteRequest = new DeleteObjectsRequest
                    {
                        BucketName = _bucketName,
                        Objects = response.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
                    };
                    var deleteResponse = await _s3Client.DeleteObjectsAsync(deleteRequest);

                    _logger.LogInformation("Successfully deleted {DeletedCount} items from {Prefix}", deleteResponse.DeletedObjects.Count, prefix);
                }
                else
                {
                    _logger.LogInformation("No files found under prefix {Prefix} to delete.", prefix);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting directory {Prefix}", prefix);
                throw;
            }
        }

        public async Task UploadPhysicalFileAsync(string physicalPath, string directory, string fileName)
        {
            var key = GetNormalizedKey(directory, fileName);
            try
            {
                _logger.LogInformation("Starting physical file upload (HLS/Media) from {PhysicalPath} to {Key}", physicalPath, key);

                var fileTransferUtility = new TransferUtility(_s3Client);
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    FilePath = physicalPath,
                    Key = key,
                    BucketName = _bucketName,
                    CannedACL = S3CannedACL.Private
                };

                await fileTransferUtility.UploadAsync(uploadRequest);
                _logger.LogInformation("Physical file {Key} uploaded successfully.", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload physical file {PhysicalPath} to {Key}", physicalPath, key);
                throw;
            }
        }

        public async Task<byte[]> ReadFileBytesAsync(string directory, string fileName)
        {
            var key = GetNormalizedKey(directory, fileName);
            try
            {
                using var response = await _s3Client.GetObjectAsync(_bucketName, key);
                using var ms = new MemoryStream();
                await response.ResponseStream.CopyToAsync(ms);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read bytes for file {Key}", key);
                throw;
            }
        }

        public async Task<string> ReadFileTextAsync(string directory, string fileName)
        {
            var key = GetNormalizedKey(directory, fileName);
            try
            {
                using var response = await _s3Client.GetObjectAsync(_bucketName, key);
                using var reader = new StreamReader(response.ResponseStream);
                return await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read text for file {Key}", key);
                throw;
            }
        }

        public async Task<(Stream Stream, long ContentLength)> GetFileStreamAsync(string directory, string fileName, long? start = null, long? end = null)
        {
            var key = GetNormalizedKey(directory, fileName);
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };

                if (start.HasValue || end.HasValue)
                {
                    request.ByteRange = new ByteRange(start ?? 0, end ?? long.MaxValue);
                }

                var response = await _s3Client.GetObjectAsync(request);
                return (response.ResponseStream, response.ContentLength);
            }
            // گرفتن خطای 404 به صورت مستقیم به جای استفاده از FileExistsAsync قبل از این متد
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("File not found directly in stream request: {Key}", key);
                throw new FileNotFoundException($"The video file {key} was not found on S3.");
            }
        }

        public string GetCdnUrl(string directory, string fileName)
        {
            var key = GetNormalizedKey(directory, fileName);
            // تولید لینک مستقیم بر بستر CDN
            return $"{_cdnUrl}/{key}";
        }

        public async Task UploadMultiplePhysicalFilesAsync(IEnumerable<string> physicalPaths, string directory)
        {
            _logger.LogInformation("Starting parallel upload of {Count} files to {Directory}", physicalPaths.Count(), directory);

            var fileTransferUtility = new TransferUtility(_s3Client);

            // استفاده از Task.WhenAll برای آپلود همزمان (Concurrent) به جای آپلود ترتیبی
            // این کار زمان آپلود قطعات .ts ویدیوی FFmpeg را به شدت کاهش می دهد
            var uploadTasks = physicalPaths.Select(async path =>
            {
                var fileName = Path.GetFileName(path);
                var key = GetNormalizedKey(directory, fileName);

                var uploadRequest = new TransferUtilityUploadRequest
                {
                    FilePath = path,
                    Key = key,
                    BucketName = _bucketName,
                    CannedACL = S3CannedACL.Private
                };

                await fileTransferUtility.UploadAsync(uploadRequest);
            });

            await Task.WhenAll(uploadTasks);
            _logger.LogInformation("Parallel upload completed successfully.");
        }

        // --- بهینه سازی GetFileStreamAsync ---
       
    }
}