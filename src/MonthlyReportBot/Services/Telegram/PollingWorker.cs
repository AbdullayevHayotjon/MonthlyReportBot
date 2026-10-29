using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using MonthlyReportBot.Configuration;
using MonthlyReportBot.Data;
using MonthlyReportBot.Data.Entities;
using MonthlyReportBot.Handlers;
using MonthlyReportBot.Helpers;
using MonthlyReportBot.Services.Images;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
namespace MonthlyReportBot.Services.Telegram;
public sealed class PollingWorker(IServiceScopeFactory scopes,ITelegramBotClient bot,ILogger<PollingWorker> log,IHostApplicationLifetime lifetime,WorkerHealth health) : BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken ct)
 {
  int offset;
  using(var scope=scopes.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();offset=(await db.PollCursors.SingleAsync(ct)).NextUpdateId;}
  var delay=2;
  bool connected=false;
  health.Clear();
  log.LogInformation("Long polling ishga tushdi.");
  while(!ct.IsCancellationRequested)
  {
   var stage="connect";
   int? updateId=null;
   var elapsed=Stopwatch.StartNew();
   try
   {
    if(!connected)
    {
     await bot.GetMe(ct);
     var webhook=await bot.GetWebhookInfo(ct);
     if(!string.IsNullOrEmpty(webhook.Url))
     {log.LogCritical("Webhook sozlangan. Long polling uchun uni alohida olib tashlang.");Environment.ExitCode=1;lifetime.StopApplication();return;}
     connected=true;health.Touch();
    }
    stage="getUpdates";
    // Bound each long poll separately; document uploads keep their existing timeout.
    using var pollTimeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
    pollTimeout.CancelAfter(TimeSpan.FromSeconds(45));
    var updates=await bot.GetUpdates(offset,timeout:30,allowedUpdates:[UpdateType.Message,UpdateType.CallbackQuery],cancellationToken:pollTimeout.Token);
    health.Touch();
    foreach(var update in updates.OrderBy(x=>x.Id))
    {
     // A global ordered queue also serializes each user's messages and album items.
     // The cursor and domain changes commit atomically, so redeliveries do not duplicate records.
     stage="processUpdate";updateId=update.Id;elapsed.Restart();
     await ProcessUpdate(update,ct);offset=update.Id+1;health.Touch();
    }
    delay=2;
   }
   catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
   catch(ApiRequestException e)when(e.ErrorCode is 401 or 409)
   {log.LogCritical("Telegram ruxsati yoki polling to‘qnashuvi. Kod: {Code}. Token va boshqa bot nusxasini tekshiring.",e.ErrorCode);Environment.ExitCode=1;lifetime.StopApplication();return;}
   catch(ApiRequestException e)when(e.ErrorCode==429)
   {log.LogWarning("Telegram rate limit; stage: {Stage}; retry: {Retry}s",stage,e.Parameters?.RetryAfter??10);await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(e.Parameters?.RetryAfter??10,1,60)),ct);}
   catch(Exception e)
   {log.LogWarning("Polling qayta urinadi. Stage: {Stage}; update: {Update}; elapsed: {Elapsed}s; cause: {Cause}; kutish: {Delay}s",stage,updateId,Math.Round(elapsed.Elapsed.TotalSeconds,1),FailureCode(e),delay);await Task.Delay(TimeSpan.FromSeconds(delay),ct);delay=Math.Min(delay*2,60);}
  }
 }
 // Log codes/types only: exception messages and URLs may contain the bot token.
 static string FailureCode(Exception error)
 {
  var codes=new List<string>();
  for(Exception? e=error;e!=null;e=e.InnerException)
   codes.Add(e switch
   {
    ApiRequestException api=>$"Telegram:{api.ErrorCode}",
    RequestException request=>$"Request:{request.HttpStatusCode}",
    SocketException socket=>$"Socket:{socket.SocketErrorCode}",
    HttpRequestException http=>$"Http:{http.StatusCode}",
    OperationCanceledException=>"CancelledOrTimeout",
    _=>e.GetType().Name
   });
  return string.Join(" > ",codes);
 }
 public override async Task StopAsync(CancellationToken ct){await base.StopAsync(ct);health.Clear();}
 async Task ProcessUpdate(Update update,CancellationToken ct)
 {
  try
  {
   using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var files=scope.ServiceProvider.GetRequiredService<FileChanges>();
   await using var transaction=await db.Database.BeginTransactionAsync(ct);var cursor=await db.PollCursors.SingleAsync(ct);
   if(update.Id<cursor.NextUpdateId)return;
   await scope.ServiceProvider.GetRequiredService<BotHandler>().Handle(update,ct);
   cursor.NextUpdateId=update.Id+1;await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);files.Commit();
  }
  catch(ApiRequestException e)when(e.ErrorCode is 400 or 403)
  {
   // Telegram rejected this update permanently (blocked bot, expired callback, etc.).
   log.LogWarning("Telegram xabarni rad etdi. Kod: {Code}; update: {Id}",e.ErrorCode,update.Id);
   await Skip(update.Id,ct);
  }
  catch(Exception e)when(e is InvalidDataException or FileNotFoundException or DirectoryNotFoundException)
  {
   log.LogError("Hisobot/storage xatosi. Tur: {Type}; update: {Id}",e.GetType().Name,update.Id);
   var m=update.Message??update.CallbackQuery?.Message;
   if(m?.Chat.Type==ChatType.Private)try{await bot.SendMessage(m.Chat.Id,"Fayl yoki shablon bilan xato yuz berdi. /cancel bosing va administratorga xabar bering.",cancellationToken:ct);}catch(ApiRequestException){}
   await Skip(update.Id,ct);
  }
 }
 async Task Skip(int id,CancellationToken ct)
 {using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var cursor=await db.PollCursors.SingleAsync(ct);cursor.NextUpdateId=Math.Max(cursor.NextUpdateId,id+1);await db.SaveChangesAsync(ct);}
}
