// Release-run checks: node tools/release/check.mjs <tag>
// Run from a checkout of the tagged commit with full history. See docs/adrs/0009-release-command-version-file-and-log.md.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkRelease, readVersionFile } from './release-rules.mjs';

export function platformLine() {
  return `Platform: ${os.type()} ${os.release()} ${os.arch()}`;
}

function git(cwd, args) {
  return execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
}

function gitMessage(e) {
  const detail = String(e.stderr ?? '').trim() || e.message;
  return detail.split(/\r?\n/)[0];
}

function readRepoFile(root, name, reasons) {
  try {
    return fs.readFileSync(path.join(root, name), 'utf8');
  } catch (e) {
    reasons.push(`Cannot read ${name} at the repository root: ${e.message}`);
    return null;
  }
}

export function runCheck({ cwd, tag }) {
  const reasons = [];
  let version = null;

  let root;
  try {
    root = git(cwd, ['rev-parse', '--show-toplevel']);
  } catch (e) {
    reasons.push(`Not inside a Git repository: ${gitMessage(e)}`);
    return { platform: platformLine(), reasons, version };
  }

  const versionFileText = readRepoFile(root, 'Version.props', reasons);
  const logText = readRepoFile(root, 'RELEASES.md', reasons);
  if (versionFileText !== null) {
    try {
      version = readVersionFile(versionFileText).version;
    } catch {
      // checkRelease reports it below
    }
  }
  if (versionFileText !== null && logText !== null) {
    reasons.push(...checkRelease({ tag, versionFileText, logText }));
  }

  try {
    git(root, ['merge-base', '--is-ancestor', 'HEAD', 'origin/main']);
  } catch (e) {
    if (e.status === 1) {
      let sha = 'HEAD';
      try { sha = git(root, ['rev-parse', '--short', 'HEAD']); } catch { /* keep HEAD */ }
      reasons.push(`Commit ${sha} is not on main.`);
    } else {
      reasons.push(`Cannot tell whether the commit is on main (git merge-base --is-ancestor HEAD origin/main): ${gitMessage(e)}`);
    }
  }

  return { platform: platformLine(), reasons, version };
}

function main(argv) {
  console.log(platformLine());
  if (argv.length !== 1) {
    console.error('Usage: node tools/release/check.mjs <tag>');
    return 1;
  }
  const tag = argv[0];
  const { reasons, version } = runCheck({ cwd: process.cwd(), tag });
  if (reasons.length > 0) {
    console.error(`Release refused for tag '${tag}':`);
    for (const reason of reasons) console.error(`- ${reason}`);
    return 1;
  }
  console.log(`Release check passed: tag ${tag}, version ${version}.`);
  return 0;
}

// Compare canonical real paths: argv[1] is not resolved (symlinks, "..", 8.3 names, case), import.meta.url is.
// A resolution failure throws rather than silently skipping the gate.
function isMainModule() {
  if (!process.argv[1]) return false;
  const real = (p) => fs.realpathSync.native(p);
  return real(fileURLToPath(import.meta.url)) === real(path.resolve(process.argv[1]));
}

if (isMainModule()) {
  process.exitCode = main(process.argv.slice(2));
}
