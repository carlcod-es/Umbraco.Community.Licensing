import { test, beforeEach, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runRelease } from './release.mjs';

const releaseScript = path.join(path.dirname(fileURLToPath(import.meta.url)), 'release.mjs');

const HEADER = '| Version | Tag | Date | What changed |\n|---|---|---|---|\n';
const propsText = (line, version) =>
  `<Project>\n  <PropertyGroup>\n    <VersionLine>${line}</VersionLine>\n    <Version>${version}</Version>\n  </PropertyGroup>\n</Project>\n`;
const logText = (...rows) => `# Releases\n\nIntro.\n\n${HEADER}${rows.map((r) => `${r}\n`).join('')}`;
const logRow = (v, t, d, n) => `| ${v} | ${t} | ${d} | ${n} |`;

const clock = (...args) => () => new Date(...args);
const CLOCK = clock(2026, 9, 20, 10, 0, 0);

// Every git call, including those made by runRelease, must ignore the user's configuration.
const ENV_KEYS = ['GIT_CONFIG_GLOBAL', 'GIT_AUTHOR_NAME', 'GIT_AUTHOR_EMAIL', 'GIT_COMMITTER_NAME', 'GIT_COMMITTER_EMAIL'];
// Inherited repository-location variables (e.g. when run from a git hook) would point git at the outer repository.
const CLEARED_KEYS = ['GIT_DIR', 'GIT_INDEX_FILE', 'GIT_WORK_TREE', 'GIT_OBJECT_DIRECTORY', 'GIT_ALTERNATE_OBJECT_DIRECTORIES', 'GIT_COMMON_DIR'];
let saved;
let root;

beforeEach(() => {
  saved = Object.fromEntries([...ENV_KEYS, ...CLEARED_KEYS].map((k) => [k, process.env[k]]));
  for (const k of CLEARED_KEYS) delete process.env[k];
  root = fs.mkdtempSync(path.join(os.tmpdir(), 'release-test-'));
  fs.writeFileSync(path.join(root, 'gitconfig'), '');
  process.env.GIT_CONFIG_GLOBAL = path.join(root, 'gitconfig');
  process.env.GIT_AUTHOR_NAME = process.env.GIT_COMMITTER_NAME = 'Test';
  process.env.GIT_AUTHOR_EMAIL = process.env.GIT_COMMITTER_EMAIL = 'test@example.com';
});

afterEach(() => {
  for (const k of [...ENV_KEYS, ...CLEARED_KEYS]) {
    if (saved[k] === undefined) delete process.env[k]; else process.env[k] = saved[k];
  }
  fs.rmSync(root, { recursive: true, force: true, maxRetries: 5 });
});

const git = (cwd, ...args) => execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();

function clone(remote, name) {
  const dir = path.join(root, name);
  git(root, 'clone', '-q', remote, dir);
  git(dir, 'config', 'core.autocrlf', 'false');
  return dir;
}

// Bare remote plus the releasing clone, seeded with one commit on main.
function setup({ line = '0.1-beta', version = '0.1.0-beta.1', rows = [] } = {}) {
  const remote = path.join(root, 'remote.git');
  git(root, 'init', '-q', '--bare', '--initial-branch=main', remote);
  const dir = clone(remote, 'work');
  fs.writeFileSync(path.join(dir, 'Version.props'), propsText(line, version));
  fs.writeFileSync(path.join(dir, 'RELEASES.md'), logText(...rows));
  fs.writeFileSync(path.join(dir, 'other.txt'), 'other\n');
  git(dir, 'add', '.');
  git(dir, 'commit', '-q', '-m', 'Seed');
  git(dir, 'push', '-q', 'origin', 'main');
  return { remote, dir };
}

const lines = (s) => s.split('\n').filter(Boolean);

function snapshot({ remote, dir }) {
  return {
    props: fs.readFileSync(path.join(dir, 'Version.props'), 'utf8'),
    log: fs.readFileSync(path.join(dir, 'RELEASES.md'), 'utf8'),
    localMain: git(dir, 'rev-parse', 'main'),
    remoteMain: git(remote, 'rev-parse', 'main'),
    commits: git(dir, 'rev-list', '--count', 'main'),
    localTags: lines(git(dir, 'tag', '--list')),
    remoteTags: lines(git(remote, 'tag', '--list')),
    branch: git(dir, 'branch', '--show-current'),
    status: git(dir, 'status', '--porcelain'),
  };
}

