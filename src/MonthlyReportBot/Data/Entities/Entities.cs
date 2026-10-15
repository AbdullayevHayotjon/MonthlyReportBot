namespace MonthlyReportBot.Data.Entities;
public sealed class Employee
{
 public int Id { get; set; }
 public long TelegramUserId { get; set; }
 public string FirstName { get; set; } = "";
 public string LastName { get; set; } = "";
 public string PositionType { get; set; } = "";
 public string? Username { get; set; }
 public bool IsActive { get; set; } = true;
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
 public string FullName => $"{FirstName} {LastName}";
}
public sealed class WorkTask
{
 public int Id { get; set; }
 public int UserId { get; set; }
 public Employee User { get; set; } = null!;
 public DateOnly WorkDate { get; set; }
 public string Description { get; set; } = "";
 public bool IsDraft { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
 public List<TaskImage> Images { get; set; } = [];
}
public sealed class TaskImage
{
 public int Id { get; set; }
 public int TaskId { get; set; }
 public WorkTask Task { get; set; } = null!;
 public string FilePath { get; set; } = "";
 public string TelegramFileId { get; set; } = "";
 public string OriginalFileName { get; set; } = "";
 public string MimeType { get; set; } = "";
 public long FileSize { get; set; }
 public int SortOrder { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class UserSession
{
 public int Id { get; set; }
 public int UserId { get; set; }
 public Employee User { get; set; } = null!;
 public string CurrentState { get; set; } = "Menu";
 public string StateDataJson { get; set; } = "{}";
 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class ReportHistory
{
 public int Id { get; set; }
 public int UserId { get; set; }
 public Employee User { get; set; } = null!;
 public string ReportType { get; set; } = "month";
 public DateOnly StartDate { get; set; }
 public DateOnly EndDate { get; set; }
 public string FileName { get; set; } = "";
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class PollCursor { public int Id { get; set; } = 1; public int NextUpdateId { get; set; } }
