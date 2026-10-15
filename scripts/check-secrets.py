#!/usr/bin/env python3
"""Fail without printing credential values. This scans current tracked files, not Git history."""
import json,re,subprocess,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
repo=subprocess.run(['git','rev-parse','--show-toplevel'],cwd=root,capture_output=True,text=True)
is_repo_root=repo.returncode==0 and Path(repo.stdout.strip()).resolve()==root
result=subprocess.run(['git','ls-files','-z'],cwd=root,capture_output=True) if is_repo_root else None
paths=[root/p.decode('utf-8') for p in result.stdout.split(b'\0') if p] if result is not None and result.returncode==0 else [p for p in root.rglob('*') if p.is_file() and not {'bin','obj','.git','.local','publish','storage','artifacts','__pycache__'}.intersection(p.relative_to(root).parts)]
problems=[]
for p in paths:
    if not p.is_file():continue
    data=p.read_bytes()
    if re.search(rb'\b[0-9]{7,}:[A-Za-z0-9_-]{30,}',data) or (b'-----BEGIN '+b'OPENSSH PRIVATE KEY-----') in data or (b'-----BEGIN '+b'RSA PRIVATE KEY-----') in data:
        problems.append(str(p.relative_to(root)))
    if p.name=='appsettings.json':
        try:
            if json.loads(data.decode('utf-8-sig')).get('Bot',{}).get('Token'):problems.append(str(p.relative_to(root)))
        except (ValueError,UnicodeError):problems.append(str(p.relative_to(root)))
if problems:
    print('Credential-like content found in files (values hidden):',file=sys.stderr)
    for name in sorted(set(problems)):print(name,file=sys.stderr)
    sys.exit(1)
print('Tracked-file credential check passed. Previously exposed tokens must still be revoked.')
