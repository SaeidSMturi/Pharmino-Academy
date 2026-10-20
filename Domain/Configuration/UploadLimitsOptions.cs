namespace Domain.Configuration;

public sealed class UploadLimitsOptions
{
    public const string SectionName = "UploadLimits";

    public long CourseCoverMaxBytes { get; set; } = 10L * 1024 * 1024;
    public long CourseVideoMaxBytes { get; set; } = 10L * 1024 * 1024 * 1024;
    public long CoursePdfMaxBytes { get; set; } = 200L * 1024 * 1024;
}
