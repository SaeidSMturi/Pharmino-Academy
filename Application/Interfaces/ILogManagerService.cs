using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface ILogManagerService
    {
        Task<LogSummaryDto> GetLogSummaryAsync();
        Task<string> GetRawLogFileContentAsync(string filePath);
        Task<bool> ClearAllLogsAsync();
    }
}
