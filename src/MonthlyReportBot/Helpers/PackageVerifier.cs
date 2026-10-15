using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MonthlyReportBot.Configuration;
using MonthlyReportBot.Data;
using MonthlyReportBot.Data.Entities;
using MonthlyReportBot.Services.Images;
using MonthlyReportBot.Services.Reports;
using SkiaSharp;
using MonthlyReportBot.Models;
namespace MonthlyReportBot.Helpers;
// Offline verification of native dependencies and DOCX generation in the actual published package.
public static class PackageVerifier
{
 public static void Verify(BotOptions source)
 {
  var temp=Path.Combine(Path.GetTempPath(),"MonthlyReportBot-verify-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  try
  {
   foreach(var (first,last,expected) in new[]{("Shoxijahon","Xalimov","Sh.Xalimov"),("shoxijahon","Xalimov","Sh.Xalimov"),("SHOXIJAHON","Xalimov","Sh.Xalimov"),("Chori","Aliyev","Ch.Aliyev"),("Sardor","Xalimov","S.Xalimov"),("Hayotjon","Abdullayev","H.Abdullayev")})
   {
    if(Names.Short(first,last)!=expected||Names.Short(first,last,true)!=expected.Replace(".",". "))
     throw new InvalidDataException("Uzbek name initial verification failed");
   }
   var o=new BotOptions{StorageRoot=temp,TemplatePath=Path.GetFullPath(source.TemplatePath),OrganizationName=source.OrganizationName,DepartmentName=source.DepartmentName};
   using(var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(temp,"verify.db")}").Options))
   {db.Database.Migrate();if(db.Users.Any())throw new InvalidDataException("Verification database must be empty");}
   using var bitmap=new SKBitmap(80,40);bitmap.Erase(SKColors.LightBlue);using var img=SKImage.FromBitmap(bitmap);using var png=img.Encode(SKEncodedImageFormat.Png,100);
   var bytes=png.ToArray();ImageService.Validate(bytes,o.MaxImageBytes,o.MaxImagePixels);File.WriteAllBytes(Path.Combine(temp,"verify.png"),bytes);
   var user=new Employee{Id=1,FirstName="CI",LastName="Verification",PositionType="Dasturchi (back-end)"};
   var task=new WorkTask{Id=1,UserId=1,WorkDate=new(2026,1,1),Description="Package verification"};
   using var changes=new FileChanges(Options.Create(o),NullLogger<FileChanges>.Instance);var generator=new WordReportGenerator(Options.Create(o),changes);
   generator.Generate(user,new(2026,1,1),new(2026,1,31),true,[task]);
   task.Images.Add(new(){TaskId=1,FilePath="verify.png",MimeType="image/png",FileSize=bytes.Length});
   generator.Generate(user,new(2026,1,1),new(2026,1,31),true,[task]);
   Console.WriteLine("Package verified: SQLite migrations, PNG decoding, DOCX with/without image. No Telegram calls.");
  }
  finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(temp,true);}
 }
}
