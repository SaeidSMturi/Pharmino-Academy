using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Application.DTOs;

namespace Application.Services
{
    public partial class SeoService
    {
        public async Task<SeoOnPageResult> AnalyzePageAsync(string url, string? keyword = null)
        {
            var result = new SeoOnPageResult
            {
                Url = url,
                StatusCode = 200
            };

            try
            {
                var response = await _httpClient.GetAsync(url);
                result.StatusCode = (int)response.StatusCode;
                result.ResponseMessage = response.ReasonPhrase;

                if (!response.IsSuccessStatusCode)
                {
                    result.PageScore = 0;
                    return result;
                }

                var html = await response.Content.ReadAsStringAsync();

                // 1. Title Audit
                var titleRegex = new Regex(@"<title\b[^>]*>(?<title>.*?)</title>", RegexOptions.IgnoreCase);
                var titleMatch = titleRegex.Match(html);
                if (titleMatch.Success)
                {
                    result.Title = System.Net.WebUtility.HtmlDecode(titleMatch.Groups["title"].Value.Trim());
                    if (result.TitleLength >= 50 && result.TitleLength <= 60)
                    {
                        result.TitleStatus = "Excellent";
                        result.TitleMessage = "Your title tag length is ideal (50-60 characters).";
                    }
                    else if (result.TitleLength >= 30 && result.TitleLength < 50)
                    {
                        result.TitleStatus = "Warning";
                        result.TitleMessage = $"Title is slightly short ({result.TitleLength} chars). Aim for 50-60 characters.";
                    }
                    else if (result.TitleLength > 60 && result.TitleLength <= 80)
                    {
                        result.TitleStatus = "Warning";
                        result.TitleMessage = $"Title is slightly long ({result.TitleLength} chars). It might get truncated in search results.";
                    }
                    else
                    {
                        result.TitleStatus = "Critical";
                        result.TitleMessage = $"Title length ({result.TitleLength} chars) is outside optimal ranges (<30 or >80).";
                    }
                }
                else
                {
                    result.TitleStatus = "Critical";
                    result.TitleMessage = "Title tag is completely missing!";
                }

                // 2. Meta Description Audit
                var descRegex = new Regex(@"<meta\s+[^>]*name=[\x22\x27\s]*description[\x22\x27\s]*content=[\x22\x27\s]*(?<desc>[^\x22\x27>]+)|<meta\s+[^>]*content=[\x22\x27\s]*(?<desc>[^\x22\x27>]+)[\x22\x27\s]*name=[\x22\x27\s]*description", RegexOptions.IgnoreCase);
                var descMatch = descRegex.Match(html);
                if (descMatch.Success)
                {
                    result.MetaDescription = System.Net.WebUtility.HtmlDecode(descMatch.Groups["desc"].Value.Trim());
                    if (result.MetaDescriptionLength >= 120 && result.MetaDescriptionLength <= 160)
                    {
                        result.MetaDescriptionStatus = "Excellent";
                        result.MetaDescriptionMessage = "Your meta description length is ideal (120-160 characters).";
                    }
                    else if (result.MetaDescriptionLength >= 80 && result.MetaDescriptionLength < 120)
                    {
                        result.MetaDescriptionStatus = "Warning";
                        result.MetaDescriptionMessage = $"Meta description is slightly short ({result.MetaDescriptionLength} chars). Aim for 120-160.";
                    }
                    else if (result.MetaDescriptionLength > 160 && result.MetaDescriptionLength <= 200)
                    {
                        result.MetaDescriptionStatus = "Warning";
                        result.MetaDescriptionMessage = $"Meta description is slightly long ({result.MetaDescriptionLength} chars). It may be truncated.";
                    }
                    else
                    {
                        result.MetaDescriptionStatus = "Critical";
                        result.MetaDescriptionMessage = $"Meta description length ({result.MetaDescriptionLength} chars) is outside optimal ranges (<80 or >200).";
                    }
                }
                else
                {
                    result.MetaDescriptionStatus = "Critical";
                    result.MetaDescriptionMessage = "Meta description tag is missing!";
                }

                // 3. Canonical URL
                var canonicalRegex = new Regex(@"<link\s+[^>]*rel=[\x22\x27\s]*canonical[\x22\x27\s]*href=[\x22\x27\s]*(?<href>[^\x22\x27>]+)", RegexOptions.IgnoreCase);
                var canonicalMatch = canonicalRegex.Match(html);
                if (canonicalMatch.Success)
                {
                    result.CanonicalUrl = canonicalMatch.Groups["href"].Value;
                    result.HasCanonical = true;
                    // Check matching
                    try
                    {
                        var cleanCanonical = new Uri(result.CanonicalUrl).PathAndQuery.ToLower();
                        var cleanCurrent = new Uri(url).PathAndQuery.ToLower();
                        result.CanonicalMatchesUrl = cleanCanonical == cleanCurrent;
                    }
                    catch
                    {
                        result.CanonicalMatchesUrl = result.CanonicalUrl.EndsWith(NormalizeUrlPath(url), StringComparison.OrdinalIgnoreCase);
                    }
                }

                // 4. Robots Directives
                var robotsRegex = new Regex(@"<meta\s+[^>]*name=[\x22\x27\s]*robots[\x22\x27\s]*content=[\x22\x27\s]*(?<robots>[^\x22\x27>]+)", RegexOptions.IgnoreCase);
                var robotsMatch = robotsRegex.Match(html);
                if (robotsMatch.Success)
                {
                    result.RobotsMeta = robotsMatch.Groups["robots"].Value;
                    result.IsNoindex = result.RobotsMeta.Contains("noindex", StringComparison.OrdinalIgnoreCase);
                    result.IsNofollow = result.RobotsMeta.Contains("nofollow", StringComparison.OrdinalIgnoreCase);
                }

                // 5. Heading Structure
                var headingsRegex = new Regex(@"<h(?<level>[1-6])\b[^>]*>(?<content>.*?)</h\k<level>>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var headingsMatches = headingsRegex.Matches(html);
                int lastLevel = 0;
                bool sequenceError = false;

                foreach (Match hMatch in headingsMatches)
                {
                    int level = int.Parse(hMatch.Groups["level"].Value);
                    string rawContent = hMatch.Groups["content"].Value;
                    // Strip nested tags in headings
                    string cleanContent = Regex.Replace(rawContent, "<.*?>", string.Empty).Trim();
                    cleanContent = System.Net.WebUtility.HtmlDecode(cleanContent);

                    result.Headings.Add(new HeadingItem { Type = $"H{level}", Content = cleanContent });
                    if (level == 1) result.H1Count++;

                    // Verify headings sequence hierarchy (no gaps like H1 -> H3)
                    if (lastLevel > 0 && level > lastLevel + 1)
                    {
                        sequenceError = true;
                    }
                    lastLevel = level;
                }

                if (result.H1Count == 1 && !sequenceError)
                {
                    result.HeadingStatus = "Excellent";
                    result.HeadingMessage = "Your headings are structured correctly with exactly one H1 tag.";
                }
                else if (result.H1Count == 0)
                {
                    result.HeadingStatus = "Critical";
                    result.HeadingMessage = "H1 tag is completely missing! Every page must have exactly one H1.";
                }
                else if (result.H1Count > 1)
                {
                    result.HeadingStatus = "Warning";
                    result.HeadingMessage = $"Duplicate H1 tags found ({result.H1Count}). Aim for a single main H1.";
                }
                else
                {
                    result.HeadingStatus = "Warning";
                    result.HeadingMessage = "Heading level hierarchy has sequence gaps (e.g. jumping from H2 directly to H4).";
                }

                // 6. Structured Data Schema
                var schemaRegex = new Regex(@"<script\s+[^>]*type=[\x22\x27]application/ld\+json[\x22\x27][^>]*>(?<json>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var schemaMatches = schemaRegex.Matches(html);
                foreach (Match sMatch in schemaMatches)
                {
                    var json = sMatch.Groups["json"].Value;
                    var typeRegex = new Regex(@"[\x22\x27]@type[\x22\x27]\s*:\s*[\x22\x27](?<type>[^\x22\x27]+)[\x22\x27]", RegexOptions.IgnoreCase);
                    var typeMatches = typeRegex.Matches(json);
                    foreach (Match tMatch in typeMatches)
                    {
                        var tName = tMatch.Groups["type"].Value;
                        if (!result.SchemaTypes.Contains(tName))
                        {
                            result.SchemaTypes.Add(tName);
                        }
                    }
                }

                // 7. OpenGraph / Social Media tags
                var ogRegex = new Regex(@"<meta\s+[^>]*(?:property|name)=[\x22\x27](?<name>og:[^\x22\x27]+|twitter:[^\x22\x27]+)[\x22\x27]\s+content=[\x22\x27](?<content>[^\x22\x27]*)[\x22\x27][^>]*>|<meta\s+[^>]*content=[\x22\x27](?<content>[^\x22\x27]*)[\x22\x27]\s+(?:property|name)=[\x22\x27](?<name>og:[^\x22\x27]+|twitter:[^\x22\x27]+)[\x22\x27][^>]*>", RegexOptions.IgnoreCase);
                var ogMatches = ogRegex.Matches(html);
                foreach (Match ogMatch in ogMatches)
                {
                    var name = ogMatch.Groups["name"].Value.ToLower();
                    var content = System.Net.WebUtility.HtmlDecode(ogMatch.Groups["content"].Value);
                    if (name.StartsWith("og:"))
                    {
                        result.OpenGraphTags[name] = content;
                    }
                    else
                    {
                        result.TwitterCardTags[name] = content;
                    }
                }

                // 8. Word count and Text analysis
                var bodyTextOnly = Regex.Replace(html, @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>|<style\b[^<]*(?:(?!<\/style>)<[^<]*)*<\/style>|<[^>]+>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var words = bodyTextOnly.Split(new[] { ' ', '\r', '\n', '\t', '.', ',', '!', '?', '،', '؛' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Trim())
                    .Where(w => w.Length > 1)
                    .ToList();

                result.WordCount = words.Count;
                if (result.WordCount > 800)
                {
                    result.ReadabilityEstimation = "High Content Value (Long-form post)";
                }
                else if (result.WordCount >= 300)
                {
                    result.ReadabilityEstimation = "Standard Content Value";
                }
                else
                {
                    result.ReadabilityEstimation = "Thin Content Warning (<300 words)";
                }

                // 9. Keyword Density Check
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var cleanKeyword = keyword.Trim().ToLower();
                    int kwCount = 0;
                    
                    // Simple count in words list
                    foreach (var word in words)
                    {
                        if (word.Equals(cleanKeyword, StringComparison.OrdinalIgnoreCase) || 
                            cleanKeyword.Contains(word, StringComparison.OrdinalIgnoreCase))
                        {
                            kwCount++;
                        }
                    }

                    // Fallback to substring matching if count is zero (especially for Persian compound keywords)
                    if (kwCount == 0)
                    {
                        var matches = Regex.Matches(bodyTextOnly, Regex.Escape(cleanKeyword), RegexOptions.IgnoreCase);
                        kwCount = matches.Count;
                    }

                    result.KeywordCount = kwCount;
                    if (result.WordCount > 0)
                    {
                        result.KeywordDensityPercentage = Math.Round((double)kwCount / result.WordCount * 100, 2);
                    }

                    bool inTitle = result.Title?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false;
                    bool inDesc = result.MetaDescription?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false;
                    bool inH1 = result.Headings.Any(h => h.Type == "H1" && h.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                    if (result.KeywordDensityPercentage >= 1.0 && result.KeywordDensityPercentage <= 2.5 && inTitle && inDesc)
                    {
                        result.KeywordStatus = "Excellent";
                        result.KeywordMessage = $"Keyword matches optimally ({result.KeywordDensityPercentage}% density) and is found in Title & Meta Description.";
                    }
                    else if (result.KeywordCount == 0)
                    {
                        result.KeywordStatus = "Critical";
                        result.KeywordMessage = "Target keyword not found in the body text!";
                    }
                    else if (result.KeywordDensityPercentage > 3.0)
                    {
                        result.KeywordStatus = "Warning";
                        result.KeywordMessage = $"Keyword stuffing risk ({result.KeywordDensityPercentage}% density). Reduce keyword frequency.";
                    }
                    else
                    {
                        var missings = new List<string>();
                        if (!inTitle) missings.Add("Title");
                        if (!inDesc) missings.Add("Meta Description");
                        if (!inH1) missings.Add("H1 Header");

                        result.KeywordStatus = "Warning";
                        result.KeywordMessage = $"Keyword density is low ({result.KeywordDensityPercentage}%). Missing in: " + string.Join(", ", missings);
                    }
                }

                // 10. Images Audit
                var imgRegex = new Regex(@"<img\b[^>]*>", RegexOptions.IgnoreCase);
                var srcRegex = new Regex(@"src=[""'](?<src>[^""']+)[""']", RegexOptions.IgnoreCase);
                var altRegex = new Regex(@"alt=[""'](?<alt>[^""']*)[""']", RegexOptions.IgnoreCase);
                
                var imgMatches = imgRegex.Matches(html);
                result.TotalImages = imgMatches.Count;

                foreach (Match iMatch in imgMatches)
                {
                    var tag = iMatch.Value;
                    var altM = altRegex.Match(tag);
                    var srcM = srcRegex.Match(tag);
                    
                    bool hasAlt = altM.Success && !string.IsNullOrWhiteSpace(altM.Groups["alt"].Value);
                    if (!hasAlt && srcM.Success)
                    {
                        result.ImagesMissingAltCount++;
                        result.ImagesMissingAltList.Add(srcM.Groups["src"].Value);
                    }
                }

                // 11. Links Audit
                var hrefRegex = new Regex(@"href=[""'](?<href>[^""'#\s]+)[""']", RegexOptions.IgnoreCase);
                var aRegex = new Regex(@"<a\b[^>]*>", RegexOptions.IgnoreCase);
                
                var aMatches = aRegex.Matches(html);
                result.TotalLinks = aMatches.Count;

                var baseUri = new Uri(url);
                foreach (Match aMatch in aMatches)
                {
                    var tag = aMatch.Value;
                    var hrefM = hrefRegex.Match(tag);
                    if (hrefM.Success)
                    {
                        var href = hrefM.Groups["href"].Value;
                        if (href.StartsWith("/") || href.Contains(baseUri.Host))
                        {
                            result.InternalLinksCount++;
                        }
                        else if (href.StartsWith("http"))
                        {
                            result.ExternalLinksCount++;
                        }
                    }
                }

                // 12. Calculate Page Score
                int score = 100;
                if (result.TitleStatus == "Critical") score -= 15;
                if (result.TitleStatus == "Warning") score -= 5;
                
                if (result.MetaDescriptionStatus == "Critical") score -= 15;
                if (result.MetaDescriptionStatus == "Warning") score -= 5;

                if (result.H1Count == 0) score -= 15;
                if (result.H1Count > 1) score -= 8;
                if (sequenceError) score -= 5;

                if (!result.HasCanonical) score -= 5;
                if (result.HasCanonical && !result.CanonicalMatchesUrl) score -= 5;

                if (result.IsNoindex) score -= 30; // Blocks indexing completely

                if (result.ImagesMissingAltCount > 0)
                {
                    score -= Math.Min(result.ImagesMissingAltCount * 2, 10);
                }

                if (!result.SchemaTypes.Any()) score -= 5;

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    if (result.KeywordStatus == "Critical") score -= 15;
                    if (result.KeywordStatus == "Warning") score -= 5;
                }

                result.PageScore = Math.Clamp(score, 0, 100);
            }
            catch (Exception ex)
            {
                result.StatusCode = 500;
                result.ResponseMessage = $"Analysis error: {ex.Message}";
                result.PageScore = 0;
            }

            return result;
        }
    }
}
