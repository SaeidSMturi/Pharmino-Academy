using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace Application.Services
{
    public class ParsedLogEntry
    {
        public string Timestamp { get; set; } = "-";
        public string Section { get; set; } = "-";
        public string Message { get; set; } = "";
    }

    public class LogManagerService : ILogManagerService
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _logsRootPath;

        public LogManagerService(IWebHostEnvironment env)
        {
            _env = env;
            // The root directory where log files are stored
            _logsRootPath = Path.Combine(_env.ContentRootPath, "Logs");
        }

        public async Task<LogSummaryDto> GetLogSummaryAsync()
        {
            var summary = new LogSummaryDto();

            if (!Directory.Exists(_logsRootPath))
                return summary;

            var logFiles = Directory.GetFiles(_logsRootPath, "*.txt", SearchOption.AllDirectories);

            foreach (var filePath in logFiles)
            {
                var fileInfo = new FileInfo(filePath);

                // Ignore empty files
                if (fileInfo.Length == 0)
                    continue;

                var relativePath = Path.GetRelativePath(_logsRootPath, filePath).Replace('\\', '/');
                var fileDto = new LogFileInfoDto
                {
                    FileName = Path.Combine(fileInfo.Directory?.Name ?? "", fileInfo.Name).Replace('\\', '/'),
                    FilePath = relativePath
                };

                var parsedErrors = new List<ParsedLogEntry>();

                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(stream))
                    {
                        bool isCustomErrorLog = fileInfo.Name.Contains("error", StringComparison.OrdinalIgnoreCase);

                        bool inBlock = false;
                        var blockLines = new List<string>();
                        string? line;

                        // State-machine parser to handle mixed format files
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            var trimmedLine = line.Trim();

                            // Detect custom format block boundaries
                            if (trimmedLine.StartsWith("--------------------------------------------------"))
                            {
                                if (inBlock)
                                {
                                    // Block closed
                                    ProcessLogBlock(blockLines, parsedErrors, fileInfo.Name, isCustomErrorLog);
                                    blockLines.Clear();
                                    inBlock = false;
                                }
                                else
                                {
                                    // Block opened
                                    inBlock = true;
                                }
                                continue;
                            }

                            if (inBlock)
                            {
                                blockLines.Add(trimmedLine);
                            }
                            else
                            {
                                // Process standalone lines (Standard Serilog format mixed in the file)
                                if (!string.IsNullOrWhiteSpace(trimmedLine) &&
                                    (trimmedLine.Contains("[ERR]", StringComparison.OrdinalIgnoreCase) ||
                                     trimmedLine.Contains("[Error]", StringComparison.OrdinalIgnoreCase) ||
                                     trimmedLine.Contains("[FTL]", StringComparison.OrdinalIgnoreCase) ||
                                     trimmedLine.Contains("[Fatal]", StringComparison.OrdinalIgnoreCase) ||
                                     trimmedLine.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                                     trimmedLine.Contains("FFmpeg conversion failed")))
                                {
                                    var match = Regex.Match(trimmedLine, @"^(\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:\s\+[\d:]+)?)\s*(?:\[.*?\])?\s*(.*)");
                                    string timestamp = match.Success ? match.Groups[1].Value.Trim() : "-";
                                    string message = match.Success ? match.Groups[2].Value.Trim() : trimmedLine;

                                    parsedErrors.Add(new ParsedLogEntry
                                    {
                                        Timestamp = timestamp,
                                        Section = fileInfo.Name,
                                        Message = message
                                    });
                                }
                            }
                        }

                        // Process the final block if the file ended without a closing separator
                        if (inBlock && blockLines.Count > 0)
                        {
                            ProcessLogBlock(blockLines, parsedErrors, fileInfo.Name, isCustomErrorLog);
                        }
                    }
                }
                catch (IOException)
                {
                    continue;
                }

                fileDto.ErrorCount = parsedErrors.Count;
                summary.TotalErrorsCount += parsedErrors.Count;

                fileDto.UniqueErrors = parsedErrors
                    .GroupBy(e => CleanLogMessage(e.Message))
                    .Select(g => new UniqueErrorDto
                    {
                        ErrorMessage = g.Key,
                        OccurrenceCount = g.Count(),
                        LastOccurrenceDate = g.LastOrDefault()?.Timestamp ?? "-",
                        Section = g.FirstOrDefault()?.Section ?? "-"
                    })
                    .OrderByDescending(g => g.OccurrenceCount)
                    .ToList();

                summary.LogFiles.Add(fileDto);
            }

            // Sort descending by error type and date
            summary.LogFiles = summary.LogFiles
                .OrderByDescending(f => f.FileName.Contains("error", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(f => {
                    var match = Regex.Match(f.FileName, @"\d{8}");
                    return match.Success ? match.Value : "";
                })
                .ToList();

            return summary;
        }

        public async Task<string> GetRawLogFileContentAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Log file path cannot be empty.", nameof(filePath));

            // Prevent path traversal sequences
            if (filePath.Contains(".."))
            {
                throw new UnauthorizedAccessException("Path traversal sequences are strictly forbidden.");
            }

            // Canonicalize the allowed root directory
            string canonicalRoot = Path.GetFullPath(_logsRootPath);
            if (!canonicalRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                canonicalRoot += Path.DirectorySeparatorChar;
            }

            // Resolve full candidate path safely
            string fullPath = Path.IsPathRooted(filePath)
                ? Path.GetFullPath(filePath)
                : Path.GetFullPath(Path.Combine(_logsRootPath, filePath));

            // Validate that the resolved path is strictly within the allowed Logs directory
            if (!fullPath.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Access to files outside the logs directory is forbidden.");
            }

            // Validate file extension is strictly .txt
            if (!string.Equals(Path.GetExtension(fullPath), ".txt", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Only .txt log files may be accessed.");
            }

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("The specified log file was not found.");

            try
            {
                // Using FileShare.ReadWrite to bypass exclusive write locks by Serilog
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    return await reader.ReadToEndAsync();
                }
            }
            catch (IOException ex)
            {
                // Return explicit message instead of crashing the API endpoint
                return $"--- FILE ACCESS ERROR ---\nCould not read the raw file because it is locked by the application.\nError details: {ex.Message}\n\nSolution: Please ensure 'shared: true' is configured in your Serilog File sink settings.";
            }
        }

        public async Task<bool> ClearAllLogsAsync()
        {
            try
            {
                if (!Directory.Exists(_logsRootPath))
                    return false;

                var logFiles = Directory.GetFiles(_logsRootPath, "*.txt", SearchOption.AllDirectories);
                bool allCleared = true;

                foreach (var file in logFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        allCleared = false;
                    }
                }
                return allCleared;
            }
            catch
            {
                return false;
            }
        }

        private void ProcessLogBlock(List<string> lines, List<ParsedLogEntry> errors, string fileName, bool isErrorLog)
        {
            var validLines = lines.Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("---")).ToList();
            if (validLines.Count == 0) return;

            string timestamp = "-";
            string section = fileName;
            var messageLines = new List<string>();

            bool headerFound = false;

            foreach (var l in validLines)
            {
                // Match the timestamp header ONLY for the first match in the block
                if (!headerFound)
                {
                    var match = Regex.Match(l, @"^\[([^\]]+)\]\s*\[([^\]]+)\]");
                    if (match.Success)
                    {
                        timestamp = match.Groups[1].Value.Trim();
                        string sectionPart = match.Groups[2].Value;
                        section = sectionPart.Replace("فایل/بخش:", "").Replace("بخش:", "").Trim();
                        headerFound = true;
                        continue;
                    }
                }

                messageLines.Add(l);
            }

            string message = string.Join(Environment.NewLine, messageLines).Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            // Strict checking for generic log files to ignore non-error/non-warning entries
            if (!isErrorLog)
            {
                bool isError = message.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                               message.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                               message.Contains("Fatal", StringComparison.OrdinalIgnoreCase) ||
                               message.Contains("Fail", StringComparison.OrdinalIgnoreCase) ||
                               message.Contains("Warning", StringComparison.OrdinalIgnoreCase) ||
                               message.Contains("unexpected", StringComparison.OrdinalIgnoreCase);

                if (!isError) return;
            }

            errors.Add(new ParsedLogEntry
            {
                Timestamp = timestamp,
                Section = section,
                Message = message
            });
        }

        private string CleanLogMessage(string logLine)
        {
            if (string.IsNullOrWhiteSpace(logLine)) return logLine;

            var cleaned = Regex.Replace(logLine, @"^\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:\s\+[\d:]+)?", "");
            cleaned = Regex.Replace(cleaned, @"\b[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}\b", "{GUID}");

            return cleaned.Trim();
        }
    }
}