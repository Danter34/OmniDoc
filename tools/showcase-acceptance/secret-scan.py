"""Local audit: print finding locations only, never secret values or matched lines."""
import json
import re
import subprocess
from pathlib import Path

config = dict(line.split('=', 1) for line in Path('.env').read_text().splitlines()
              if '=' in line and not line.startswith('#'))
secret_names = ['GEMINI_API_KEY', 'JWT_SECRET', 'POSTGRES_PASSWORD', 'SMTP_PASSWORD']
secrets = {k: config[k].encode() for k in secret_names if len(config.get(k, '')) >= 12}
patterns = {'Gemini key': rb'AIza[0-9A-Za-z_-]{35}',
            'private key': rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
            'GitHub token': rb'gh[pousr]_[A-Za-z0-9]{36,}'}
findings = []
files = subprocess.check_output(['git', 'ls-files', '-z']).decode().split('\0')
for name in filter(None, files):
    path = Path(name)
    if not path.is_file():
        continue
    data = path.read_bytes()
    for label, value in secrets.items():
        if value in data:
            findings.append([name, label])
    for label, pattern in patterns.items():
        if re.search(pattern, data):
            findings.append([name, label])
history = subprocess.check_output(['git', 'log', '--all', '-p', '--format=', '--no-ext-diff', '--no-textconv'])
for label, value in secrets.items():
    if value in history:
        findings.append(['reachable git history', label])
for label, pattern in patterns.items():
    if re.search(pattern, history):
        findings.append(['reachable git history', label])
print(json.dumps({'tracked_files_scanned': len(list(filter(None, files))), 'findings': findings}))
raise SystemExit(bool(findings))
