#!/usr/bin/env python3
"""Bounded artifact upload. The forced SSH command supplies validated SHA values."""
import hashlib, os, re, sys
from pathlib import Path
release, expected=sys.argv[1:]
if not re.fullmatch('[0-9a-f]{40}',release) or not re.fullmatch('[0-9a-f]{64}',expected):
    sys.exit('Invalid release/checksum')
root=Path('/opt/monthlyreportbot/incoming')
temp=root/(release+'.partial.'+str(os.getpid()))
try:
    digest=hashlib.sha256();total=0
    fd=os.open(temp,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
    with os.fdopen(fd,'wb') as output:
        while chunk:=sys.stdin.buffer.read(1024*1024):
            total+=len(chunk)
            if total>256*1024*1024:raise ValueError('Artifact exceeds 256 MB')
            output.write(chunk);digest.update(chunk)
    if digest.hexdigest()!=expected:raise ValueError('Artifact checksum mismatch')
    os.replace(temp,root/(release+'.tar.gz'))
    print('Artifact received and checksum verified.')
finally:
    temp.unlink(missing_ok=True)
