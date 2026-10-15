# Ubuntu 24.04 ga joylashtirish

Bu bot alohida systemd xizmati sifatida ishlaydi. Nginx, frontend/backend containerlari, mavjud portlar va domenlarga tegmaydi. Kiruvchi port kerak emas: Telegram bilan outbound HTTPS long polling ishlatiladi. O‘rnatish skripti server paketlarini yangilamaydi va reboot qilmaydi; zarur kutubxona yo‘q bo‘lsa o‘rnatishni to‘xtatadi.

## Joylashuv va hisoblar

| Joy | Maqsad |
|---|---|
| `/opt/monthlyreportbot/releases/<commit>` | Har bir versiyaning o‘zgarmas self-contained Linux x64 buildi |
| `/opt/monthlyreportbot/current` | Ishlayotgan versiyaga symlink |
| `/var/lib/monthlyreportbot` | SQLite, rasmlar va loglar; deploy bu papkani almashtirmaydi |
| `/etc/monthlyreportbot/bot.env` | `Bot__Token` va zarur server sozlamalari, root-only |
| `/var/backups/monthlyreportbot` | Yangilashdan oldingi sovuq zaxiralar, root-only |
| `/run/monthlyreportbot/healthy` | Telegramga ulanishdan keyingi worker heartbeat |
| `monthlyreportbot.service` | Faqat ushbu bot xizmati |

Runtime `monthlyreportbot` tizim foydalanuvchisida ishlaydi. CI SSH kaliti faqat `upload`, `deploy`, `status` buyruqlarini bajaradi. Mavjud SSH parol va boshqa kalitlar o‘zgarmaydi. Deploy foydalanuvchisiga faqat shu xizmatni stop/start qilish, schema holatini olish va shu botni zaxiralash uchun sudo ruxsatlari yoziladi.

## Bir martalik o‘rnatish

`deploy/linux` fayllari, CI ochiq kaliti (`ci-key.pub`) va ixtiyoriy shaxsiy `storage-transfer.tar.gz` serverdagi alohida setup papkasiga yuboriladi. Private kalit va foydalanuvchi ma’lumotlari GitHub repositoryga kiritilmaydi. Serverda:

```bash
cd ~/monthlyreportbot-setup
sudo bash bootstrap.sh deploy
```

Avval sudo paroli, keyin yangi Telegram bot tokeni so‘raladi. Token ekranda ko‘rinmaydi. Oldin publicga chiqqan token BotFather orqali bekor qilingan bo‘lishi kerak. Tokenni repositorydagi appsettings fayliga qayta yozmang. Installer xizmatni enable qiladi, lekin dastlabki release bo‘lmaguncha ishga tushirmaydi.

Mahalliy tasklarni ko‘chirish uchun Windowsdagi shu botni to‘xtatib, SQLite backup API orqali izchil nusxa olinadi; rasmlar bilan birga SSH orqali yuboriladi. Import mavjud server bazasi ustiga yozmaydi. Windowsdagi `\` rasm yo‘llari Linux uchun `/` ga aylantiriladi, FK/integrity va rasm hajmlari tekshiriladi. Windowsdagi asl baza va rasmlar o‘zgarmaydi. Eski va yangi botni bir token bilan bir vaqtda polling qilmang.

## GitHub Actions

Workflow: `.github/workflows/ci-cd.yml`. Push/PR paytida credential scan, shell/Python sintaksisi, Linux x64 publish, SQLite migratsiyasi va haqiqiy PNG/DOCX generatsiyasi offline tekshiriladi. Alohida test solution loyihasi yo‘q. Buildda bot tokeni ishlatilmaydi. Artefakt faqat dastur va litsenziyalarni saqlaydi; baza, rasmlar va local config kirmaydi.

Repository Actions **Variables**:

- `SSH_HOST` — server IP yoki hostname.
- `SSH_USER` — deploy hisob nomi.
- `SSH_PORT` — odatda `22`.
- `DEPLOY_ENABLED` — dastlab `false`; birinchi o‘rnatish tekshirilgach `true`.

`production` GitHub Environment **Secrets**:

- `SSH_PRIVATE_KEY` — faqat shu bot uchun yaratilgan CI private kaliti.
- `SSH_KNOWN_HOSTS` — server fingerprinti alohida tekshirilgan known_hosts yozuvi; CI keyscan natijasiga ko‘r-ko‘rona ishonmaydi.

Environment deployment branch qoidalarini faqat `master`/`main` ga cheklash tavsiya etiladi. Workflowda faqat master/main push yoki manual run deploy qiladi; PRlar secret olmaydi. Actions `contents: read` bilan cheklangan, third-party SSH action ishlatilmaydi; rasmiy actions commit SHAlariga biriktirilgan. Telegram tokeni CI Secrets ichida ham talab qilinmaydi — u serverda qoladi.

## Deploy jarayoni va xato

SHA256 bilan tekshirilgan arxiv alohida release papkasiga oddiy foydalanuvchi nomidan ochiladi. Absolute/traversal yo‘llari, symlink va device fayllari qabul qilinmaydi. Yangi build native SQLite/PNG va Word testidan **ishlayotgan xizmat to‘xtatilishidan oldin** o‘tadi. So‘ng faqat monthlyreportbot xizmati to‘xtaydi, baza+rasmlar zaxiralanadi, current symlink yangilanadi va yangi xizmat readiness tekshiruvidan o‘tadi. Migratsiyalar startupda avtomatik bajariladi.

Xato bo‘lsa va baza migration holati o‘zgarmagan bo‘lsa, eski kod qaytariladi; foydalanuvchining yangi ma’lumotlari orqaga qaytarilmaydi. Baza sxemasi o‘zgargan bo‘lsa, noto‘g‘ri kod/baza juftligini avtomatik ishga tushirish o‘rniga xizmat to‘xtatiladi va operatorga xato beriladi. Backupdan tiklash faqat operator ko‘rib chiqqandan so‘ng bajariladi; aks holda yangi tasklarni yo‘qotish mumkin.

## Boshqarish

```bash
sudo systemctl status monthlyreportbot --no-pager
sudo journalctl -u monthlyreportbot -n 80 --no-pager
sudo systemctl stop monthlyreportbot
sudo systemctl start monthlyreportbot
```

Tokenni almashtirish: `sudo nano /etc/monthlyreportbot/bot.env`, so‘ng faqat monthlyreportbot xizmatini qayta ishga tushiring. `/var/lib/monthlyreportbot` ni o‘chirmang. Zaxira olish: avval faqat botni stop qiling, `sudo /usr/local/sbin/monthlyreportbot-backup`, so‘ng start qiling. Zaxiralar va eski releaselar avtomatik o‘chirilmaydi; disk holatini kuzatib, kerakli nusxalarni qoldiring.

.NET 8 runtime self-contained artefakt ichida keladi; serverdagi boshqa .NET, Node yoki Docker muhitlari o‘zgarmaydi. Xavfsizlik yangilanishlarini olish uchun muntazam yangi build/deploy qiling. `.NET 8` ni kelajakda qo‘llab-quvvatlanadigan yangi LTSga ko‘chirish alohida rejalashtiriladi.
