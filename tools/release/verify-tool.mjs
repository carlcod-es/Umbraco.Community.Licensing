// Installs the tool from the pack folder and checks `ucl --version`: node tools/release/verify-tool.mjs <pack folder> <tool path> [expected version].
// The reported version must equal the Version.props release version exactly, as backups depend on it (ADR-0023).
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { readVersionFile } from './release-rules.mjs';

export const TOOL_ID = 'Umbraco.Community.Licensing.Tool';
export const LIBRARY_ID = 'Umbraco.Community.Licensing';

// Returns a failure reason, or null when the folder holds exactly the library and tool packages for the version and no other .nupkg.
// Guards against a pack step that failed silently in a shell that reports only the last command's exit code.
export function checkPackFolder(fileNames, version) {
  const expected = [LIBRARY_ID, TOOL_ID].map((id) => `${id}.${version}.nupkg`);
  const found = fileNames.filter((n) => n.endsWith('.nupkg'));
  const missing = expected.filter((n) => !found.includes(n));
  const extra = found.filter((n) => !expected.includes(n)).sort();
  if (missing.length === 0 && extra.length === 0) return null;
  const parts = [];
  if (missing.length) parts.push(`missing ${missing.join(', ')}`);
  if (extra.length) parts.push(`unexpected ${extra.join(', ')}`);
  return `The pack folder must hold exactly ${expected.join(' and ')}: ${parts.join('; ')}.`;
}

// Returns a failure reason, or null when the output is exactly the expected version (one trailing newline allowed).
export function checkVersionOutput(output, expected) {
  const reported = String(output ?? '').replace(/\r?\n$/, '');
  if (reported === expected) return null;
  return `'ucl --version' reported '${reported}' but the release version in Version.props is '${expected}'.`;
}

// Exact-version range, so only the pinned version can be chosen.
export function installArgs(packFolder, toolPath, version) {
  return ['tool', 'install', TOOL_ID, '--tool-path', toolPath, '--add-source', packFolder, '--version', `[${version}]`];
}

function run(command, args) {
  const result = spawnSync(command, args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] });
  if (result.error) throw new Error(`Cannot run ${command}: ${result.error.message}`);
  return result;
}

function main(argv) {
  const [packFolder, toolPath, override] = argv;
  if (!packFolder || !toolPath) {
    throw new Error('Usage: node tools/release/verify-tool.mjs <pack folder> <tool path> [expected version]');
  }
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
  const expected = override ?? readVersionFile(fs.readFileSync(path.join(root, 'Version.props'), 'utf8')).version;

  const packReason = checkPackFolder(fs.readdirSync(path.resolve(packFolder)), expected);
  if (packReason) throw new Error(packReason);

  const install = run('dotnet', installArgs(path.resolve(packFolder), path.resolve(toolPath), expected));
  if (install.status !== 0) {
    process.stdout.write(install.stdout);
    throw new Error(`dotnet tool install failed with exit code ${install.status}.`);
  }
  const ucl = run(path.join(path.resolve(toolPath), process.platform === 'win32' ? 'ucl.exe' : 'ucl'), ['--version']);
  if (ucl.status !== 0) throw new Error(`'ucl --version' exited with code ${ucl.status}.`);
  const reason = checkVersionOutput(ucl.stdout, expected);
  if (reason) throw new Error(reason);
  console.log(`ucl --version reported ${expected}.`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    main(process.argv.slice(2));
  } catch (e) {
    console.error(`::error::${e.message}`);
    process.exit(1);
  }
}
