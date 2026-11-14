import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';

const [, , scriptPath, ...args] = process.argv;
if (!scriptPath || !existsSync(scriptPath)) {
  console.error(`PowerShell script not found: ${scriptPath ?? '(missing)'}`);
  process.exit(1);
}

const candidates = process.platform === 'win32' ? ['pwsh', 'powershell'] : ['pwsh'];
for (const shell of candidates) {
  try {
    execFileSync(
      shell,
      ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', scriptPath, ...args],
      { stdio: 'inherit' },
    );
    process.exit(0);
  } catch (err) {
    if (err && typeof err === 'object' && 'code' in err && err.code === 'ENOENT') {
      continue;
    }
    const status = err && typeof err === 'object' && 'status' in err && typeof err.status === 'number'
      ? err.status
      : 1;
    process.exit(status);
  }
}

console.error(`No compatible PowerShell executable found (${candidates.join(', ')}).`);
process.exit(1);
