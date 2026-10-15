using Telegram.Bot.Types.ReplyMarkups;
namespace MonthlyReportBot.Keyboards;
public static class Menus
{
 public const string Add="➕ Task qo‘shish", Tasks="📋 Tasklarim", Report="📄 Hisobot yaratish", Profile="👤 Profilim", Settings="⚙️ Sozlamalar", Help="❓ Yordam", Cancel="❌ Bekor qilish", Save="✅ Saqlash", Done="✅ Tayyor", Today="📅 Bugun", Back="⬅️ Orqaga";
 public static ReplyKeyboardMarkup Reply(params string[][] rows)=>new(rows.Select(r=>r.Select(t=>new KeyboardButton(t)))){ResizeKeyboard=true};
 public static ReplyKeyboardMarkup Main=>Reply([Add,Tasks],[Report,Profile],[Settings,Help]);
 public static ReplyKeyboardMarkup Stop=>Reply([Back,Cancel]);
 public static ReplyKeyboardMarkup Images=>Reply([Save],[Back,Cancel]);
 public static InlineKeyboardMarkup Inline(params (string Text,string Data)[] buttons)=>new(buttons.Select(b=>new[]{InlineKeyboardButton.WithCallbackData(b.Text,b.Data)}));
}
