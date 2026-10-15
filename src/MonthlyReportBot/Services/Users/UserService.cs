using Microsoft.EntityFrameworkCore;
using MonthlyReportBot.Data;
using MonthlyReportBot.Data.Entities;
using MonthlyReportBot.Configuration;
namespace MonthlyReportBot.Services.Users;
public sealed class UserService(AppDbContext db)
{
 public async Task<Employee> GetOrCreate(long telegramId,string? username,CancellationToken ct=default)
 {
  var u=await db.Users.SingleOrDefaultAsync(x=>x.TelegramUserId==telegramId,ct);
  if(u is null) {u=new Employee{TelegramUserId=telegramId,Username=username};db.Users.Add(u);}
  else u.Username=username;
  await db.SaveChangesAsync(ct);return u;
 }
 public static string Name(string text) {text=text.Trim();if(text.Length is <1 or >80 || text.Any(char.IsControl)) throw new UserInputException("Ism/familiya 1–80 belgi bo‘lsin.");return text;}
 public async Task Save(Employee u,string first,string last,string position,CancellationToken ct=default)
 {u.FirstName=Name(first);u.LastName=Name(last);u.PositionType=position;u.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);}
}
