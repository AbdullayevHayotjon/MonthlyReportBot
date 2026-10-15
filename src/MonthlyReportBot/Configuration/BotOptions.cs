namespace MonthlyReportBot.Configuration;
public sealed class BotOptions
{
 public string Token { get; set; } = "";
 public string ConnectionString { get; set; } = "Data Source=storage/bot.db";
 public string TemplatePath { get; set; } = "Templates/MonthlyReportTemplate.docx";
 public string StorageRoot { get; set; } = "storage";
 public long MaxImageBytes { get; set; } = 10 * 1024 * 1024;
 public int MaxImagesPerTask { get; set; } = 50;
 public int MaxReportImages { get; set; } = 200;
 public long MaxReportBytes { get; set; } = 45 * 1024 * 1024;
 public int MaxReportTasks { get; set; } = 1000;
 public long MaxImagePixels { get; set; } = 40_000_000;
 public string DefaultTimeZone { get; set; } = "Asia/Tashkent";
 public string OrganizationName { get; set; } = "";
 public string DepartmentName { get; set; } = "";
 public string[] Positions { get; set; } = [];
 public long[] AllowedUserIds { get; set; } = [];
 public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZone)));
}
public sealed class UserInputException(string message) : Exception(message);
