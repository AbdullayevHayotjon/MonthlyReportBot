#!/usr/bin/env bash
set -Eeuo pipefail
[[ $EUID == 0 ]] || { echo 'Run: sudo bash bootstrap.sh deploy';exit 1; }
deploy_user=${1:-deploy}
[[ $deploy_user =~ ^[a-z_][a-z0-9_-]{0,31}$ ]] || exit 2
id "$deploy_user" >/dev/null
. /etc/os-release
[[ $ID == ubuntu && $VERSION_ID == 24.04 && $(uname -m) == x86_64 ]] || { echo 'This bootstrap is verified for Ubuntu 24.04 x86_64.';exit 1; }
source_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
for file in monthlyreportbot.service ssh-command.sh receive.py unpack.py deploy.sh backup.sh schema.sh import-data.py ci-key.pub;do
  [[ -f $source_dir/$file ]] || { echo "Missing setup file: $file";exit 1; }
done
# Do not update shared libraries, install Docker, change Nginx or restart other applications.
for library in libicuuc.so.74 libssl.so.3 libstdc++.so.6 libgcc_s.so.1;do
  /sbin/ldconfig -p | grep -F "$library" >/dev/null || { echo "Required library missing: $library. No packages were changed.";exit 1; }
done
for path in /opt/monthlyreportbot /var/lib/monthlyreportbot /etc/monthlyreportbot;do
  [[ ! -L $path ]] || { echo 'Unexpected symlink; stopped.';exit 1; }
done
if [[ -e /opt/monthlyreportbot && ! -f /opt/monthlyreportbot/.managed-by-monthlyreportbot ]];then
  echo '/opt/monthlyreportbot already exists and is not managed by this installer.';exit 1
fi
if id monthlyreportbot >/dev/null 2>&1 && [[ ! -f /opt/monthlyreportbot/.managed-by-monthlyreportbot ]];then
  echo 'An existing monthlyreportbot account was found; stopped to avoid affecting another application.';exit 1
fi
getent group monthlyreportbot >/dev/null || groupadd --system monthlyreportbot
if ! id monthlyreportbot >/dev/null 2>&1;then useradd --system --gid monthlyreportbot --home-dir /var/lib/monthlyreportbot --no-create-home --shell /usr/sbin/nologin monthlyreportbot;fi
usermod -aG monthlyreportbot "$deploy_user"
install -d -m 2750 -o "$deploy_user" -g monthlyreportbot /opt/monthlyreportbot /opt/monthlyreportbot/releases
install -d -m 700 -o "$deploy_user" -g monthlyreportbot /opt/monthlyreportbot/incoming
install -d -m 700 -o monthlyreportbot -g monthlyreportbot /var/lib/monthlyreportbot
install -d -m 700 -o root -g root /etc/monthlyreportbot /var/backups/monthlyreportbot
touch /opt/monthlyreportbot/.managed-by-monthlyreportbot
chown root:root /opt/monthlyreportbot/.managed-by-monthlyreportbot
install -d -m 755 /usr/local/libexec
install -m 755 -o root -g root "$source_dir/ssh-command.sh" /usr/local/libexec/monthlyreportbot-ssh
install -m 755 -o root -g root "$source_dir/receive.py" /usr/local/libexec/monthlyreportbot-receive
install -m 755 -o root -g root "$source_dir/unpack.py" /usr/local/libexec/monthlyreportbot-unpack
install -m 755 -o root -g root "$source_dir/deploy.sh" /usr/local/libexec/monthlyreportbot-deploy
install -m 755 -o root -g root "$source_dir/backup.sh" /usr/local/sbin/monthlyreportbot-backup
install -m 755 -o root -g root "$source_dir/schema.sh" /usr/local/sbin/monthlyreportbot-schema
install -m 644 -o root -g root "$source_dir/monthlyreportbot.service" /etc/systemd/system/monthlyreportbot.service
sudoers_temp=$(mktemp)
trap 'rm -f "$sudoers_temp"' EXIT
cat > "$sudoers_temp" <<EOF
$deploy_user ALL=(root) NOPASSWD: /usr/bin/systemctl stop monthlyreportbot.service, /usr/bin/systemctl start monthlyreportbot.service, /usr/bin/systemctl reset-failed monthlyreportbot.service, /usr/local/sbin/monthlyreportbot-backup, /usr/local/sbin/monthlyreportbot-schema
EOF
visudo -cf "$sudoers_temp"
install -m 440 -o root -g root "$sudoers_temp" /etc/sudoers.d/monthlyreportbot
if [[ ! -s /etc/monthlyreportbot/bot.env ]] || ! grep -q '^Bot__Token=.' /etc/monthlyreportbot/bot.env;then
  read -r -s -p 'New Telegram bot token (hidden): ' bot_token </dev/tty;echo
  [[ $bot_token =~ ^[0-9]+:[A-Za-z0-9_-]{30,}$ ]] || { echo 'Invalid token format. Rerun bootstrap.';exit 1; }
  (umask 077;printf 'Bot__Token=%s\n' "$bot_token" > /etc/monthlyreportbot/bot.env)
  unset bot_token
fi
chmod 600 /etc/monthlyreportbot/bot.env
chown root:root /etc/monthlyreportbot/bot.env
if [[ -f $source_dir/storage-transfer.tar.gz && ! -e /var/lib/monthlyreportbot/bot.db ]];then
  python3 "$source_dir/import-data.py" "$source_dir/storage-transfer.tar.gz"
fi
# Scope this CI key to this bot. Password login / other authorized keys stay unchanged.
operator_home=$(getent passwd "$deploy_user" | cut -d: -f6)
[[ -d $operator_home/.ssh && ! -L $operator_home/.ssh && ! -L $operator_home/.ssh/authorized_keys ]] || exit 1
python3 - "$operator_home/.ssh/authorized_keys" "$source_dir/ci-key.pub" <<'PY'
import sys
from pathlib import Path
path,public=map(Path,sys.argv[1:])
parts=public.read_text().split()
if len(parts)<2 or parts[0]!='ssh-ed25519':raise SystemExit('Invalid public key')
blob=parts[1]
lines=path.read_text().splitlines() if path.exists() else []
lines=[line for line in lines if blob not in line.split()]
lines.append('restrict,command="/usr/local/libexec/monthlyreportbot-ssh" '+parts[0]+' '+blob+' MonthlyReportBot-CI')
path.write_text('\n'.join(lines)+'\n')
PY
chmod 600 "$operator_home/.ssh/authorized_keys"
chown "$deploy_user:$(id -gn "$deploy_user")" "$operator_home/.ssh/authorized_keys"
systemctl daemon-reload
systemctl enable monthlyreportbot.service
echo 'Setup complete. Token stays on server. No existing services were restarted. Bot awaits its first release and data import.'