function script(answers) {
  const asked = [];
  const queue = [...answers];
  const printed = [];
  return {
    asked,
    printed,
    prompt: async (q) => {
      asked.push(q);
      assert.ok(queue.length > 0, `unexpected question: ${q}`);
      return queue.shift();
    },
    print: (l) => printed.push(l),
  };
}

const run = (env, answers, now = CLOCK) => {
  const s = script(answers);
  return { s, done: runRelease({ cwd: env.dir, prompt: s.prompt, now, print: s.print }) };
};

async function refused(env, answers, pattern, now = CLOCK) {
  const before = snapshot(env);
  const { s, done } = run(env, answers, now);
  await assert.rejects(done, (e) => { assert.match(e.message, pattern); return true; });
  assert.deepEqual(snapshot(env), before, 'refusal must change nothing');
  return s;
}

test('Scenario: Release started', async () => {
  const env = setup({ rows: [
    logRow('0.1.0-beta.2', 'release-20261019', '2026-10-19', 'Second'),
    logRow('0.1.0-beta.1', 'release-20261018', '2026-10-18', 'First'),
  ] });
  const before = snapshot(env);
  const { s, done } = run(env, ['Adds reissue', 'y']);
  const result = await done;

  const after = snapshot(env);
  assert.equal(result.outcome, 'released');
  assert.equal(result.version, '0.1.0-beta.3');
  assert.equal(result.tag, 'release-20261020');
  assert.equal(result.commit, after.remoteMain);
  assert.equal(after.props, propsText('0.1-beta', '0.1.0-beta.3'));
  assert.equal(after.log, before.log.replace(HEADER, `${HEADER}${logRow('0.1.0-beta.3', 'release-20261020', '2026-10-20', 'Adds reissue')}\n`));
  assert.equal(after.commits, String(Number(before.commits) + 1));
  assert.equal(after.localMain, after.remoteMain);
  assert.equal(git(env.remote, 'rev-parse', 'main~1'), before.remoteMain);
  assert.deepEqual(lines(git(env.remote, 'show', '--name-only', '--format=', 'main')).sort(), ['RELEASES.md', 'Version.props']);
  assert.equal(git(env.remote, 'log', '-1', '--format=%s', 'main'), 'Release 0.1.0-beta.3');
  assert.deepEqual(after.remoteTags, ['release-20261020']);
  assert.deepEqual(after.localTags, ['release-20261020']);
  assert.equal(git(env.remote, 'rev-parse', 'release-20261020^{commit}'), after.remoteMain);
  assert.equal(git(env.remote, 'cat-file', '-t', 'release-20261020'), 'commit', 'tag is lightweight');
  assert.equal(after.status, '');
  assert.equal(s.asked.length, 2);
  assert.ok(s.printed.join('\n').includes('Adds reissue'));
});

test('Release started: first release changes only RELEASES.md in the commit', async () => {
  const env = setup();
  const before = snapshot(env);
  const result = await run(env, ['First beta', 'yes']).done;
  const after = snapshot(env);
  assert.equal(result.version, '0.1.0-beta.1');
  assert.equal(after.props, before.props);
  assert.deepEqual(lines(git(env.remote, 'show', '--name-only', '--format=', 'main')), ['RELEASES.md']);
  assert.ok(after.log.includes(logRow('0.1.0-beta.1', 'release-20261020', '2026-10-20', 'First beta')));
  assert.deepEqual(after.remoteTags, ['release-20261020']);
});

test('Release started: tag and row date agree at 23:59:59 local', async () => {
  const env = setup();
  await run(env, ['Late', 'y'], clock(2026, 9, 20, 23, 59, 59)).done;
  const after = snapshot(env);
  assert.deepEqual(after.remoteTags, ['release-20261020']);
  assert.ok(after.log.includes('| release-20261020 | 2026-10-20 |'));
});

test('Release started: a clock that crosses midnight during the prompts gives the tag and row the same day', async () => {
  const env = setup();
  let calls = 0;
  const now = () => (calls++ === 0 ? new Date(2026, 9, 20, 23, 59, 59) : new Date(2026, 9, 21, 0, 0, 1));
  const result = await run(env, ['Late', 'y'], now).done;
  assert.equal(result.tag, 'release-20261020');
  assert.ok(snapshot(env).log.includes('| release-20261020 | 2026-10-20 |'));
});

