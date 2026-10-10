#!/usr/bin/env python3
"""Push an already committed version and append a local audit receipt (even on failure)."""
import argparse
import datetime
import json
import re
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[1]
def git(*args):
    return subprocess.check_output(['git', *args], cwd=root, text=True).strip()

parser = argparse.ArgumentParser()
parser.add_argument('--notes', required=True)
args = parser.parse_args()
version = (root / 'VERSION').read_text().strip()
if not re.fullmatch(r'\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?', version):
    raise SystemExit('Invalid VERSION')
if git('status', '--porcelain'):
    raise SystemExit('Commit or resolve working tree changes before release')
branch = git('branch', '--show-current')
if not branch:
    raise SystemExit('Detached HEAD; cannot release')
commit = git('rev-parse', 'HEAD')
tag = 'v' + version
if git('tag', '--list', tag):
    if git('rev-list', '-n', '1', tag) != commit:
        raise SystemExit('Existing tag points to a different commit; refusing to overwrite')
else:
    subprocess.run(['git', 'tag', '-a', tag, '-m', f'Nook Constellation {version}'], cwd=root, check=True)
local = root / '.local'
local.mkdir(exist_ok=True)
receipts = local / 'push-records.jsonl'
number = len(receipts.read_text().splitlines()) + 1 if receipts.exists() else 1
record = dict(number=number, time=datetime.datetime.now().astimezone().isoformat(timespec='seconds'), version=version, commit=commit, remote=git('remote', 'get-url', 'origin'), branch=branch, tag=tag, content=args.notes, status='pending')
def append_receipt():
    with receipts.open('a', encoding='utf-8') as f:
        f.write(json.dumps(record, ensure_ascii=False) + '\n')
    with (local / 'push-records.md').open('a', encoding='utf-8') as f:
        f.write(f"\n## {number:04d} · {version}\n\n- 时间：{record['time']}\n- 提交：{commit}\n- 目标：{record['remote']} / {branch} / {tag}\n- 内容：{args.notes}\n- 结果：{record['status']}\n")
# Write intent first so interrupted network operations remain auditable.
(local / 'pending-push.json').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
try:
    result = subprocess.run(['git', 'push', '--atomic', 'origin', f'HEAD:refs/heads/{branch}', f'refs/tags/{tag}'], cwd=root)
    if result.returncode:
        record['status'] = f'failed (exit {result.returncode})'
    else:
        record['status'] = 'success'
except BaseException as e:
    record['status'] = f'interrupted: {type(e).__name__}'
    raise
finally:
    append_receipt()
    (local / 'pending-push.json').unlink(missing_ok=True)
print(json.dumps(record, ensure_ascii=False, indent=2))
if record['status'] != 'success':
    raise SystemExit(1)
