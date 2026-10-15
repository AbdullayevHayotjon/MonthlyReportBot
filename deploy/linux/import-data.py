#!/usr/bin/env python3
"""One-time cold data import. Does not overwrite an existing server database."""
import os, pwd, shutil, sqlite3, sys, tarfile, tempfile
from pathlib import Path,PurePosixPath
if os.geteuid()!=0:sys.exit('Run as root')
destination=Path('/var/lib/monthlyreportbot')
if (destination/'bot.db').exists():sys.exit('Existing server database retained; import stopped')
account=pwd.getpwnam('monthlyreportbot')
with tempfile.TemporaryDirectory(prefix='monthlyreportbot-import-') as temp:
    root=Path(temp)
    with tarfile.open(sys.argv[1],'r:gz') as archive:
        members=archive.getmembers()
        if len(members)>100000 or sum(m.size for m in members)>10*1024**3:sys.exit('Data import limit exceeded')
        for m in members:
            p=PurePosixPath(m.name)
            if p.is_absolute() or '..' in p.parts or not (m.isfile() or m.isdir()):sys.exit('Unsafe data member')
            if not p.parts or p.parts[0] not in ('bot.db','images'):sys.exit('Unexpected data member')
            if p.parts[0]=='bot.db' and len(p.parts)!=1:sys.exit('Invalid database member')
            target=root.joinpath(*p.parts)
            if m.isdir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with archive.extractfile(m) as inp,target.open('xb') as out:shutil.copyfileobj(inp,out)
    db=root/'bot.db'
    if not db.is_file():sys.exit('Database missing')
    with sqlite3.connect(db) as c:
        c.execute("UPDATE TaskImages SET FilePath=replace(FilePath,char(92),'/')")
        c.commit()
        if c.execute('PRAGMA integrity_check').fetchone()[0]!='ok':sys.exit('Database integrity check failed')
        if c.execute('PRAGMA foreign_key_check').fetchone():sys.exit('Database relationship check failed')
        images=c.execute('SELECT FilePath,FileSize FROM TaskImages').fetchall()
        for relative,size in images:
            p=PurePosixPath(relative)
            if p.is_absolute() or '..' in p.parts or not p.parts or p.parts[0]!='images':sys.exit('Unsafe stored image path')
            image=root.joinpath(*p.parts)
            if not image.is_file() or image.stat().st_size!=size:sys.exit('Image missing or size mismatch')
        counts={table:c.execute('SELECT COUNT(*) FROM '+table).fetchone()[0] for table in ['Users','WorkTasks','TaskImages']}
    c.close()  # Flush/checkpoint WAL before copying only the database file.
    # The target database was confirmed absent. Keep IDs, dates and image ordering intact.
    if (root/'images').exists():
        if (destination/'images').exists():sys.exit('Existing image directory retained; import stopped')
        shutil.copytree(root/'images',destination/'images')
    shutil.copy2(db,destination/'bot.db')
    for p in [destination,*destination.rglob('*')]:
        os.chown(p,account.pw_uid,account.pw_gid);p.chmod(0o700 if p.is_dir() else 0o600)
    print('Data imported and verified (Windows image paths normalized):',counts)
