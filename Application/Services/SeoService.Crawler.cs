using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Domain.Entities.Seo;

namespace Application.Services
{
    public partial class SeoService
    {
        public async Task<List<SeoPageScan>> GetPageScansAsync()
        {
            var scans = await _context.SeoPageScans.ToListAsync();
            return scans.Where(s => !IsExternalOrIgnoredRoute(s.Url)).ToList();
        }

        private static bool IsExternalOrIgnoredRoute(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;

            var lower = url.ToLower();

            // 1. External identity / OAuth / Google / Auth providers & flows
            if (lower.Contains("accounts.google.com") ||
                lower.Contains("googleusercontent.com") ||
                lower.Contains("/signin/") ||
                lower.Contains("/lifecycle/") ||
                lower.Contains("usernamerecovery") ||
                lower.Contains("code_challenge") ||
                lower.Contains("client_id=") ||
                lower.Contains("app_domain=") ||
                lower.Contains("oauth") ||
                lower.Contains("openid") ||
                lower.Contains("facebook.com") ||
                lower.Contains("instagram.com") ||
                lower.Contains("t.me") ||
                lower.Contains("whatsapp.com") ||
                lower.Contains("telegram.org") ||
                lower.Contains("twitter.com") ||
                lower.Contains("linkedin.com"))
            {
                return true;
            }

            // 2. Language / Culture / System parameters
            if (lower.Contains("/language/setlanguage") ||
                lower.Contains("culture=") ||
                lower.Contains("returnurl="))
            {
                return true;
            }


            return false;
        }
        public async Task<List<SeoImage>> GetSeoImagesAsync()
        {
            return await _context.SeoImages.ToListAsync();
        }