test('Release started: run from a subdirectory of the clone', async () => {
  const env = setup();
  const sub = path.join(env.dir, 'sub', 'deeper');
  fs.mkdirSync(sub, { recursive: true });
  const s = script(['Sub', 'y']);
  const result = await runRelease({ cwd: sub, prompt: s.prompt, now: CLOCK, print: s.print });
  const after = snapshot(env);
  assert.equal(result.outcome, 'released');
  assert.equal(after.props, propsText('0.1-beta', '0.1.0-beta.1'));
  assert.ok(after.log.includes(logRow('0.1.0-beta.1', 'release-20261020', '2026-10-20', 'Sub')));
  assert.equal(after.localMain, after.remoteMain);
  assert.deepEqual(after.remoteTags, ['release-20261020']);
  assert.equal(after.status, '');
});

test('Release started: only the two files are committed, other changes stay in the working tree', async () => {
  const env = setup();
  fs.writeFileSync(path.join(env.dir, 'other.txt'), 'edited\n');
  fs.writeFileSync(path.join(env.dir, 'untracked.txt'), 'x\n');
  await run(env, ['Note', 'y']).done;
  assert.deepEqual(lines(git(env.remote, 'show', '--name-only', '--format=', 'main')), ['RELEASES.md']);
  assert.equal(fs.readFileSync(path.join(env.dir, 'other.txt'), 'utf8'), 'edited\n');
  assert.match(git(env.dir, 'status', '--porcelain'), /other\.txt/);
});

test('Scenario: Not on main', async () => {
  const env = setup();
  git(env.dir, 'checkout', '-q', '-b', 'feature/x');
  const s = await refused(env, [], /feature\/x/);
  assert.deepEqual(s.asked, []);
});

test('Not on main: detached HEAD is refused', async () => {
  const env = setup();
  git(env.dir, 'checkout', '-q', '--detach');
  await refused(env, [], /detached/);
});

test('Scenario: Out of step with the remote (ahead)', async () => {
  const env = setup();
  fs.writeFileSync(path.join(env.dir, 'other.txt'), 'local\n');
  git(env.dir, 'commit', '-q', '-am', 'Local only');
  await refused(env, [], /local main has 1 commit\(s\) the remote lacks\./);
});

test('Scenario: Out of step with the remote (behind)', async () => {
  const env = setup();
  const other = clone(env.remote, 'other');
  fs.writeFileSync(path.join(other, 'other.txt'), 'remote\n');
  git(other, 'commit', '-q', '-am', 'Remote only');
  git(other, 'push', '-q', 'origin', 'main');
  await refused(env, [], /local main lacks 1 commit\(s\) the remote has\./);
});

test('Out of step with the remote (both)', async () => {
  const env = setup();
  const other = clone(env.remote, 'other');
  fs.writeFileSync(path.join(other, 'other.txt'), 'remote\n');
  git(other, 'commit', '-q', '-am', 'Remote only');
  git(other, 'push', '-q', 'origin', 'main');
  fs.writeFileSync(path.join(env.dir, 'other.txt'), 'local\n');
  git(env.dir, 'commit', '-q', '-am', 'Local only');
  await refused(env, [], /has 1 commit\(s\) the remote lacks and lacks 1 commit\(s\) the remote has/);
});

test('Uncommitted changes to Version.props or RELEASES.md are refused', async () => {
  for (const file of ['Version.props', 'RELEASES.md']) {
    const env = setup();
    fs.appendFileSync(path.join(env.dir, file), '\n');
    await refused(env, [], new RegExp(`Uncommitted changes in ${file.replace('.', '\\.')}`));
    fs.rmSync(path.join(root, 'work'), { recursive: true, force: true, maxRetries: 5 });
    fs.rmSync(path.join(root, 'remote.git'), { recursive: true, force: true, maxRetries: 5 });
  }
});

test('Scenario: Empty note', async () => {
  const env = setup();
  const { s, done } = run(env, ['', '   ', '  Real note  ', 'y']);
  const result = await done;
  assert.equal(result.outcome, 'released');
  assert.equal(s.asked.filter((q) => q.startsWith('What changed')).length, 3);
  assert.equal(s.asked.length, 4);
  assert.ok(snapshot(env).log.includes('| Real note |'));
});

