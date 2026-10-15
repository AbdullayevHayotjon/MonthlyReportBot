using Microsoft.EntityFrameworkCore;
using MonthlyReportBot.Data;
using MonthlyReportBot.Data.Entities;
using MonthlyReportBot.Configuration;
using MonthlyReportBot.Models;
namespace MonthlyReportBot.Services.Tasks;
public sealed class TaskService(AppDbContext db)
{
 public IQueryable<WorkTask> Owned(int user) => db.WorkTasks.Where(x=>x.UserId==user);
 public async Task<WorkTask> Get(int user,int id,CancellationToken ct=default) => await Owned(user).Include(x=>x.Images).SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new UserInputException("Task topilmadi yoki sizga tegishli emas.");
 public static string Text(string text) {text=text.Trim();if(text.Length is <1 or >16000) throw new UserInputException("Task matni 1–16000 belgi bo‘lsin.");try{System.Xml.XmlConvert.VerifyXmlChars(text);}catch(System.Xml.XmlException){throw new UserInputException("Matnda Word qo‘llamaydigan boshqaruv belgisi bor.");}return text;}
 public async Task<WorkTask> Add(int user,DateOnly date,string text,bool draft=false,CancellationToken ct=default)
 {var t=new WorkTask{UserId=user,WorkDate=date,Description=Text(text),IsDraft=draft};db.WorkTasks.Add(t);await db.SaveChangesAsync(ct);return t;}
 public async Task Edit(int user,int id,string? text=null,DateOnly? date=null,CancellationToken ct=default)
 {var t=await Get(user,id,ct);if(text!=null)t.Description=Text(text);if(date!=null)t.WorkDate=date.Value;t.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);}
 public async Task Delete(int user,int id,CancellationToken ct=default) {var t=await Get(user,id,ct);db.WorkTasks.Remove(t);await db.SaveChangesAsync(ct);}
 public IQueryable<WorkTask> PeriodQuery(int user,DateOnly start,DateOnly end)
 {Dates.Validate(start,end);return Owned(user).Where(x=>!x.IsDraft&&x.WorkDate>=start&&x.WorkDate<=end).OrderBy(x=>x.WorkDate).ThenBy(x=>x.CreatedAt).ThenBy(x=>x.Id);}
}
