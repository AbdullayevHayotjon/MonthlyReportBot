using MonthlyReportBot.Configuration;
using Microsoft.Extensions.Options;
namespace MonthlyReportBot.Services.Images;
// Files created in a DB transaction are removed on rollback; deletions wait for commit.
public sealed class FileChanges(IOptions<BotOptions> options, ILogger<FileChanges> log) : IDisposable
{
 readonly List<string> created=[]; readonly List<string> deleted=[];
 bool committed;
 public string Root => Path.GetFullPath(options.Value.StorageRoot);
 public string Resolve(string relative)
 {
  var p=Path.GetFullPath(Path.Combine(Root,relative));
  if(!p.StartsWith(Root+Path.DirectorySeparatorChar,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe storage path");
  return p;
 }
 public void Created(string path) => created.Add(path);
 public void DeleteAfterCommit(string relative) => deleted.Add(Resolve(relative));
 public void Commit() {committed=true;foreach(var p in deleted)TryDelete(p);}
 void TryDelete(string p) {try{File.Delete(p);}catch(IOException){log.LogWarning("Faylni tozalash keyingi ishga tushishda takrorlanadi.");}catch(UnauthorizedAccessException){log.LogWarning("Storage yozish huquqini tekshiring.");}}
 public void Dispose(){if(!committed)foreach(var p in created)TryDelete(p);}
}