test('Empty note: a note with a line break is asked again', async () => {
  const env = setup();
  const { s, done } = run(env, ['first line\nsecond line', 'a\r\nb', 'One line', 'y']);
  const result = await done;
  assert.equal(result.outcome, 'released');
  assert.equal(s.asked.filter((q) => q.startsWith('What changed')).length, 3);
  assert.ok(snapshot(env).log.includes('| One line |'));
});

test('A prompt that rejects before the write changes nothing', async () => {
  for (const failAt of [0, 1]) {
    const env = setup();
    const before = snapshot(env);
    let n = 0;
    const prompt = async () => {
      if (n++ === failAt) throw new Error('Cancelled. Nothing changed.');
      return 'Note';
    };
    await assert.rejects(runRelease({ cwd: env.dir, prompt, now: CLOCK, print: () => {} }), /Cancelled/);
    assert.deepEqual(snapshot(env), before);
    fs.rmSync(path.join(root, 'work'), { recursive: true, force: true, maxRetries: 5 });
    fs.rmSync(path.join(root, 'remote.git'), { recursive: true, force: true, maxRetries: 5 });
  }
});

test('Scenario: Confirmation declined', async () => {
  for (const answer of ['n', '', 'maybe']) {
    const env = setup();
    const before = snapshot(env);
    const result = await run(env, ['Note', answer]).done;
    assert.deepEqual(result, { outcome: 'declined' });
    assert.deepEqual(snapshot(env), before);
    fs.rmSync(path.join(root, 'work'), { recursive: true, force: true, maxRetries: 5 });
    fs.rmSync(path.join(root, 'remote.git'), { recursive: true, force: true, maxRetries: 5 });
  }
});

test('Scenario: Invalid line', async () => {
  for (const line of ['1.0', '0.1', '1-beta', 'abc']) {
    const env = setup({ line });
    const s = await refused(env, [], /is not valid\. Accepted forms/);
    assert.deepEqual(s.asked, []);
    fs.rmSync(path.join(root, 'work'), { recursive: true, force: true, maxRetries: 5 });
    fs.rmSync(path.join(root, 'remote.git'), { recursive: true, force: true, maxRetries: 5 });
  }
});

test('Invalid line: the refusal names the line', async () => {
  const env = setup({ line: '1.0' });
  await refused(env, [], /Version line '1\.0'/);
});

test('Scenario: Third release of the day', async () => {
  const env = setup();
  const other = clone(env.remote, 'other');
  git(other, 'tag', 'release-20261020');
  git(other, 'tag', 'release-20261020-2');
  git(other, 'push', '-q', 'origin', 'release-20261020', 'release-20261020-2');
  assert.deepEqual(lines(git(env.dir, 'tag', '--list')), [], 'releasing clone has not fetched');

  const result = await run(env, ['Third', 'y']).done;
  assert.equal(result.tag, 'release-20261020-3');
  const after = snapshot(env);
  assert.deepEqual(after.remoteTags.sort(), ['release-20261020', 'release-20261020-2', 'release-20261020-3']);
  assert.equal(git(env.remote, 'rev-parse', 'release-20261020-3^{commit}'), after.remoteMain);
  assert.ok(after.log.includes('| release-20261020-3 | 2026-10-20 |'));
});

test('Scenario: Tag after a failed attempt counts local-only tags', async () => {
  const env = setup();
  git(env.dir, 'tag', 'release-20261020');
  const result = await run(env, ['Again', 'y']).done;
  assert.equal(result.tag, 'release-20261020-2');
});

test('Scenario: No person present', () => {
  const env = setup();
  const before = snapshot(env);
  const r = spawnSync(process.execPath, [releaseScript], { cwd: env.dir, stdio: 'pipe', input: '', encoding: 'utf8' });
  assert.notEqual(r.status, 0);
  assert.match(r.stderr, /No person present/);
  assert.match(r.stderr, /only by Carl/);
  assert.deepEqual(snapshot(env), before);
});

// The CLI must still run when reached through a symlink or an unnormalised path.
function assertRefusesVia(scriptPath) {
  const env = setup();
  const before = snapshot(env);
  const r = spawnSync(process.execPath, [scriptPath], { cwd: env.dir, stdio: 'pipe', input: '', encoding: 'utf8' });
  assert.equal(r.status, 1, r.stderr);
  assert.match(r.stderr, /No person present/);
  assert.deepEqual(snapshot(env), before);
}

