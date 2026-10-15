using System.Text.RegularExpressions;
namespace MonthlyReportBot.Helpers;
// Never persist exception messages, request URLs, profile names or user message contents.
public sealed class PrivateFileLoggerProvider(string root) : ILoggerProvider
{
 readonly string directory=root;
 readonly object gate=new();
 public ILogger CreateLogger(string categoryName)=>new PrivateLogger(this,categoryName);
 public void Dispose(){}
 sealed class PrivateLogger(PrivateFileLoggerProvider owner,string category) : ILogger
 {
  public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
  public bool IsEnabled(LogLevel level)=>level>=LogLevel.Information;
  public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
  {
   if(!IsEnabled(level)||category.StartsWith("System.Net.Http")||category.StartsWith("Microsoft.EntityFrameworkCore"))return;
   string message=Regex.Replace(formatter(state,null),@"\b\d{5,}:[A-Za-z0-9_-]{20,}","[REDACTED]");
   lock(owner.gate)
   {
    Directory.CreateDirectory(owner.directory);
    File.AppendAllText(Path.Combine(owner.directory,$"bot-{DateTime.UtcNow:yyyy-MM-dd}.log"),$"{DateTime.UtcNow:O} [{level}] {category}: {message}\n");
    foreach(var old in Directory.EnumerateFiles(owner.directory,"bot-*.log").Where(p=>File.GetLastWriteTimeUtc(p)<DateTime.UtcNow.AddDays(-14)))try{File.Delete(old);}catch(IOException){}
   }
  }
 }
}
