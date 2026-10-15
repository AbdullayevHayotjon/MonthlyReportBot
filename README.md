# MonthlyReportBot

Telegram orqali task, rasm va oylik Word hisobotlari. .NET 8 Worker Service, EF Core SQLite va Open XML SDK. Foydalanuvchilar Telegram ID bo‘yicha ajratiladi; bot faqat shaxsiy chatda ishlaydi.

## Server

Ubuntu 24.04 x64 uchun to‘liq o‘rnatish, CI/CD, ma’lumotlarni ko‘chirish va rollback tartibi: [DEPLOYMENT.md](DEPLOYMENT.md).

GitHub Actions har bir push/PRda tokensiz build va native SQLite/PNG/Word tekshiruvlarini bajaradi. `master`/`main` yangilanishi serverga SSH orqali deploy qilinadi (`DEPLOY_ENABLED=true` bo‘lganda). Baza va rasmlar release’dan alohida saqlanadi. Telegram uchun kiruvchi port va domen talab qilinmaydi.

## Mahalliy ishlatish

`src/MonthlyReportBot/appsettings.json` public sozlamalar shabloni: haqiqiy token repositoryga yozilmaydi. Windowsda avval tayyorlangan `publish/appsettings.json` Gitdan tashqarida turadi. Yangi mahalliy build uchun `src/MonthlyReportBot/appsettings.Local.json` yarating:

```json
{
  "Bot": {
    "Token": ""
  }
}
```

Tokenni faqat shu Gitga kirmaydigan faylga kiriting yoki `Bot__Token` muhit o‘zgaruvchisidan foydalaning. Serverda token `/etc/monthlyreportbot/bot.env` orqali beriladi. Sozlamalar ustuvorligi: `appsettings.json` → `appsettings.Local.json` → environment variables. `user-secrets` talab qilinmaydi.

```powershell
dotnet run --project src/MonthlyReportBot
```

Yoki mavjud Windows build uchun `Start.cmd`. Oyna ochiq turishi kerak. To‘xtatish: Ctrl+C. Bir token bilan mahalliy va server botini bir vaqtda ishlatmang.

## Telegram menyusi

`/start`: profil. **Task qo‘shish**: sana → matn → ixtiyoriy rasmlar → Saqlash. **Tasklarim**: ko‘rish/tahrirlash/o‘chirish va rasmlar tartibi. **Hisobot yaratish**: yil/oy yoki inclusive sana oralig‘i → tasdiq → Word. `/cancel`, `/menu`, `/help`, `/tasks`, `/report`, `/id` mavjud.

`Positions` faqat konfiguratsiyadan olinadi. Rasm photo bo‘lsa eng katta variant, document bo‘lsa original PNG/JPEG olinadi. Profil, task, rasm yo‘llari va hisobot davrlari SQLite’da, haqiqiy rasmlar diskda. Tayyor DOCX xotirada yaratiladi va Telegramga yuboriladi.

## Ma’lumotlar

Windows tayyor build: `publish/storage`. Ubuntu: `/var/lib/monthlyreportbot`. SQLite bazasi bilan birga rasmlarni ham saqlang. Ishlayotgan bazaning `-wal` / `-shm` fayllarini o‘chirmang. Zaxira/transferdan oldin aynan shu botni to‘xtating.

Word shabloni `Templates/MonthlyReportTemplate.docx`. Asl namuna kontenti tozalangan nusxa ishlatiladi; numbering, jadval, font va sahifa parametrlari saqlanadi. Har bir natija Open XML validatsiyasidan o‘tadi. Sahifalarning Word/LibreOffice render ko‘rinishi ushbu Windows muhitida vizual tasdiqlanmagan.
