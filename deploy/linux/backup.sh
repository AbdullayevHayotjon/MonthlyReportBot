#!/usr/bin/env bash
set -Eeuo pipefail
[[ $EUID == 0 ]] || { echo 'Run as root.' >&2; exit 1; }
if systemctl is-active --quiet monthlyreportbot.service; then
  echo 'Stop monthlyreportbot.service before a consistent backup.' >&2; exit 1
fi
umask 077
install -d -m 700 /var/backups/monthlyreportbot
archive="/var/backups/monthlyreportbot/predeploy-$(date -u +%Y%m%dT%H%M%S)-$$.tar.gz"
tar -czf "$archive" -C /var/lib monthlyreportbot
printf '%s\n' "$archive" > /var/backups/monthlyreportbot/latest-predeploy
echo 'Database and images backed up.'
