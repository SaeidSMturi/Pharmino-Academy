public class LogSummaryDto
{
    public int TotalErrorsCount { get; set; }
    public List<LogFileInfoDto> LogFiles { get; set; } = new();
}

public class LogFileInfoDto
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int ErrorCount { get; set; }
    public List<UniqueErrorDto> UniqueErrors { get; set; } = new();
}

public class UniqueErrorDto
{
    public string ErrorMessage { get; set; }
    public int OccurrenceCount { get; set; }

    // فیلدهای جدید برای نمایش در Popover
    public string LastOccurrenceDate { get; set; }
    public string Section { get; set; }
}