using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MonthlyReportBot.Configuration;
using MonthlyReportBot.Data;
using MonthlyReportBot.Data.Entities;
using MonthlyReportBot.Handlers;
using MonthlyReportBot.Helpers;
using MonthlyReportBot.Services.Images;
using MonthlyReportBot.Services.Reports;
using MonthlyReportBot.Services.Tasks;
using MonthlyReportBot.Services.Telegram;
using MonthlyReportBot.Services.Users;
using Telegram.Bot;

// Resolve all relative data paths against the executable, including Windows service runs.
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
var builder=Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
 Args=args,
 ContentRootPath=AppContext.BaseDirectory
});
builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json",optional:false,reloadOnChange:false);
builder.Configuration.AddJsonFile("appsettings.Local.json",optional:true,reloadOnChange:false);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddWindowsService(o=>o.ServiceName="MonthlyReportBot");
builder.Services.AddOptions<BotOptions>().Bind(builder.Configuration.GetSection("Bot"))
 .Validate(o=>args.Contains("--migrate")||System.Text.RegularExpressions.Regex.IsMatch(o.Token,@"^\d+:[A-Za-z0-9_-]{30,}$"),"appsettings.json ichidagi Bot:Token qiymatini tekshiring.")
 .Validate(o=>File.Exists(o.TemplatePath),"Word shablon topilmadi.")
 .Validate(o=>o.MaxImageBytes>0&&o.MaxImageBytes<=20*1024*1024&&o.MaxImagesPerTask>0&&o.MaxReportImages>0&&o.MaxReportTasks>0&&o.MaxImagePixels is >0 and <=100_000_000&&o.MaxReportBytes is >1048576 and <=49*1024*1024,"Limitlar noto‘g‘ri.")
 .Validate(o=>o.Positions.Length>0&&o.Positions.All(p=>p.Length is >0 and <=100),"Lavozimlar ro‘yxati noto‘g‘ri.")
 .Validate(o=>!string.IsNullOrWhiteSpace(o.OrganizationName)&&!string.IsNullOrWhiteSpace(o.DepartmentName),"Tashkilot/bo‘lim bo‘sh.")
 .Validate(o=>{try{TimeZoneInfo.FindSystemTimeZoneById(o.DefaultTimeZone);return true;}catch{return false;}},"Vaqt mintaqasi noto‘g‘ri.").ValidateOnStart();
var settings=builder.Configuration.GetSection("Bot").Get<BotOptions>()??new();
if(args.Contains("--health"))
{
 Environment.ExitCode=MonthlyReportBot.Helpers.WorkerHealth.IsHealthy(settings)?0:1;
 return;
}
if(args.Contains("--verify-package"))
{
 MonthlyReportBot.Helpers.PackageVerifier.Verify(settings);
 return;
}
Directory.CreateDirectory(settings.StorageRoot);
var sqlite=new SqliteConnectionStringBuilder(settings.ConnectionString);
if(sqlite.DataSource!=":memory:"){sqlite.DataSource=Path.GetFullPath(sqlite.DataSource);Directory.CreateDirectory(Path.GetDirectoryName(sqlite.DataSource)!);}
builder.Services.AddDbContext<AppDbContext>(o=>o.UseSqlite(sqlite.ToString()));
builder.Services.AddSingleton<ITelegramBotClient>(_=>new TelegramBotClient(settings.Token));
builder.Services.AddScoped<UserService>();builder.Services.AddScoped<TaskService>();builder.Services.AddScoped<ImageService>();builder.Services.AddScoped<FileChanges>();builder.Services.AddScoped<WordReportGenerator>();builder.Services.AddScoped<ReportService>();builder.Services.AddScoped<BotHandler>();
builder.Services.AddHostedService<PollingWorker>();
builder.Services.AddSingleton<WorkerHealth>();
builder.Logging.ClearProviders();builder.Logging.AddConsole();builder.Logging.AddProvider(new PrivateFileLoggerProvider(Path.Combine(settings.StorageRoot,"logs")));
using var host=builder.Build();
try
{
 _=host.Services.GetRequiredService<IOptions<BotOptions>>().Value;
 // Lock the token across local processes, even if their working directories differ.
 var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(settings.Token)))[..20];
 var lockDir=Path.Combine(Path.GetTempPath(),"MonthlyReportBot-locks");Directory.CreateDirectory(lockDir);
 using var instanceLock=new FileStream(Path.Combine(lockDir,hash+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
 using(var scope=host.Services.CreateScope())
 {
  var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();await db.Database.MigrateAsync();
  await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
  if(!await db.PollCursors.AnyAsync()){db.PollCursors.Add(new PollCursor());await db.SaveChangesAsync();}
  // Clean up files left by a crash between a disk write and a SQLite commit.
  var imageRoot=Path.Combine(settings.StorageRoot,"images");
  if(Directory.Exists(imageRoot))
  {
   var used=(await db.TaskImages.Select(x=>x.FilePath).ToListAsync()).Select(p=>Path.GetFullPath(Path.Combine(settings.StorageRoot,p))).ToHashSet(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
   foreach(var p in Directory.EnumerateFiles(imageRoot,"*",SearchOption.AllDirectories))if(!used.Contains(Path.GetFullPath(p))&&File.GetLastWriteTimeUtc(p)<DateTime.UtcNow.AddHours(-1))File.Delete(p);
  }
 }
 if(args.Contains("--migrate")){Console.WriteLine("SQLite migratsiyalari bajarildi.");return;}
 if(args.Contains("--check"))
 {
  using var scope=host.Services.CreateScope();var bot=scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();var me=await bot.GetMe();var webhook=await bot.GetWebhookInfo();
  Console.WriteLine(JsonSerializer.Serialize(new{ok=true,bot=me.Username,webhookConfigured=!string.IsNullOrEmpty(webhook.Url)}));return;
 }
 await host.RunAsync();
}
catch(OptionsValidationException ex)
{
 Console.Error.WriteLine("Sozlamalarda xato:");
 foreach(var failure in ex.Failures)Console.Error.WriteLine("- "+failure);
 Environment.ExitCode=1;
}
catch(IOException){Console.Error.WriteLine("Bot nusxasi allaqachon ishlayapti yoki storage fayliga kirish imkoni yo‘q.");Environment.ExitCode=1;}
catch(Exception ex){Console.Error.WriteLine($"Ishga tushirish xatosi: {ex.GetType().Name}. Sozlamalar, token va storage huquqlarini tekshiring.");Environment.ExitCode=1;}
