import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const SCRIPT = path.join(path.dirname(fileURLToPath(import.meta.url)), 'check.mjs');
const TAG = 'release-20261020';

const temps = [];
after(() => {
  for (const dir of temps) fs.rmSync(dir, { recursive: true, force: true, maxRetries: 5, retryDelay: 100 });
});

const versionProps = (line, version) =>
  `<Project>\n  <PropertyGroup>\n    <VersionLine>${line}</VersionLine>\n    <Version>${version}</Version>\n  </PropertyGroup>\n</Project>\n`;

const releaseLog = (...rows) =>
  `# Releases\n\n| Version | Tag | Date | What changed |\n|---|---|---|---|\n${rows.map(([v, t]) => `| ${v} | ${t} | 2026-10-20 | Note |\n`).join('')}`;

// Bare remote plus a clone with a committed `main` pushed to it.
function createRepo({ line = '0.1-beta', version = '0.1.0-beta.1', log = releaseLog(['0.1.0-beta.1', TAG]) } = {}) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'release-check-'));
  temps.push(root);
  const emptyConfig = path.join(root, 'gitconfig');
  fs.writeFileSync(emptyConfig, '');
  const env = {
    ...process.env,
    GIT_CONFIG_GLOBAL: emptyConfig,
    GIT_AUTHOR_NAME: 'Test', GIT_AUTHOR_EMAIL: 'test@example.com',
    GIT_COMMITTER_NAME: 'Test', GIT_COMMITTER_EMAIL: 'test@example.com',
  };
  // Inherited repository-location variables (e.g. when run from a git hook) would point git at the outer repository.
  for (const k of ['GIT_DIR', 'GIT_INDEX_FILE', 'GIT_WORK_TREE', 'GIT_OBJECT_DIRECTORY', 'GIT_ALTERNATE_OBJECT_DIRECTORIES', 'GIT_COMMON_DIR']) delete env[k];
  const remote = path.join(root, 'remote.git');
  const work = path.join(root, 'work');
  fs.mkdirSync(remote);
  fs.mkdirSync(work);
  const git = (cwd, ...args) => execFileSync('git', args, { cwd, env, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  git(remote, 'init', '--bare', '--initial-branch=main');
  git(work, 'init', '--initial-branch=main');
  git(work, 'remote', 'add', 'origin', remote);
  fs.writeFileSync(path.join(work, 'Version.props'), versionProps(line, version));
  fs.writeFileSync(path.join(work, 'RELEASES.md'), log);
  git(work, 'add', '.');
  git(work, 'commit', '-m', 'Release');
  git(work, 'push', 'origin', 'main');
  return { work, env, git: (...args) => git(work, ...args) };
}

function runCli(repo, args = [TAG]) {
  const r = spawnSync(process.execPath, [SCRIPT, ...args], { cwd: repo.work, env: repo.env, encoding: 'utf8' });
  return { status: r.status, stdout: r.stdout, stderr: r.stderr };
}

function runScript(repo, script, args) {
  const r = spawnSync(process.execPath, [script, ...args], { cwd: repo.work, env: repo.env, encoding: 'utf8' });
  return { status: r.status, stdout: r.stdout, stderr: r.stderr };
}

function refused(result, ...fragments) {
  assert.equal(result.status, 1, result.stderr);
  for (const fragment of fragments) assert.ok(result.stderr.includes(fragment), `expected '${fragment}' in:\n${result.stderr}`);
}

test('Commit on main passes (check.mjs)', () => {
  const repo = createRepo();
  const r = runCli(repo);
  assert.equal(r.status, 0, r.stderr);
  assert.match(r.stdout, /^Platform: /);
  assert.ok(r.stdout.includes(`tag ${TAG}, version 0.1.0-beta.1`));
  assert.equal(r.stderr, '');
});

test('Scenario: Commit not on main (check.mjs)', () => {
  const repo = createRepo();
  repo.git('checkout', '-b', 'feature');
  fs.appendFileSync(path.join(repo.work, 'extra.txt'), 'x');
  repo.git('add', '.');
  repo.git('commit', '-m', 'Feature work');
  repo.git('push', 'origin', 'feature');
  refused(runCli(repo), 'is not on main');
});

test('Scenario: Malformed tag (check.mjs)', () => {
  const repo = createRepo();
  for (const tag of ['release-2026-10-20', 'release-20261020-1']) {
    refused(runCli(repo, [tag]), 'does not match');
  }
});

test('Scenario: Duplicate version in the log (check.mjs)', () => {
  const repo = createRepo({ log: releaseLog(['0.1.0-beta.1', TAG], ['0.1.0-beta.1', TAG]) });
  refused(runCli(repo), "Version '0.1.0-beta.1' appears 2 times");
});

test('Scenario: Beta status disagrees (check.mjs)', () => {
  const a = createRepo({ line: '1', version: '1.0.0-beta.1', log: releaseLog(['1.0.0-beta.1', TAG]) });
  refused(runCli(a), '1.0.0-beta.1', "'1'");
  const b = createRepo({ line: '0.1-beta', version: '0.1.0', log: releaseLog(['0.1.0', TAG]) });
  refused(runCli(b), '0.1.0', '0.1-beta');
});

test('Scenario: Tag pushed without the release command (check.mjs)', () => {
  const repo = createRepo();
  refused(runCli(repo, ['release-20261021']), 'release-20261020', 'release-20261021');
});

test('Scenario: Tag with no log row (check.mjs)', () => {
  const repo = createRepo({ log: releaseLog() });
  refused(runCli(repo), "no row for version '0.1.0-beta.1'", 'release command');
});

test('Missing version file or log is a reason, not a crash (check.mjs)', () => {
  const repo = createRepo();
  fs.rmSync(path.join(repo.work, 'Version.props'));
  fs.rmSync(path.join(repo.work, 'RELEASES.md'));
  refused(runCli(repo), 'Cannot read Version.props', 'Cannot read RELEASES.md');
});

test('No origin/main is a reason naming the git error (check.mjs)', () => {
  const repo = createRepo();
  repo.git('remote', 'remove', 'origin');
  refused(runCli(repo), 'Cannot tell whether the commit is on main');
});

test('Every reason is printed, not only the first (check.mjs)', () => {
  const repo = createRepo();
  repo.git('checkout', '-b', 'feature');
  fs.writeFileSync(path.join(repo.work, 'extra.txt'), 'x');
  repo.git('add', '.');
  repo.git('commit', '-m', 'Feature work');
  const r = runCli(repo, ['release-2026-10-20']);
  refused(r, 'does not match', 'is not on main', 'names tag');
  assert.ok(r.stderr.split('\n').filter((l) => l.startsWith('- ')).length >= 3);
});

test('Scenario: Release reports its test platform (check.mjs)', () => {
  const repo = createRepo();
  const first = runCli(repo).stdout.split('\n')[0];
  assert.ok(first.startsWith('Platform: '));
  assert.ok(first.includes(os.type()));
  assert.ok(first.includes(os.release()));
  assert.ok(first.includes(os.arch()));
  // Printed on a refusal too.
  assert.equal(runCli(repo, ['bad']).stdout.split('\n')[0], first);
});

// The gate must not silently pass when reached through a path that is not the script's real path.
function assertNotSilent(repo, script) {
  const noArgs = runScript(repo, script, []);
  assert.equal(noArgs.status, 1);
  assert.ok(noArgs.stdout.startsWith('Platform: '));
  assert.ok(noArgs.stderr.includes('Usage: node tools/release/check.mjs <tag>'));
  const ok = runScript(repo, script, [TAG]);
  assert.equal(ok.status, 0, ok.stderr);
  assert.ok(ok.stdout.startsWith('Platform: '));
  assert.ok(ok.stdout.includes(`tag ${TAG}, version 0.1.0-beta.1`));
  const bad = runScript(repo, script, ['bad']);
  assert.equal(bad.status, 1);
  assert.ok(bad.stdout.startsWith('Platform: '));
  assert.ok(bad.stderr.includes('does not match'));
}

test('CLI run through a symlinked directory is not silent (check.mjs)', () => {
  const repo = createRepo();
  const link = path.join(path.dirname(repo.work), 'linked-release');
  fs.symlinkSync(path.dirname(SCRIPT), link, 'junction');
  assertNotSilent(repo, path.join(link, 'check.mjs'));
});

test('CLI run through an unnormalised path is not silent (check.mjs)', () => {
  const repo = createRepo();
  const dir = path.dirname(SCRIPT);
  assertNotSilent(repo, path.join(dir, '..', path.basename(dir), 'check.mjs'));
});

test('Missing or extra argument prints usage and exits 1 (check.mjs)', () => {
  const repo = createRepo();
  for (const args of [[], [TAG, 'extra']]) {
    const r = runCli(repo, args);
    assert.equal(r.status, 1);
    assert.ok(r.stdout.startsWith('Platform: '));
    assert.ok(r.stderr.includes('Usage: node tools/release/check.mjs <tag>'));
  }
});
