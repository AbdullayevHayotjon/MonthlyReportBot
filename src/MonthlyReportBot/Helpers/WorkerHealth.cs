using Microsoft.Extensions.Options;
using MonthlyReportBot.Configuration;
namespace MonthlyReportBot.Helpers;
public sealed class WorkerHealth(IOptions<BotOptions> options)
{
 static string PathFor(BotOptions o)=>Path.GetFullPath(string.IsNullOrWhiteSpace(o.HealthFile)?Path.Combine(o.StorageRoot,".worker-health"):o.HealthFile);
 public void Clear(){var p=PathFor(options.Value);if(File.Exists(p))File.Delete(p);}
 public void Touch()
 {
  var p=PathFor(options.Value);Directory.CreateDirectory(Path.GetDirectoryName(p)!);
  File.WriteAllText(p,DateTime.UtcNow.ToString("O"));
  if(!OperatingSystem.IsWindows())File.SetUnixFileMode(p,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.GroupRead);
 }
 public static bool IsHealthy(BotOptions o)
 {
  var p=PathFor(o);
  try{return File.Exists(p)&&DateTime.TryParse(File.ReadAllText(p),null,System.Globalization.DateTimeStyles.RoundtripKind,out var time)&&DateTime.UtcNow-time.ToUniversalTime()<TimeSpan.FromMinutes(5)&&time.ToUniversalTime()<=DateTime.UtcNow.AddSeconds(10);}
  catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
 }
}
