# Botni ishlatish

1. Shu papkadagi **Start.cmd** faylini ikki marta bosing.
2. Ochilgan oynani ochiq qoldiring. Bot shu oyna ishlayotgan paytda xizmat qiladi.
3. Telegramda **@taskreportuz_bot** ni oching va **/start** yuboring.
4. Ism, familiya va lavozimingizni kiriting.
5. **➕ Task qo‘shish** orqali sana, ish matni va ixtiyoriy rasmlarni yuboring, keyin **✅ Saqlash** bosing.
6. **📄 Hisobot yaratish** orqali yil/oy yoki sana oralig‘ini tanlang va **✅ Word yaratish** ni bosing.

Taskni o‘zgartirish/o‘chirish va rasmlarni boshqarish: **📋 Tasklarim**. Amalni bekor qilish: **/cancel**. Yordam: **/help**.

## To‘xtatish va qayta ishga tushirish

Bot oynasida **Ctrl+C** bosing. Qayta ishlatish uchun **Start.cmd** ni yana oching. Bir vaqtning o‘zida bitta nusxa ishlasin. Kompyuter o‘chsa, uyquga ketsa yoki internet uzilsa bot javob bera olmaydi.

## Maxfiylik va saqlash

Token **publish/appsettings.json** ichidagi **Bot → Token** maydonida. Bot tayyor dastur ishga tushganda shu fayldan o‘qiydi. Kodni Visual Studio orqali ishlatish uchun token **src/MonthlyReportBot/appsettings.json** fayliga ham yozilgan. Tokenni almashtirsangiz ikkala nusxani yangilang. Alohida secret ombori yoki token kiritish skripti kerak emas. Bu fayllarni ulashsangiz token ham birga ketadi.

Ishlar va profillar: **publish/storage/bot.db**. Rasmlar: **publish/storage/images**. Loglar: **publish/storage/logs**. **storage papkasini o‘chirmang** — unda kiritgan ma’lumotlaringiz saqlanadi. Yangi hisobotlar faqat tegishli foydalanuvchining shaxsiy Telegram chatiga yuboriladi. Guruhlarda bot ma’lumot ko‘rsatmaydi.

Shaxsiy foydalanish uchun `publish/appsettings.json` ichidagi `Bot.AllowedUserIds` ro‘yxatiga faqat o‘z Telegram IDingizni kiriting. IDni botdagi **/id** buyrug‘i bilan olasiz. Ro‘yxat bo‘sh bo‘lsa boshqa xodimlar ham o‘z alohida profilini ochishi mumkin; ular sizning tasklaringizni ko‘rmaydi. Sozlamani o‘zgartirgach botni qayta ishga tushiring.

## Qaysi fayllar kerak?

- **Start.cmd / Start.ps1** — oddiy ishga tushirish.
- **publish** — tayyor dastur, kutubxonalar, Word shabloni va ishchi ma’lumotlar. Ichidagi DLL fayllarni o‘chirmang.
- **src / MonthlyReportBot.sln / global.json** — dastur manba kodi va Visual Studio loyihasi; keyin kodni o‘zgartirish uchun qoldirilgan. Test loyihasi yo‘q.
- **src/MonthlyReportBot/Data/Migrations** — birinchi ishga tushishda bazani yaratish uchun kerak; test fayllari emas.
- **ThirdPartyLicenses** — ishlatilgan kutubxonalarning litsenziya matnlari.
- **Backup.ps1** — botni to‘xtatgandan keyin ma’lumotlarni zaxiralash yordamchisi.

ZIP va `.sha256` ishlash uchun kerak emas. `.sha256` faqat ZIP arxivi buzilmaganini tekshirishga xizmat qilgan.

## Kodni o‘zgartirgandan keyin

Avval botni to‘xtating. Solutionni Visual Studio 2022 orqali ochish mumkin. PowerShellda shu papkadan tayyor buildni yangilash:

```powershell
dotnet publish src/MonthlyReportBot/MonthlyReportBot.csproj -c Release -r win-x64 --self-contained false -o publish
```

`publish/storage` saqlanib qoladi; yangilashdan oldin zaxira oling. Bazaga migratsiyalar dastur ishga tushganda avtomatik qo‘llanadi. `Start.ps1 -Check` token va Telegram ulanishini tekshiradi. Alohida test va EF CLI vositalari oddiy ishlatish uchun talab qilinmaydi.

Word shablon tuzilishi tekshirilgan. Bu muhitda sahifa render vositasi mavjud bo‘lmagani uchun hujjatning chop etish ko‘rinishi vizual tasdiqlanmagan.
