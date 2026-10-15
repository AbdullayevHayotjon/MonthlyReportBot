#!/usr/bin/env bash
set -Eeuo pipefail
umask 027
release=${1:-};expected=${2:-}
[[ $release =~ ^[0-9a-f]{40}$ && $expected =~ ^[0-9a-f]{64}$ ]] || exit 2
root=/opt/monthlyreportbot
exec 9>"$root/.deployment.lock"
flock -n 9 || { echo 'Another deployment is running.' >&2; exit 1; }
archive="$root/incoming/$release.tar.gz"
[[ -f $archive && ! -L $archive ]] || exit 2
[[ $(sha256sum "$archive" | cut -d' ' -f1) == "$expected" ]] || { echo 'Checksum mismatch.' >&2; exit 1; }
target="$root/releases/$release"
previous=$(readlink -f "$root/current" 2>/dev/null || true)
if [[ -e $target ]]; then
  if [[ $previous == "$target" ]] && systemctl is-active --quiet monthlyreportbot.service && Bot__HealthFile=/run/monthlyreportbot/healthy "$target/MonthlyReportBot" --health; then
    echo 'This release is already healthy.'; exit 0
  fi
  [[ -f $target/.artifact-sha256 && $(cat "$target/.artifact-sha256") == "$expected" ]] || { echo 'Incomplete or mismatched release; inspect it before retrying.' >&2;exit 1; }
else
  mkdir -m 2750 "$target"
  python3 /usr/local/libexec/monthlyreportbot-unpack "$archive" "$target"
  printf '%s\n' "$expected" > "$target/.artifact-sha256"
fi
if [[ -f $root/last-successful-release ]];then
  last=$(cat "$root/last-successful-release")
  if [[ $last =~ ^[0-9a-f]{40}$ && $last != "$release" && -d $root/releases/$last ]];then previous="$root/releases/$last";fi
fi
if [[ $previous == "$target" ]];then previous='';fi
[[ -x $target/MonthlyReportBot ]] || { echo 'Missing executable.' >&2; exit 1; }
# Offline verification happens before the running service is stopped.
"$target/MonthlyReportBot" --verify-package
sudo -n /usr/bin/systemctl stop monthlyreportbot.service
schema_before=$(sudo -n /usr/local/sbin/monthlyreportbot-schema)
restore_on_error() {
  result=$?
  trap - ERR
  sudo -n /usr/bin/systemctl stop monthlyreportbot.service || true
  schema_after=$(sudo -n /usr/local/sbin/monthlyreportbot-schema || echo unknown)
  if [[ -n $previous && $previous == "$root/releases/"* && $schema_before == "$schema_after" ]]; then
    ln -s "$previous" "$root/.rollback-$$"; mv -Tf "$root/.rollback-$$" "$root/current"
    sudo -n /usr/bin/systemctl reset-failed monthlyreportbot.service || true
    sudo -n /usr/bin/systemctl start monthlyreportbot.service || true
    echo 'Deployment failed; previous code restored. Database/images were not reverted.' >&2
  else
    echo 'Deployment failed; service stopped. Schema changed or no previous release. Root backup is retained; inspect before restoring data.' >&2
  fi
  exit "$result"
}
trap restore_on_error ERR
sudo -n /usr/local/sbin/monthlyreportbot-backup
ln -s "$target" "$root/.current-$$"; mv -Tf "$root/.current-$$" "$root/current"
sudo -n /usr/bin/systemctl reset-failed monthlyreportbot.service || true
sudo -n /usr/bin/systemctl start monthlyreportbot.service
healthy=false
for attempt in $(seq 1 45); do
  if systemctl is-active --quiet monthlyreportbot.service && Bot__HealthFile=/run/monthlyreportbot/healthy "$target/MonthlyReportBot" --health; then healthy=true;break;fi
  sleep 2
done
[[ $healthy == true ]] || { echo 'Worker readiness timed out.' >&2; false; }
trap - ERR
printf '%s\n' "$release" > "$root/last-successful-release"
echo "Release $release is healthy."
# Releases and private backups are kept for operator-controlled retention/rollback.
