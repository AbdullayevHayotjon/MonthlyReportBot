using Microsoft.Extensions.Options;
using MonthlyReportBot.Configuration;
using MonthlyReportBot.Data;
using MonthlyReportBot.Data.Entities;
using MonthlyReportBot.Models;
using MonthlyReportBot.Services.Tasks;
namespace MonthlyReportBot.Services.Reports;
public sealed class ReportService(TaskService tasks,WordReportGenerator generator,AppDbContext db,IOptions<BotOptions> settings)
{
 public async Task<(string FileName,byte[] Bytes)> Create(Employee user,DateOnly start,DateOnly end,bool monthly,CancellationToken ct)
 {
  var query=tasks.PeriodQuery(user.Id,start,end);
  var list=await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(query,x=>x.Images).Take(settings.Value.MaxReportTasks+1),ct);
  var bytes=generator.Generate(user,start,end,monthly,list);
  var suffix=monthly?$"{start:yyyy_MM}":$"{start:yyyy-MM-dd}_{end:yyyy-MM-dd}";
  var filename=$"Hisobot_{Names.Safe(user.LastName)}_{Names.Safe(user.FirstName)}_{suffix}.docx";
  return(filename,bytes);
 }
 public async Task Record(int user,DateOnly start,DateOnly end,bool monthly,string filename,CancellationToken ct)
 {db.ReportHistory.Add(new(){UserId=user,StartDate=start,EndDate=end,ReportType=monthly?"month":"range",FileName=filename});await db.SaveChangesAsync(ct);}
}
