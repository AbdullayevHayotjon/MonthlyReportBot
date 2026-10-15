#!/usr/bin/env bash
set -Eeuo pipefail
[[ $EUID == 0 ]] || exit 1
python3 - <<'PY'
import sqlite3, hashlib
from pathlib import Path
p=Path('/var/lib/monthlyreportbot/bot.db')
if not p.exists():
    print('no-database')
else:
    with sqlite3.connect(p.as_uri()+'?mode=ro', uri=True) as c:
        exists=c.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'").fetchone()
        rows=c.execute('SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId').fetchall() if exists else []
        print(hashlib.sha256(repr(rows).encode()).hexdigest())
PY
