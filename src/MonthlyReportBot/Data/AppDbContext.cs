using Microsoft.EntityFrameworkCore;
using MonthlyReportBot.Data.Entities;
namespace MonthlyReportBot.Data;
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
 public DbSet<Employee> Users => Set<Employee>();
 public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
 public DbSet<TaskImage> TaskImages => Set<TaskImage>();
 public DbSet<UserSession> UserSessions => Set<UserSession>();
 public DbSet<ReportHistory> ReportHistory => Set<ReportHistory>();
 public DbSet<PollCursor> PollCursors => Set<PollCursor>();
 protected override void OnModelCreating(ModelBuilder b)
 {
  b.Entity<Employee>().HasIndex(x=>x.TelegramUserId).IsUnique();
  b.Entity<Employee>().Ignore(x=>x.FullName);
  b.Entity<WorkTask>().HasIndex(x=>new {x.UserId,x.WorkDate});
  b.Entity<WorkTask>().HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<TaskImage>().HasIndex(x=>new{x.TaskId,x.SortOrder});
  b.Entity<TaskImage>().HasOne(x=>x.Task).WithMany(x=>x.Images).HasForeignKey(x=>x.TaskId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<UserSession>().HasIndex(x=>x.UserId).IsUnique();
  b.Entity<UserSession>().HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<ReportHistory>().HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<ReportHistory>().HasIndex(x=>new{x.UserId,x.CreatedAt});
 }
}
