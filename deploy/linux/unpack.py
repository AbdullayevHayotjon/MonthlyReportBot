#!/usr/bin/env python3
"""Extract only ordinary files/directories; reject symlinks and traversal."""
import os, sys, tarfile
from pathlib import Path, PurePosixPath
archive, destination=map(Path,sys.argv[1:])
destination=destination.resolve()
with tarfile.open(archive,'r:gz') as bundle:
    members=bundle.getmembers()
    if len(members)>10000 or sum(m.size for m in members)>768*1024*1024:
        sys.exit('Unpacked release limit exceeded')
    for m in members:
        p=PurePosixPath(m.name)
        if p.is_absolute() or '..' in p.parts or not (m.isfile() or m.isdir()):
            sys.exit('Unsafe archive member')
        if p.parts and p.parts[0]=='storage':sys.exit('Runtime data must not be in a release')
        if p.name=='appsettings.Local.json' or p.name.endswith(('.db','.db-wal','.db-shm')):
            sys.exit('Private runtime files must not be in a release')
    for m in members:
        target=destination.joinpath(*PurePosixPath(m.name).parts)
        if m.isdir():target.mkdir(parents=True,exist_ok=True);target.chmod(0o2750)
        else:
            target.parent.mkdir(parents=True,exist_ok=True)
            with bundle.extractfile(m) as inp, target.open('xb') as out:
                while chunk:=inp.read(1024*1024):out.write(chunk)
            target.chmod(0o750 if m.mode&0o111 else 0o640)
