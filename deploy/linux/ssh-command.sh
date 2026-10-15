#!/usr/bin/env bash
set -Eeuo pipefail
umask 027
read -r -a arguments <<< "${SSH_ORIGINAL_COMMAND:-}"
case "${arguments[0]:-}" in
  upload|deploy)
    [[ ${#arguments[@]} == 3 && ${arguments[1]} =~ ^[0-9a-f]{40}$ && ${arguments[2]} =~ ^[0-9a-f]{64}$ ]] || exit 2
    if [[ ${arguments[0]} == upload ]]; then
      exec /usr/local/libexec/monthlyreportbot-receive "${arguments[1]}" "${arguments[2]}"
    fi
    exec /usr/local/libexec/monthlyreportbot-deploy "${arguments[1]}" "${arguments[2]}"
    ;;
  status)
    [[ ${#arguments[@]} == 1 ]] || exit 2
    systemctl is-active monthlyreportbot.service
    Bot__HealthFile=/run/monthlyreportbot/healthy /opt/monthlyreportbot/current/MonthlyReportBot --health
    ;;
  *) echo 'This key permits MonthlyReportBot upload, deploy and status only.' >&2; exit 2 ;;
esac