        public async Task UpdateImageAltAsync(int imageId, string altText)
        {
            var seoImage = await _context.SeoImages.FindAsync(imageId);
            if (seoImage == null) return;

            seoImage.AltText = altText;
            _context.SeoImages.Update(seoImage);

            var foundOnPage = seoImage.FoundOnPage;
            var imageUrl = seoImage.ImageUrl;

            if (foundOnPage.Contains("/post/details/", StringComparison.OrdinalIgnoreCase) ||
                foundOnPage.Contains("/post/", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(foundOnPage, @"\d+$");
                if (match.Success && int.TryParse(match.Value, out int postId))
                {
                    var post = await _context.Posts.FindAsync(postId);
                    if (post != null)
                    {
                        post.Body = UpdateImageAltInHtml(post.Body, imageUrl, altText);
                        _context.Posts.Update(post);
                    }
                }
            }
            else if (foundOnPage.Contains("/course/", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(foundOnPage, @"\d+$");
                if (match.Success && int.TryParse(match.Value, out int courseId))
                {
                    var course = await _context.Courses.FindAsync(courseId);
                    if (course != null)
                    {
                        course.Description = UpdateImageAltInHtml(course.Description, imageUrl, altText);
                        _context.Courses.Update(course);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        private string UpdateImageAltInHtml(string html, string imageUrl, string newAlt)
        {
            if (string.IsNullOrEmpty(html) || string.IsNullOrEmpty(imageUrl))
                return html;

            var imgRegex = new Regex(@"<img\b[^>]*>", RegexOptions.IgnoreCase);
            return imgRegex.Replace(html, match =>
            {
                var imgTag = match.Value;
                if (imgTag.Contains(imageUrl, StringComparison.OrdinalIgnoreCase))
                {
                    var altRegex = new Regex(@"alt=[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                    if (altRegex.IsMatch(imgTag))
                    {
                        return altRegex.Replace(imgTag, $"alt=\"{System.Net.WebUtility.HtmlEncode(newAlt)}\"");
                    }
                    else
                    {
                        if (imgTag.EndsWith("/>"))
                        {
                            return imgTag.Insert(imgTag.Length - 2, $" alt=\"{System.Net.WebUtility.HtmlEncode(newAlt)}\" ");
                        }
                        else
                        {
                            return imgTag.Insert(imgTag.Length - 1, $" alt=\"{System.Net.WebUtility.HtmlEncode(newAlt)}\" ");
                        }
                    }
                }
                return imgTag;
            });
        }

        public async Task RunCrawlingAsync(string baseUrl)
        {
            // پاکسازی دیتابیس برای شروع یک اسکن تازه
            _context.SeoPageScans.RemoveRange(_context.SeoPageScans);
            _context.SeoImages.RemoveRange(_context.SeoImages);
            await _context.SaveChangesAsync();

            // استانداردسازی آدرس پایه (حذف اسلش اضافه)
            baseUrl = baseUrl.TrimEnd('/');
            var baseUri = new Uri(baseUrl);

            var queue = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            queue.Enqueue(baseUrl);
            visited.Add(baseUrl);

            int pagesScanned = 0;
            // محدودیت را بالا بردیم تا تمام صفحات سایت‌های بزرگ هم اسکن شوند
            const int maxPages = 1500;

            // Regex بسیار دقیق‌تر: فقط لینک‌های داخل تگ <a> را می‌گیرد تا با فایل‌های CSS اشتباه نشود
            // این Regex هم لینک‌های کوتیشن‌دار (href="/link") و هم بدون کوتیشن (href=/link) را پیدا می‌کند
            var linkRegex = new Regex(@"<a\s+(?:[^>]*?\s+)?href\s*=\s*(?:([""'])(?<link>.*?)\1|(?<link>[^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var titleRegex = new Regex(@"<title\b[^>]*>(?<title>.*?)</title>", RegexOptions.IgnoreCase);
            var descRegex = new Regex(@"<meta\s+[^>]*name=[\x22\x27\s]*description[\x22\x27\s]*content=[\x22\x27\s]*(?<desc>[^\x22\x27>]+)|<meta\s+[^>]*content=[\x22\x27\s]*(?<desc>[^\x22\x27>]+)[\x22\x27\s]*name=[\x22\x27\s]*description", RegexOptions.IgnoreCase);
            var robotsRegex = new Regex(@"<meta\s+[^>]*name=[\x22\x27\s]*robots[\x22\x27\s]*content=[\x22\x27\s]*(?<robots>[^\x22\x27>]+)", RegexOptions.IgnoreCase);
            var h1Regex = new Regex(@"<h1\b[^>]*>(?<h1Content>.*?)</h1>", RegexOptions.IgnoreCase);
            var imgRegex = new Regex(@"<img\b[^>]*>", RegexOptions.IgnoreCase);
            var srcRegex = new Regex(@"src=[""'](?<src>[^""']+)[""']", RegexOptions.IgnoreCase);
            var altRegex = new Regex(@"alt=[""'](?<alt>[^""']*)[""']", RegexOptions.IgnoreCase);

            while (queue.Count > 0 && pagesScanned < maxPages)
            {
                var currentUrl = queue.Dequeue();
                if (IsExternalOrIgnoredRoute(currentUrl))
                {
                    continue;
                }

                pagesScanned++;

                try
                {
                    var response = await _httpClient.GetAsync(currentUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        var normalizedFailUrl = NormalizeUrlPath(currentUrl);
                        if (!IsExternalOrIgnoredRoute(normalizedFailUrl))
                        {
                            var safeFailUrl = normalizedFailUrl.Length > 250 ? normalizedFailUrl.Substring(0, 250) : normalizedFailUrl;

                            var failScan = new SeoPageScan
                            {
                                Url = safeFailUrl,
                                ResponseStatus = $"Error {(int)response.StatusCode} {response.ReasonPhrase}",
                                ScannedAt = DateTime.UtcNow
                            };
                            _context.SeoPageScans.Add(failScan);
                        }
                        continue;
                    }

                    // چک کردن نوع محتوا (فقط صفحات HTML اسکن شوند نه فایل‌های دانلودی)
                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    if (contentType != null && !contentType.Contains("text/html"))
                    {
                        continue;
                    }

                    var html = await response.Content.ReadAsStringAsync();

                    // 1. استخراج مقادیر سئو
                    bool hasNoIndex = false;
                    var robotsMatch = robotsRegex.Match(html);
                    if (robotsMatch.Success && robotsMatch.Groups["robots"].Value.Contains("noindex", StringComparison.OrdinalIgnoreCase))
                    {
                        hasNoIndex = true;
                    }

                    string? title = titleRegex.Match(html).Success ? System.Net.WebUtility.HtmlDecode(titleRegex.Match(html).Groups["title"].Value.Trim()) : null;
                    string? description = descRegex.Match(html).Success ? System.Net.WebUtility.HtmlDecode(descRegex.Match(html).Groups["desc"].Value.Trim()) : null;
                    int h1Count = h1Regex.Matches(html).Count;

                    // 2. پردازش تصاویر
                    int missingAltCount = 0;
                    var imgMatches = imgRegex.Matches(html);
                    foreach (Match imgMatch in imgMatches)
                    {
                        var imgTag = imgMatch.Value;
                        var srcM = srcRegex.Match(imgTag);
                        if (srcM.Success)
                        {
                            var src = srcM.Groups["src"].Value;
                            if (!src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                            {
                                var altM = altRegex.Match(imgTag);
                                bool hasAlt = altM.Success && !string.IsNullOrWhiteSpace(altM.Groups["alt"].Value);

                                if (!hasAlt)
                                {
                                    missingAltCount++;
                                    var imageRelativePath = NormalizeUrlPath(currentUrl);
                                    var existingImage = await _context.SeoImages
                                        .FirstOrDefaultAsync(i => i.ImageUrl == src && i.FoundOnPage == imageRelativePath);

                                    if (existingImage == null)
                                    {
                                        _context.SeoImages.Add(new SeoImage
                                        {
                                            ImageUrl = src,
                                            AltText = altM.Success ? altM.Groups["alt"].Value : null,
                                            FoundOnPage = imageRelativePath,
                                            FoundAt = DateTime.UtcNow
                                        });
                                    }
                                }
                            }
                        }
                    }

                    // 3. ذخیره اطلاعات صفحه
                    var normalizedUrl = NormalizeUrlPath(currentUrl);

                    if (!IsExternalOrIgnoredRoute(normalizedUrl))
                    {
                        var safeUrl = normalizedUrl.Length > 250 ? normalizedUrl.Substring(0, 250) : normalizedUrl;

                        _context.SeoPageScans.Add(new SeoPageScan
                        {
                            Url = safeUrl,
                            Title = title,
                            MetaDescription = description,
                            H1Count = h1Count,
                            MissingAltCount = missingAltCount,
                            HasNoIndex = hasNoIndex,
                            ResponseStatus = "Success (200)",
                            ScannedAt = DateTime.UtcNow
                        });
                    }

                    // 4. استخراج و پردازش تمام لینک‌های داخلی (خزش عمیق)
                    var currentUri = new Uri(currentUrl);
                    var matches = linkRegex.Matches(html);

                    foreach (Match match in matches)
                    {
                        var rawLink = match.Groups["link"].Value;

                        // تبدیل HTML Entities (مثل &amp; به &)
                        rawLink = System.Net.WebUtility.HtmlDecode(rawLink);

                        // جدا کردن Fragment (#)
                        var hashIndex = rawLink.IndexOf('#');
                        if (hashIndex >= 0)
                        {
                            rawLink = rawLink.Substring(0, hashIndex);
                        }

                        if (string.IsNullOrWhiteSpace(rawLink) || IsExternalOrIgnoredRoute(rawLink)) continue;

                        // تلاش برای ساختن یک لینک مطلق از لینک پیدا شده
                        if (Uri.TryCreate(currentUri, rawLink, out Uri? resolvedUri))
                        {
                            // *** شرط حیاتی: فقط و فقط دامنه‌هایی که با سایت اصلی یکی هستند ***
                            if (resolvedUri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase) &&
                                (resolvedUri.Scheme == Uri.UriSchemeHttp || resolvedUri.Scheme == Uri.UriSchemeHttps))
                            {
                                var cleanLink = resolvedUri.GetComponents(UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.Unescaped);

                                // حذف اسلش از انتهای مسیر
                                if (cleanLink.EndsWith("/") && cleanLink.Length > baseUri.GetLeftPart(UriPartial.Authority).Length + 1)
                                {
                                    cleanLink = cleanLink.TrimEnd('/');
                                }

                                if (IsExternalOrIgnoredRoute(cleanLink))
                                {
                                    continue;
                                }

                                // نادیده گرفتن پسوندهای فایلی
                                var ext = Path.GetExtension(resolvedUri.AbsolutePath).ToLower();
                                var ignoredExts = new[] { ".png", ".jpg", ".jpeg", ".gif", ".pdf", ".zip", ".rar", ".css", ".js", ".mp4", ".mp3", ".webm", ".xml", ".json", ".doc", ".docx" };
                                if (ignoredExts.Contains(ext)) continue;

                                // اضافه کردن به صف در صورت جدید بودن
                                if (!visited.Contains(cleanLink))
                                {
                                    visited.Add(cleanLink);
                                    queue.Enqueue(cleanLink);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var normalizedErrorUrl = NormalizeUrlPath(currentUrl);
                    var safeErrorUrl = normalizedErrorUrl.Length > 250 ? normalizedErrorUrl.Substring(0, 250) : normalizedErrorUrl;

                    _context.SeoPageScans.Add(new SeoPageScan
                    {
                        Url = safeErrorUrl,
                        ResponseStatus = $"Failed: {ex.Message}".Substring(0, Math.Min(ex.Message.Length + 8, 250)),
                        ScannedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<int> CalculateHealthScoreAsync()
        {
            int score = 100;

            // Deduct for technical unresolved critical errors (404 and 5xx)
            var unresolvedErrors = await _context.SeoErrorLogs
                .Where(e => !e.IsResolved)
                .ToListAsync();

            int error404Count = unresolvedErrors.Count(e => e.StatusCode == 404);
            int error5xxCount = unresolvedErrors.Count(e => e.StatusCode >= 500);

            // Cap penalties for individual sections
            int penalty404 = Math.Min(error404Count * 2, 20);
            int penalty5xx = Math.Min(error5xxCount * 5, 20);

            score -= penalty404;
            score -= penalty5xx;

            // Deduct for Global Noindex flag
            if (await IsGlobalNoindexActiveAsync())
            {
                score -= 30;
            }

            var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            if (!File.Exists(Path.Combine(wwwroot, "robots.txt"))) score -= 5;
            if (!File.Exists(Path.Combine(wwwroot, "sitemap_index.xml"))) score -= 5;

            var scans = await _context.SeoPageScans.ToListAsync();
            if (scans.Any())
            {
                int totalPages = scans.Count;

                int badH1Count = scans.Count(s => s.H1Count != 1);
                double badH1Ratio = (double)badH1Count / totalPages;
                score -= (int)Math.Round(badH1Ratio * 15);

                int badDescCount = scans.Count(s =>
                    string.IsNullOrWhiteSpace(s.MetaDescription) ||
                    s.MetaDescription.Length < 120 ||
                    s.MetaDescription.Length > 160);
                double badDescRatio = (double)badDescCount / totalPages;
                score -= (int)Math.Round(badDescRatio * 20);
            }

            var missingAltCount = await _context.SeoImages.CountAsync();
            if (missingAltCount > 0)
            {
                int penaltyAlt = Math.Min(missingAltCount * 1, 15);
                score -= penaltyAlt;
            }

            return Math.Clamp(score, 0, 100);
        }
    }
}