using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MonthlyReportBot.Configuration;
using MonthlyReportBot.Data;
using MonthlyReportBot.Data.Entities;
using MonthlyReportBot.Keyboards;
using MonthlyReportBot.Models;
using MonthlyReportBot.Services.Users;
using MonthlyReportBot.Services.Tasks;
using MonthlyReportBot.Services.Images;
using MonthlyReportBot.Services.Reports;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
namespace MonthlyReportBot.Handlers;
public sealed class BotHandler(ITelegramBotClient bot,AppDbContext db,UserService users,TaskService tasks,ImageService images,FileChanges files,ReportService reports,IOptions<BotOptions> options)
{
 readonly BotOptions o=options.Value;
 Employee u=null!;UserSession session=null!;Dialog d=new();long chat;CancellationToken ct;
 Task Say(string text,ReplyMarkup? keyboard=null)=>bot.SendMessage(chat,text,replyMarkup:keyboard,cancellationToken:ct);
 async Task State(string state){session.CurrentState=state;session.StateDataJson=JsonSerializer.Serialize(d);session.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);}
 async Task Clear()
 {
  if(d.TaskId is int id){var t=await tasks.Owned(u.Id).SingleOrDefaultAsync(x=>x.Id==id,ct);if(t?.IsDraft==true)await images.DeleteTask(u.Id,id,ct);}
  d=new();await State("Menu");
 }
 public async Task Handle(Update update,CancellationToken token)
 {
  ct=token;var msg=update.Message??update.CallbackQuery?.Message;var sender=update.Message?.From??update.CallbackQuery?.From;
  // Private chats only: never send profiles, tasks or documents to groups or channels.
  if(msg==null||sender==null||sender.IsBot||msg.Chat.Type!=ChatType.Private||msg.Chat.Id!=sender.Id)return;
  chat=msg.Chat.Id;
  if(o.AllowedUserIds.Length>0&&!o.AllowedUserIds.Contains(sender.Id)){if(update.Message!=null)await Say("Botdan foydalanish uchun ruxsat berilmagan.");return;}
  u=await users.GetOrCreate(sender.Id,sender.Username,ct);
  if(!u.IsActive)return;
  session=await db.UserSessions.SingleOrDefaultAsync(x=>x.UserId==u.Id,ct)??new UserSession{UserId=u.Id};
  if(session.Id==0){db.UserSessions.Add(session);await db.SaveChangesAsync(ct);}
  d=JsonSerializer.Deserialize<Dialog>(session.StateDataJson)??new();
  try
  {
   if(update.CallbackQuery is { } cb)
   {
    try{await bot.AnswerCallbackQuery(cb.Id,cancellationToken:ct);}catch(Telegram.Bot.Exceptions.ApiRequestException){}
    if(string.IsNullOrEmpty(u.PositionType)&&!(cb.Data??"").StartsWith("pos:")){await Say("Avval /start orqali profil yarating.");return;}
    await Callback(cb.Data??"");return;
   }
   var text=msg.Text?.Trim()??"";
   if(text is "/cancel" or "/menu" or Menus.Cancel){await Clear();await Say("Amal bekor qilindi.",Menus.Main);return;}
   if(text is "/help" or Menus.Help){await Help();return;}
   if(text=="/id"){await Say($"Telegram ID: {sender.Id}");return;}
   if(text=="/start"||string.IsNullOrEmpty(u.PositionType)&&session.CurrentState=="Menu")
   {await Clear();if(string.IsNullOrEmpty(u.PositionType)){await State("FirstName");await Say("Xush kelibsiz! Ismingizni kiriting:",Menus.Stop);}else await Say($"Salom, {u.FirstName}!",Menus.Main);return;}
   if(text==Menus.Back){await Back();return;}
   if(text==Menus.Add){await Clear();await State("TaskDate");await Say("Ish sanasini dd.MM.yyyy shaklida kiriting yoki bugunni tanlang.",Menus.Reply([Menus.Today],[Menus.Back,Menus.Cancel]));return;}
   if(text is Menus.Report or "/report"){await Clear();d.Mode="report";await ChoosePeriod();return;}
   if(text is Menus.Tasks or "/tasks"){await Clear();d.Mode="tasks";await ChoosePeriod();return;}
   if(text==Menus.Profile){await Clear();await Say(string.IsNullOrEmpty(u.PositionType)?"Profil hali tugallanmagan.":$"{u.FullName}\n{u.PositionType}",Menus.Inline(("✏️ Profilni o‘zgartirish","profile")));return;}
   if(text==Menus.Settings){await Say($"Vaqt mintaqasi: {o.DefaultTimeZone}\nSana: dd.MM.yyyy\nBir task: {o.MaxImagesPerTask} tagacha rasm\nRasm: {o.MaxImageBytes/1024/1024} MB\nHisobot: {o.MaxReportImages} rasm, {o.MaxReportTasks} task\nTelegram ID: {sender.Id}\nMa’lumotlar faqat shaxsiy chatda ko‘rsatiladi.",Menus.Main);return;}
   switch(session.CurrentState)
   {
    case "FirstName":d.FirstName=UserService.Name(text);await State("LastName");await Say("Familiyangizni kiriting:",Menus.Stop);break;
    case "LastName":d.LastName=UserService.Name(text);await State("Position");await Say("Lavozimni tanlang:",new InlineKeyboardMarkup(o.Positions.Select((p,i)=>new[]{InlineKeyboardButton.WithCallbackData(p,$"pos:{i}")})));break;
    case "Position":await Say("Yuqoridagi lavozim tugmasini bosing.");break;
    case "TaskDate":d.Date=text==Menus.Today?o.Today:Dates.Parse(text);await State("TaskText");await Say("Bajarilgan ish matnini yuboring:",Menus.Stop);break;
    case "TaskText":var added=await tasks.Add(u.Id,d.Date!.Value,text,true,ct);d.TaskId=added.Id;await State("TaskImages");await Say("Rasmlarni photo yoki PNG/JPEG document sifatida yuboring. Bir nechta rasmli albom ham mumkin. Rasmsiz saqlash ham mumkin.",Menus.Images);break;
    case "TaskImages":case "MoreImages":
     if(text is Menus.Save or Menus.Done){var t=await tasks.Get(u.Id,d.TaskId!.Value,ct);t.IsDraft=false;t.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);var tid=t.Id;d.TaskId=null;await State("Menu");await Say("✅ Saqlandi.",Menus.Main);await Detail(tid);}
     else await Upload(msg);break;
    case "EditText":await tasks.Edit(u.Id,d.TaskId!.Value,text:text,ct:ct);await State("Menu");await Detail(d.TaskId.Value);break;
    case "EditDate":await tasks.Edit(u.Id,d.TaskId!.Value,date:Dates.Parse(text),ct:ct);await State("Menu");await Detail(d.TaskId.Value);break;
    case "Year":if(!int.TryParse(text,out int year)||year is <2000 or >2100)throw new UserInputException("2000–2100 oralig‘ida yil kiriting.");d.Year=year;await Months();break;
    case "StartDate":d.Start=Dates.Parse(text);await State("EndDate");await Say("Tugash sanasini dd.MM.yyyy shaklida kiriting:",Menus.Stop);break;
    case "EndDate":var end=Dates.Parse(text);Dates.Validate(d.Start!.Value,end);d.End=end;await PeriodReady();break;
    case "ImageOrder":
     var numbers=text.Split(' ',StringSplitOptions.RemoveEmptyEntries);
     if(numbers.Length!=2||!int.TryParse(numbers[0],out var imageId)||!int.TryParse(numbers[1],out var position))throw new UserInputException("Rasm ID va yangi o‘rnini kiriting: 15 2");
     var image=await images.Get(u.Id,imageId,ct);if(image.TaskId!=d.TaskId)throw new UserInputException("Rasm ushbu taskga tegishli emas.");
     var list=(await tasks.Get(u.Id,d.TaskId!.Value,ct)).Images.OrderBy(x=>x.SortOrder).ThenBy(x=>x.Id).ToList();if(position<1||position>list.Count)throw new UserInputException("O‘rin noto‘g‘ri.");
     await images.Move(u.Id,imageId,position-1-list.FindIndex(x=>x.Id==imageId),ct);await State("Menu");await ImageList(d.TaskId.Value,0);break;
    default:await Say("Menyudan amal tanlang. /cancel bilan joriy amal bekor qilinadi.",Menus.Main);break;
   }
  }
  catch(UserInputException e){await Say(e.Message+"\nQayta urinib ko‘ring yoki /cancel bosing.");}
 }
 async Task Upload(Message msg)
 {
  string fileId,name;string? mime=null;long? size;
  if(msg.Photo is {Length:>0} photos){var p=photos.OrderByDescending(x=>(long)x.Width*x.Height).First();fileId=p.FileId;name="photo.jpg";size=p.FileSize;}
  else if(msg.Document is { } doc){fileId=doc.FileId;name=doc.FileName??"image";mime=doc.MimeType;size=doc.FileSize;var ext=Path.GetExtension(name).ToLowerInvariant();if(ext is not (".png" or ".jpg" or ".jpeg"))throw new UserInputException("PNG/JPG/JPEG document yuboring.");if(mime is not ("image/png" or "image/jpeg"))throw new UserInputException("Document MIME turi PNG/JPEG bo‘lishi kerak.");}
  else throw new UserInputException("Rasm yuboring yoki ✅ Saqlash tugmasini bosing.");
  var img=await images.Add(u.Id,d.TaskId!.Value,u.TelegramUserId,fileId,name,mime,size,ct);
  await Say($"Rasm qo‘shildi (ID: {img.Id}).",Menus.Inline(("Rasmlarni boshqarish",$"imgs:{d.TaskId}:0")));
 }
 async Task ChoosePeriod(){await State("PeriodType");await Say(d.Mode=="report"?"Hisobot davrini tanlang:":"Tasklarni ko‘rish davrini tanlang:",Menus.Inline(("📆 Oylik","period:month"),("📅 Sana oralig‘i","period:range"),("📜 Oldingi hisobotlar","history:0"),("❌ Bekor qilish","cancel")));}
 async Task Months(){await State("Month");await Say($"{d.Year}-yil uchun oy tanlang:",new InlineKeyboardMarkup(Enumerable.Range(1,12).Chunk(3).Select(row=>row.Select(m=>InlineKeyboardButton.WithCallbackData(Dates.Months[m-1],$"month:{m}")))));}
 async Task PeriodReady()
 {
  Dates.Validate(d.Start!.Value,d.End!.Value);d.Page=0;
  if(d.Mode=="tasks"){await State("TaskList");await TaskList(0);return;}
  var q=tasks.PeriodQuery(u.Id,d.Start.Value,d.End.Value);var count=await q.CountAsync(ct);var pictureCount=await q.SelectMany(t=>t.Images).CountAsync(ct);
  if(count==0){await State("Menu");await Say("Tanlangan davrda task topilmadi.",Menus.Main);return;}
  await State("ReportConfirm");await Say($"📄 Hisobot tayyorlash\nXodim: {u.FullName}\nLavozim: {u.PositionType}\nDavr: {d.Start:dd.MM.yyyy} – {d.End:dd.MM.yyyy}\nBajarilgan ishlar: {count} ta\nBiriktirilgan rasmlar: {pictureCount} ta",Menus.Inline(("✅ Word yaratish","generate"),("❌ Bekor qilish","cancel")));
 }
 async Task TaskList(int page)
 {
  if(d.Start==null||d.End==null)throw new UserInputException("Avval /tasks bilan davrni tanlang.");
  var q=tasks.PeriodQuery(u.Id,d.Start.Value,d.End.Value);var count=await q.CountAsync(ct);var pages=Math.Max(1,(count+4)/5);page=Math.Clamp(page,0,pages-1);d.Page=page;await State("TaskList");
  var list=await q.Skip(page*5).Take(5).ToListAsync(ct);
  var buttons=list.Select(t=>($"{t.WorkDate:dd.MM}: {t.Description.Truncate(45)}",$"task:{t.Id}")).ToList();
  if(page>0)buttons.Add(("⬅️ Oldingi",$"list:{page-1}"));if(page+1<pages)buttons.Add(("Keyingi ➡️",$"list:{page+1}"));buttons.Add(("Menyu","cancel"));
  await Say($"📋 Tasklar: {count} ta. Sahifa {page+1}/{pages}",Menus.Inline(buttons.ToArray()));
 }
 async Task Detail(int id)
 {
  var t=await tasks.Get(u.Id,id,ct);
  foreach(var chunk in t.Description.Chunk(3500))await Say(new string(chunk));
  await Say($"Task #{t.Id}\nSana: {t.WorkDate:dd.MM.yyyy}\nRasmlar: {t.Images.Count} ta{(t.IsDraft?"\nQoralama — ✅ Saqlash bilan yakunlang.":"")}",Menus.Inline(("✏️ Matnni o‘zgartirish",$"text:{id}"),("📅 Sanani o‘zgartirish",$"date:{id}"),("🖼 Rasmlar",$"imgs:{id}:0"),("➕ Rasm qo‘shish",$"addimg:{id}"),("🗑 Taskni o‘chirish",$"delask:{id}"),("⬅️ Ro‘yxat","list:0")));
 }
 async Task ImageList(int taskId,int page)
 {
  var t=await tasks.Get(u.Id,taskId,ct);var all=t.Images.OrderBy(x=>x.SortOrder).ThenBy(x=>x.Id).ToList();page=Math.Clamp(page,0,Math.Max(0,(all.Count-1)/5));
  var buttons=all.Skip(page*5).Take(5).SelectMany((i,n)=>new[]{($"🖼 {page*5+n+1}. Rasm #{i.Id}",$"view:{i.Id}"),($"🗑 Rasm #{i.Id}",$"imgask:{i.Id}"),($"⬆️ #{i.Id}",$"up:{i.Id}"),($"⬇️ #{i.Id}",$"down:{i.Id}")}).ToList();
  if(page>0)buttons.Add(("⬅️",$"imgs:{taskId}:{page-1}"));if((page+1)*5<all.Count)buttons.Add(("➡️",$"imgs:{taskId}:{page+1}"));
  buttons.Add(("🔢 Aniq tartib belgilash",$"order:{taskId}"));buttons.Add(("➕ Yana rasm",$"addimg:{taskId}"));buttons.Add(("✅ Tayyor",$"task:{taskId}"));
  await Say($"Rasmlar: {all.Count} ta. Rasmni ko‘rish yoki tartibini o‘zgartirish mumkin.",Menus.Inline(buttons.ToArray()));
 }
 async Task History(int page)
 {
  var q=db.ReportHistory.Where(h=>h.UserId==u.Id).OrderByDescending(h=>h.CreatedAt).ThenByDescending(h=>h.Id);page=Math.Clamp(page,0,100000);
  var list=await q.Skip(page*5).Take(6).ToListAsync(ct);var b=list.Take(5).Select(h=>($"{h.StartDate:dd.MM.yyyy} – {h.EndDate:dd.MM.yyyy}",$"regen:{h.Id}")).ToList();if(page>0)b.Add(("⬅️",$"history:{page-1}"));if(list.Count>5)b.Add(("➡️",$"history:{page+1}"));b.Add(("Menyu","cancel"));await Say("Oldingi hisobotlar (joriy task va rasmlar asosida qayta yaratiladi):",Menus.Inline(b.ToArray()));
 }
 static int Id(string value)=>int.TryParse(value,out var n)&&n>=0?n:throw new UserInputException("Tugma ma’lumoti noto‘g‘ri.");
 async Task Callback(string data)
 {
  if(data.Length>64)throw new UserInputException("Noto‘g‘ri tugma.");var p=data.Split(':');var action=p[0];
  if(action=="period")
  {
   if(p.Length!=2||p[1] is not ("month" or "range"))throw new UserInputException("Davr turi noto‘g‘ri.");
   if(session.CurrentState!="PeriodType")throw new UserInputException("Avval /report yoki /tasks orqali davrni tanlang.");
   d.ReportType=p[1];
   if(p[1]=="month"){await State("Year");await Say($"Yilni kiriting (masalan, {o.Today.Year}):",Menus.Stop);}
   else{await State("StartDate");await Say("Boshlanish sanasini dd.MM.yyyy shaklida kiriting:",Menus.Stop);}
   return;
  }
  int id=p.Length>1?Id(p[1]):0;
  switch(action)
  {
   case "cancel":await Clear();await Say("Asosiy menyu",Menus.Main);break;
   case "profile":await Clear();await State("FirstName");await Say("Yangi ismingizni kiriting:",Menus.Stop);break;
   case "pos":
    if(session.CurrentState!="Position"||d.FirstName==null||d.LastName==null||id>=o.Positions.Length)throw new UserInputException("Avval profil ma’lumotlarini kiriting.");
    await users.Save(u,d.FirstName,d.LastName,o.Positions[id],ct);await Clear();await Say("Profil saqlandi.",Menus.Main);break;
   case "month":
    if(session.CurrentState!="Month")throw new UserInputException("Avval davrni tanlang.");
    (d.Start,d.End)=Dates.Month(d.Year,id);d.ReportType="month";await PeriodReady();break;
   case "generate":
    if(session.CurrentState!="ReportConfirm"||d.Start==null||d.End==null)throw new UserInputException("Avval hisobot davrini tanlang.");
    await Say("Word hisoboti tayyorlanmoqda…");var result=await reports.Create(u,d.Start.Value,d.End.Value,d.ReportType=="month",ct);
    using(var stream=new MemoryStream(result.Bytes))await bot.SendDocument(chat,InputFile.FromStream(stream,result.FileName),protectContent:true,cancellationToken:ct);
    await reports.Record(u.Id,d.Start.Value,d.End.Value,d.ReportType=="month",result.FileName,ct);await State("Menu");await Say("✅ Hisobot tayyor.",Menus.Main);break;
   case "list":await TaskList(id);break;
   case "task":await Detail(id);break;
   case "text":case "date":case "addimg":case "order":
    var edit=await tasks.Get(u.Id,id,ct);if(d.TaskId!=id)await Clear();d.TaskId=id;
    await State(action=="text"?"EditText":action=="date"?"EditDate":action=="order"?"ImageOrder":edit.IsDraft?"TaskImages":"MoreImages");
    await Say(action=="text"?"Yangi task matnini kiriting:":action=="date"?"Yangi sanani dd.MM.yyyy shaklida kiriting:":action=="order"?"Rasm ID va yangi o‘rnini yuboring. Masalan: 15 2":"PNG/JPG/JPEG rasmlar yuboring, so‘ng ✅ Saqlash bosing.",action=="addimg"?Menus.Images:Menus.Stop);break;
   case "imgs":if(p.Length!=3)throw new UserInputException("Tugma noto‘g‘ri.");await ImageList(id,Id(p[2]));break;
   case "view":
    var image=await images.Get(u.Id,id,ct);await using(var file=File.OpenRead(files.Resolve(image.FilePath)))await bot.SendDocument(chat,InputFile.FromStream(file,$"Rasm_{id}{(image.MimeType=="image/png"?".png":".jpg")}"),protectContent:true,cancellationToken:ct);break;
   case "up":case "down":var moved=await images.Get(u.Id,id,ct);await images.Move(u.Id,id,action=="up"?-1:1,ct);await ImageList(moved.TaskId,0);break;
   case "imgask":var ask=await images.Get(u.Id,id,ct);await Say($"Rasm #{id} o‘chirilsinmi?",Menus.Inline(("Ha, o‘chirish",$"imgdelete:{id}"),("Yo‘q",$"imgs:{ask.TaskId}:0")));break;
   case "imgdelete":var removed=await images.Get(u.Id,id,ct);await images.Delete(u.Id,id,ct);await ImageList(removed.TaskId,0);break;
   case "delask":await tasks.Get(u.Id,id,ct);await Say($"Task #{id} va uning rasmlari o‘chirilsinmi?",Menus.Inline(("Ha, o‘chirish",$"delete:{id}"),("Yo‘q",$"task:{id}")));break;
   case "delete":await images.DeleteTask(u.Id,id,ct);if(d.TaskId==id)d.TaskId=null;await State("Menu");await Say("Task o‘chirildi.",Menus.Main);break;
   case "history":await History(id);break;
   case "regen":var h=await db.ReportHistory.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==u.Id,ct)??throw new UserInputException("Hisobot topilmadi.");await Clear();d.Start=h.StartDate;d.End=h.EndDate;d.ReportType=h.ReportType;d.Mode="report";await PeriodReady();break;
   default:throw new UserInputException("Tugma eskirgan. /menu bosing.");
  }
 }
 async Task Back()
 {
  switch(session.CurrentState)
  {
   case "LastName":await State("FirstName");await Say("Ismingizni kiriting:",Menus.Stop);break;
   case "Position":await State("LastName");await Say("Familiyangizni kiriting:",Menus.Stop);break;
   case "TaskText":await State("TaskDate");await Say("Sanani kiriting:",Menus.Reply([Menus.Today],[Menus.Back,Menus.Cancel]));break;
   case "TaskImages":if(d.TaskId is int id){await images.DeleteTask(u.Id,id,ct);d.TaskId=null;}await State("TaskText");await Say("Task matnini qayta kiriting:",Menus.Stop);break;
   case "EndDate":await State("StartDate");await Say("Boshlanish sanasini kiriting:",Menus.Stop);break;
   case "Month":await State("Year");await Say("Yilni kiriting:",Menus.Stop);break;
   case "ReportConfirm":case "Year":case "StartDate":await ChoosePeriod();break;
   default:await Clear();await Say("Asosiy menyu",Menus.Main);break;
  }
 }
 Task Help()=>Say("➕ Task qo‘shish: sana → ish matni → ixtiyoriy rasmlar → ✅ Saqlash.\n📋 Tasklarim: oylik yoki sana oralig‘ida ko‘rish, tahrirlash va tasdiqlab o‘chirish.\n📄 Hisobot yaratish: davrni tanlash → task/rasm sonini tekshirish → Word yaratish.\nBir albomdagi rasmlarni yuborib bo‘lgach ✅ Saqlash bosing. Har bir rasm bir xil taskga ulanadi.\nSana: dd.MM.yyyy. Chegara sanalari ham hisobotga kiradi.\n/cancel — bekor qilish\n/menu — menyu\n/report — hisobot\n/tasks — tasklar\n/id — Telegram ID\nShaxsiy ma’lumotlarni faqat o‘zingizning hisobotingizga kiriting. Xizmat Telegram orqali fayl yuboradi; serverdagi baza va storage mahalliy saqlanadi.",Menus.Main);
}
