// One command to run the Acme Contact Form demo site from a clone:
//   node samples/run-demo.mjs
// Installs and builds the backoffice client when needed, then starts the Umbraco site.
// Node built-ins only.
import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const samplesDir = path.dirname(fileURLToPath(import.meta.url));
const clientDir = path.join(samplesDir, 'Acme.ContactForm', 'Client');
const bundleDir = path.join(samplesDir, 'Acme.ContactForm', 'wwwroot', 'App_Plugins', 'AcmeContactForm');
const siteDir = path.join(samplesDir, 'Acme.ContactForm.Site');

export const MIN_NODE = [24, 13];

// True when the running Node is at least MIN_NODE. `version` is a string like "24.13.0".
export function nodeVersionOk(version = process.versions.node, min = MIN_NODE) {
  const [major = 0, minor = 0] = version.split('.').map(Number);
  return major > min[0] || (major === min[0] && minor >= min[1]);
}

// Newest mtime (ms) of the file, or of every file under the directory. 0 when nothing exists.
function newestMtime(target) {
  let stat;
  try {
    stat = fs.statSync(target);
  } catch {
    return 0;
  }
  if (!stat.isDirectory()) return stat.mtimeMs;
  let newest = 0;
  for (const entry of fs.readdirSync(target)) {
    newest = Math.max(newest, newestMtime(path.join(target, entry)));
  }
  return newest;
}

// Rule: build when the bundle has no files, or when the newest build input is newer than the
// newest bundle file. `inputs` are files or directories (searched recursively).
export function needsBuild({ bundleDir, inputs }) {
  const bundleNewest = newestMtime(bundleDir);
  if (bundleNewest === 0) return true;
  return inputs.some((input) => newestMtime(input) > bundleNewest);
}

const buildInputs = () => [
  path.join(clientDir, 'src'),
  path.join(clientDir, 'public'),
  path.join(clientDir, 'package.json'),
  path.join(clientDir, 'vite.config.ts'),
  path.join(clientDir, 'tsconfig.json'),
];

function run(command, args, cwd) {
  // npm is npm.cmd on Windows, which needs a shell to start.
  const result = spawnSync(command, args, { cwd, stdio: 'inherit', shell: process.platform === 'win32' && command === 'npm' });
  if (result.error || result.status !== 0) {
    console.error(`\n"${command} ${args.join(' ')}" failed${result.error ? `: ${result.error.message}` : ` with exit code ${result.status}`}.`);
    process.exit(result.status || 1);
  }
}

function main() {
  if (!nodeVersionOk()) {
    console.error(`Node ${MIN_NODE.join('.')} or newer is required (this is ${process.versions.node}).`);
    process.exit(1);
  }

  if (!fs.existsSync(path.join(clientDir, 'node_modules'))) {
    console.log('Installing client packages (npm ci)...');
    run('npm', ['ci'], clientDir);
  }

  if (needsBuild({ bundleDir, inputs: buildInputs() })) {
    console.log('Building the backoffice client (npm run build)...');
    run('npm', ['run', 'build'], clientDir);
  } else {
    console.log('Backoffice client bundle is up to date.');
  }

  console.log('Starting the demo site (dotnet run)...');
  const site = spawn('dotnet', ['run', '--project', siteDir], { stdio: 'inherit' });
  // Ctrl+C reaches the dotnet child through the terminal too; forward it for other senders.
  for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => site.kill(signal));
  site.on('error', (error) => {
    console.error(`Could not start dotnet: ${error.message}`);
    process.exit(1);
  });
  site.on('exit', (code, signal) => process.exit(code ?? (signal ? 130 : 1)));
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) main();
