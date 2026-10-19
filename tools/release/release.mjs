// The release command. See docs/adrs/0009-release-command-version-file-and-log.md for the flow.
// Run as `node tools/release/release.mjs` from the repository root, at an interactive prompt.
import { execFileSync } from 'node:child_process';
import { readFileSync, realpathSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  invalidLineMessage, parseLine, nextVersion, nextTag, localDate,
  parseLog, insertRow, assertVersionNotInLog, readVersionFile, writeVersion,
} from './release-rules.mjs';

const VERSION_FILE = 'Version.props';
const LOG_FILE = 'RELEASES.md';

// Git is never run through a shell. A failure carries git's own text.
function git(cwd, args) {
  try {
    return execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  } catch (e) {
    const detail = [e.stderr, e.stdout].map((s) => String(s ?? '').trim()).filter(Boolean).join('\n');
    throw new Error(`git ${args.join(' ')} failed${detail ? `:\n${detail}` : '.'}`);
  }
}

// Failure after the release commit: state and the exact commands to finish by hand.
function unfinishedMessage({ sha, tag, mainPushed, tagged, failed, error }) {
  const commands = [];
  if (!mainPushed) commands.push('git push origin main');
  if (!tagged) commands.push(`git tag ${tag} ${sha}`);
  commands.push(`git push origin ${tag}`);
  return [
    `The release stopped: ${failed} failed.`,
    error.message,
    '',
    'State:',
    `  release commit ${sha} is made on local main`,
    `  main pushed to origin: ${mainPushed ? 'yes' : 'no'}`,
    `  tag ${tag} created locally: ${tagged ? 'yes' : 'no'}`,
    '  tag pushed to origin: no',
    'The release has not started: nothing is published, because the tag is not on the remote.',
    'Nothing was retried or forced. To finish by hand, run these in order:',
    ...commands.map((c) => `  ${c}`),
  ].join('\n');
}

