using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MonthlyReportBot.Configuration;
using MonthlyReportBot.Data;
using MonthlyReportBot.Data.Entities;
using MonthlyReportBot.Services.Tasks;
using SkiaSharp;
using Telegram.Bot;
namespace MonthlyReportBot.Services.Images;
public sealed class ImageService(AppDbContext db,TaskService tasks,FileChanges files,ITelegramBotClient bot,IOptions<BotOptions> settings)
{
 readonly BotOptions o=settings.Value;
 public static (int Width,int Height,string Mime,string Extension) Validate(byte[] bytes,long maxBytes,long maxPixels)
 {
  if(bytes.Length==0||bytes.LongLength>maxBytes)throw new UserInputException("Rasm hajmi limitdan oshgan.");
  bool png=bytes.Length>=8&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
  bool jpg=bytes.Length>=3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255;
  if(!png&&!jpg)throw new UserInputException("Faqat haqiqiy PNG/JPG/JPEG rasmlar qabul qilinadi.");
  using var codec=SKCodec.Create(new SKMemoryStream(bytes));
  if(codec==null||codec.Info.Width<1||codec.Info.Height<1||(long)codec.Info.Width*codec.Info.Height>maxPixels)throw new UserInputException("Rasm buzilgan yoki o‘lchami limitdan oshgan.");
  using var bitmap=new SKBitmap(codec.Info.Width,codec.Info.Height,SKColorType.Rgba8888,SKAlphaType.Premul);
  if(codec.GetPixels(bitmap.Info,bitmap.GetPixels())!=SKCodecResult.Success)throw new UserInputException("Rasm fayli to‘liq emas yoki buzilgan.");
  return (codec.Info.Width,codec.Info.Height,png?"image/png":"image/jpeg",png?".png":".jpg");
 }
 public async Task<TaskImage> Add(int user,int taskId,long telegramUserId,string fileId,string name,string? declaredMime,long? length,CancellationToken ct)
 {
  var task=await tasks.Get(user,taskId,ct);
  if(task.Images.Count>=o.MaxImagesPerTask)throw new UserInputException($"Bitta task uchun limit: {o.MaxImagesPerTask} ta rasm.");
  if(declaredMime is not null && declaredMime is not ("image/png" or "image/jpeg"))throw new UserInputException("PNG yoki JPEG rasm yuboring.");
  if(length>o.MaxImageBytes)throw new UserInputException($"Rasm limiti: {o.MaxImageBytes/1024/1024} MB.");
  var file=await bot.GetFile(fileId,ct);
  if(file.FileSize>o.MaxImageBytes)throw new UserInputException("Rasm juda katta.");
  using var ms=new LimitedMemoryStream(o.MaxImageBytes);
  await bot.DownloadFile(file,ms,ct);
  var bytes=ms.ToArray();var info=Validate(bytes,o.MaxImageBytes,o.MaxImagePixels);
  if(declaredMime!=null&&declaredMime!=info.Mime)throw new UserInputException("MIME turi rasm formatiga mos emas.");
  var rel=Path.Combine("images",telegramUserId.ToString(),taskId.ToString(),Guid.NewGuid().ToString("N")+info.Extension);
  var path=files.Resolve(rel);Directory.CreateDirectory(Path.GetDirectoryName(path)!);files.Created(path);
  await File.WriteAllBytesAsync(path,bytes,ct);
  var image=new TaskImage{TaskId=taskId,FilePath=rel,TelegramFileId=fileId,OriginalFileName=Path.GetFileName(name).Replace("\\","").Replace("/","")[..Math.Min(Path.GetFileName(name).Replace("\\","").Replace("/","").Length,150)],MimeType=info.Mime,FileSize=bytes.Length,SortOrder=task.Images.Count==0?0:task.Images.Max(x=>x.SortOrder)+1};
  db.TaskImages.Add(image);await db.SaveChangesAsync(ct);return image;
 }
 public async Task<TaskImage> Get(int user,int imageId,CancellationToken ct) => await db.TaskImages.Include(x=>x.Task).SingleOrDefaultAsync(x=>x.Id==imageId&&x.Task.UserId==user,ct)??throw new UserInputException("Rasm topilmadi yoki sizga tegishli emas.");
 public async Task Delete(int user,int id,CancellationToken ct){var i=await Get(user,id,ct);files.DeleteAfterCommit(i.FilePath);db.TaskImages.Remove(i);await db.SaveChangesAsync(ct);}
 public async Task Move(int user,int id,int delta,CancellationToken ct)
 {
  var i=await Get(user,id,ct);var ordered=await db.TaskImages.Where(x=>x.TaskId==i.TaskId).OrderBy(x=>x.SortOrder).ThenBy(x=>x.Id).ToListAsync(ct);
  var old=ordered.FindIndex(x=>x.Id==id);var target=Math.Clamp(old+delta,0,ordered.Count-1);ordered.RemoveAt(old);ordered.Insert(target,i);
  for(int n=0;n<ordered.Count;n++)ordered[n].SortOrder=n;
  await db.SaveChangesAsync(ct);
 }
 public async Task DeleteTask(int user,int taskId,CancellationToken ct){var t=await tasks.Get(user,taskId,ct);foreach(var i in t.Images)files.DeleteAfterCommit(i.FilePath);await tasks.Delete(user,taskId,ct);}
}
public sealed class LimitedMemoryStream(long limit) : MemoryStream
{
 void Check(int count){if(Position+count>limit)throw new UserInputException("Yuklangan fayl hajmi limitdan oshgan.");}
 public override void Write(byte[] buffer,int offset,int count){Check(count);base.Write(buffer,offset,count);}
 public override void Write(ReadOnlySpan<byte> buffer){Check(buffer.Length);base.Write(buffer);}
 public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken ct=default){Check(buffer.Length);return base.WriteAsync(buffer,ct);}
 public override Task WriteAsync(byte[] buffer,int offset,int count,CancellationToken ct){Check(count);return base.WriteAsync(buffer,offset,count,ct);}
}