test('Scenario: No person present, script reached through a symlinked directory', () => {
  const link = path.join(root, 'linked');
  fs.symlinkSync(path.dirname(releaseScript), link, 'junction');
  assertRefusesVia(path.join(link, 'release.mjs'));
});

test('Scenario: No person present, script reached through an unnormalised path', () => {
  const dir = path.dirname(releaseScript);
  // Concatenated: path.join would collapse the ".." and prove nothing.
  const unnormalised = `${dir}${path.sep}..${path.sep}${path.basename(dir)}${path.sep}release.mjs`;
  assert.ok(unnormalised.includes('..'));
  assertRefusesVia(unnormalised);
});

function rejectRefs(remote, pattern) {
  const hook = path.join(remote, 'hooks', 'pre-receive');
  fs.writeFileSync(hook,
    `#!/bin/sh\nwhile read old new ref; do\n  case "$ref" in\n    ${pattern}) echo "rejected by test hook: $ref" >&2; exit 1;;\n  esac\ndone\nexit 0\n`);
  fs.chmodSync(hook, 0o755);
}

test('Failure after the commit: the remote rejects the tag push', async () => {
  const env = setup();
  const before = snapshot(env);
  rejectRefs(env.remote, 'refs/tags/*');
  const { done } = run(env, ['Note', 'y']);
  let message;
  await assert.rejects(done, (e) => { message = e.message; return true; });

  const after = snapshot(env);
  assert.deepEqual(after.remoteTags, []);
  assert.deepEqual(after.localTags, ['release-20261020']);
  assert.equal(after.remoteMain, after.localMain);
  assert.notEqual(after.remoteMain, before.remoteMain);
  assert.equal(git(env.dir, 'rev-parse', 'release-20261020^{commit}'), after.localMain);

  assert.ok(message.includes(`release commit ${after.localMain}`));
  assert.match(message, /main pushed to origin: yes/);
  assert.match(message, /tag release-20261020 created locally: yes/);
  assert.match(message, /nothing is published/);
  assert.match(message, /rejected by test hook/);
  const commands = message.split('\n').filter((l) => l.startsWith('  git '));
  assert.deepEqual(commands, ['  git push origin release-20261020']);
  assert.ok(!/&&|--force|\s-f\s/.test(message));
});

test('Failure after the commit: the remote rejects the push of main', async () => {
  const env = setup();
  const before = snapshot(env);
  rejectRefs(env.remote, 'refs/heads/main');
  const { done } = run(env, ['Note', 'y']);
  let message;
  await assert.rejects(done, (e) => { message = e.message; return true; });

  const after = snapshot(env);
  assert.equal(after.remoteMain, before.remoteMain);
  assert.notEqual(after.localMain, before.localMain);
  assert.deepEqual(after.localTags, []);
  assert.deepEqual(after.remoteTags, []);
  assert.match(message, /main pushed to origin: no/);
  assert.match(message, /tag release-20261020 created locally: no/);
  const commands = message.split('\n').filter((l) => l.startsWith('  git '));
  assert.deepEqual(commands, [
    '  git push origin main',
    `  git tag release-20261020 ${after.localMain}`,
    '  git push origin release-20261020',
  ]);
});

test('Failure at the commit: the files stay written and the message says how to discard them', async () => {
  const env = setup();
  const hook = path.join(env.dir, '.git', 'hooks', 'pre-commit');
  fs.writeFileSync(hook, '#!/bin/sh\necho "no commits today" >&2\nexit 1\n');
  fs.chmodSync(hook, 0o755);
  const before = snapshot(env);
  const { done } = run(env, ['Note', 'y']);
  await assert.rejects(done, /written but not committed[\s\S]*git checkout -- Version\.props RELEASES\.md/);
  const after = snapshot(env);
  assert.equal(after.localMain, before.localMain);
  assert.equal(after.remoteMain, before.remoteMain);
  assert.deepEqual(after.localTags, []);
  assert.deepEqual(after.remoteTags, []);
  git(env.dir, 'checkout', '--', 'Version.props', 'RELEASES.md');
  assert.deepEqual(snapshot(env), before);
});