export async function runRelease({ cwd: startDir, prompt, now, print = console.log }) {
  // 1. Fetch, so remote main and remote tags are current.
  git(startDir, ['fetch', 'origin', '--tags']);

  // Files and git calls below use the repository root, wherever the command was started.
  const cwd = git(startDir, ['rev-parse', '--show-toplevel']).trim();

  // 2. On main.
  const branch = git(cwd, ['branch', '--show-current']).trim();
  if (branch !== 'main') {
    throw new Error(branch === ''
      ? "Not on main: HEAD is detached. A release is made from 'main'."
      : `Not on main: the current branch is '${branch}'. A release is made from 'main'.`);
  }

  // 3. main equals origin/main.
  const [ahead, behind] = git(cwd, ['rev-list', '--left-right', '--count', 'main...origin/main'])
    .trim().split(/\s+/).map(Number);
  if (ahead > 0 && behind > 0) {
    throw new Error(`Out of step with the remote: local main has ${ahead} commit(s) the remote lacks and lacks ${behind} commit(s) the remote has.`);
  }
  if (ahead > 0) throw new Error(`Out of step with the remote: local main has ${ahead} commit(s) the remote lacks. Push them first.`);
  if (behind > 0) throw new Error(`Out of step with the remote: local main lacks ${behind} commit(s) the remote has. Pull them first.`);

  // 4. The two files the command writes must be clean.
  const dirty = git(cwd, ['status', '--porcelain', '--', VERSION_FILE, LOG_FILE])
    .split('\n').filter((l) => l.trim() !== '').map((l) => l.slice(3));
  if (dirty.length > 0) {
    throw new Error(`Uncommitted changes in ${dirty.join(', ')}. Commit or discard them first.`);
  }

  // 5. Line, log, next version.
  const versionText = readFileSync(join(cwd, VERSION_FILE), 'utf8');
  const logText = readFileSync(join(cwd, LOG_FILE), 'utf8');
  const { line } = readVersionFile(versionText);
  if (parseLine(line) === null) throw new Error(invalidLineMessage(line));
  const { rows } = parseLog(logText);
  const version = nextVersion(line, rows.map((r) => r.version));
  assertVersionNotInLog(rows, version);

  // 6. Tag, counting tags fetched from the remote.
  const tags = git(cwd, ['tag', '--list']).split('\n').map((t) => t.trim()).filter(Boolean);
  // One reading of the clock, so the tag and the row date cannot fall on different days.
  const today = now();
  const tag = nextTag(today, tags);

  // 7. Note.
  let note = '';
  // A note with a line break cannot be a log row: ask again, as for an empty note.
  while (note === '' || /[\r\n]/.test(note)) note = (await prompt('What changed (one line)? ')).trim();

  // 8. Confirm.
  print(`Version: ${version}`);
  print(`Tag:     ${tag}`);
  print(`Note:    ${note}`);
  const answer = (await prompt(`Release ${version} as ${tag}? [y/N] `)).trim().toLowerCase();
  if (answer !== 'y' && answer !== 'yes') {
    print('Declined. Nothing changed.');
    return { outcome: 'declined' };
  }

  // 9. Write both files (insertRow validates the note before anything is written).
  const newLog = insertRow(logText, { version, tag, date: localDate(today), note });
  const newVersionText = writeVersion(versionText, version);
  writeFileSync(join(cwd, VERSION_FILE), newVersionText);
  writeFileSync(join(cwd, LOG_FILE), newLog);

  // 10. Commit only those two files.
  let sha;
  try {
    git(cwd, ['commit', '--only', VERSION_FILE, LOG_FILE, '-m', `Release ${version}`]);
    sha = git(cwd, ['rev-parse', 'HEAD']).trim();
  } catch (e) {
    throw new Error([
      `The commit failed. ${VERSION_FILE} and ${LOG_FILE} were written but not committed; nothing was pushed or tagged.`,
      e.message,
      `To discard the changes: git checkout -- ${VERSION_FILE} ${LOG_FILE}`,
    ].join('\n'));
  }

  // 11-13. Publish. Any failure stops at once: no retry, no force.
  const state = { sha, tag, mainPushed: false, tagged: false };
  const step = (failed, args) => {
    try {
      git(cwd, args);
    } catch (error) {
      throw new Error(unfinishedMessage({ ...state, failed, error }));
    }
  };
  step('the push of main', ['push', 'origin', 'main']);
  state.mainPushed = true;
  step(`creating tag ${tag}`, ['-c', 'tag.gpgSign=false', 'tag', tag, sha]);
  state.tagged = true;
  step(`the push of tag ${tag}`, ['push', 'origin', tag]);

  print(`Released ${version} as ${tag} (commit ${sha}). The release run starts from the tag.`);
  return { outcome: 'released', version, tag, commit: sha };
}

// ---- CLI ----

const CANCELLED = 'Cancelled. Nothing changed.';

// readline leaves a pending question unsettled when input closes (Ctrl-D, closed stdin): turn that into a rejection.
function questioner(rl) {
  let closed = false;
  const onClose = new Set();
  rl.on('close', () => { closed = true; for (const reject of onClose) reject(new Error(CANCELLED)); });
  return (q) => new Promise((resolvePrompt, reject) => {
    if (closed) return reject(new Error(CANCELLED));
    onClose.add(reject);
    rl.question(q).then(resolvePrompt, (e) => reject(e?.name === 'AbortError' ? new Error(CANCELLED) : e))
      .finally(() => onClose.delete(reject));
  });
}

async function main() {
  if (!process.stdin.isTTY || !process.stdout.isTTY) {
    console.error('No person present: a release is started only by Carl at an interactive prompt. Nothing was changed.');
    process.exitCode = 1;
    return;
  }
  const { createInterface } = await import('node:readline/promises');
  const rl = createInterface({ input: process.stdin, output: process.stdout });
  try {
    await runRelease({ cwd: process.cwd(), prompt: questioner(rl), now: () => new Date() });
  } catch (e) {
    console.error(e.message);
    process.exitCode = 1;
  } finally {
    rl.close();
  }
}

// Compare canonical real paths: argv[1] is not resolved (symlinks, "..", 8.3 names, case), import.meta.url is.
// A resolution failure throws rather than silently skipping the command.
function isMainModule() {
  if (!process.argv[1]) return false;
  const real = (p) => realpathSync.native(p);
  return real(fileURLToPath(import.meta.url)) === real(resolve(process.argv[1]));
}

if (isMainModule()) {
  await main();
}
