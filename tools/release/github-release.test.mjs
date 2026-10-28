import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  isPrerelease, packageIdsFromFiles, releaseBody, runCreate, runPackageIds,
} from './github-release.mjs';

const SCRIPT = path.join(path.dirname(fileURLToPath(import.meta.url)), 'github-release.mjs');
const TAG = 'release-20261028';
const ID = 'Umbraco.Community.Licensing';

const temps = [];
after(() => {
  for (const dir of temps) fs.rmSync(dir, { recursive: true, force: true, maxRetries: 5, retryDelay: 100 });
});
const tempDir = () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'github-release-'));
  temps.push(dir);
  return dir;
};

// ---- pure functions ----

test('Scenario: Page created - body holds the note and a nuget.org link', () => {
  assert.equal(
    releaseBody('Adds package icon', [ID], '0.1.0-beta.5'),
    `Adds package icon\n\n- [${ID} 0.1.0-beta.5](https://www.nuget.org/packages/${ID}/0.1.0-beta.5)`,
  );
});

test('Scenario: One link per published package', () => {
  const body = releaseBody('Note', [ID, `${ID}.Tool`], '0.2.0-beta.1');
  assert.equal(
    body,
    'Note\n\n'
    + `- [${ID} 0.2.0-beta.1](https://www.nuget.org/packages/${ID}/0.2.0-beta.1)\n`
    + `- [${ID}.Tool 0.2.0-beta.1](https://www.nuget.org/packages/${ID}.Tool/0.2.0-beta.1)`,
  );
});

test('release body refuses an empty ID list', () => {
  assert.throws(() => releaseBody('Note', [], '1.0.0'), /No package IDs/);
});

test('Scenario: Release from 1.0 - not a prerelease', () => {
  assert.equal(isPrerelease('1.0.0'), false);
  assert.equal(isPrerelease('2.3.0'), false);
  assert.equal(isPrerelease('0.1.0-beta.5'), true);
});

test('package IDs: dotted ID, sorted, other files ignored', () => {
  const files = [`${ID}.Tool.0.2.0-beta.1.nupkg`, `${ID}.0.2.0-beta.1.nupkg`, 'notes.txt', `${ID}.0.2.0-beta.1.snupkg`];
  assert.deepEqual(packageIdsFromFiles(files, '0.2.0-beta.1'), [ID, `${ID}.Tool`]);
});

test('package IDs: refuses a name not ending .<version>.nupkg', () => {
  assert.throws(() => packageIdsFromFiles([`${ID}.0.1.0-beta.4.nupkg`], '0.1.0-beta.5'), /does not end with '\.0\.1\.0-beta\.5\.nupkg'/);
  assert.throws(() => packageIdsFromFiles(['.0.1.0.nupkg'], '0.1.0'), /does not end with/);
});

test('package IDs: refuses an empty list', () => {
  assert.throws(() => packageIdsFromFiles([], '1.0.0'), /No \.nupkg file/);
  assert.throws(() => packageIdsFromFiles(['readme.md'], '1.0.0'), /No \.nupkg file/);
});

// ---- package-ids command ----

// A git repository holding Version.props, as the workflow's checkout does.
function gitRepo(version) {
  const root = tempDir();
  execFileSync('git', ['init', '-q'], { cwd: root });
  fs.writeFileSync(path.join(root, 'Version.props'),
    `<Project><PropertyGroup><VersionLine>0.1-beta</VersionLine><Version>${version}</Version></PropertyGroup></Project>\n`);
  return root;
}

test('package-ids prints IDs and appends to GITHUB_OUTPUT when set', () => {
  const repo = gitRepo('0.2.0-beta.1');
  const dir = tempDir();
  for (const n of [`${ID}.Tool.0.2.0-beta.1.nupkg`, `${ID}.0.2.0-beta.1.nupkg`, 'other.txt']) fs.writeFileSync(path.join(dir, n), '');
  const outFile = path.join(dir, 'github-output');
  fs.writeFileSync(outFile, 'existing=1\n');

  const withOut = spawnSync(process.execPath, [SCRIPT, 'package-ids', dir], {
    cwd: repo, encoding: 'utf8', env: { ...process.env, GITHUB_OUTPUT: outFile },
  });
  assert.equal(withOut.status, 0, withOut.stderr);
  assert.equal(withOut.stdout.trim(), `${ID} ${ID}.Tool`);
  assert.equal(fs.readFileSync(outFile, 'utf8'), `existing=1\npackage-ids=${ID} ${ID}.Tool\n`);

  const env = { ...process.env };
  delete env.GITHUB_OUTPUT;
  const without = spawnSync(process.execPath, [SCRIPT, 'package-ids', dir], { cwd: repo, encoding: 'utf8', env });
  assert.equal(without.status, 0, without.stderr);
  assert.equal(without.stdout.trim(), `${ID} ${ID}.Tool`);
  assert.equal(fs.readFileSync(outFile, 'utf8'), `existing=1\npackage-ids=${ID} ${ID}.Tool\n`);
});

test('package-ids fails on a file name not matching the Version.props version', () => {
  const repo = gitRepo('0.1.0-beta.5');
  const dir = tempDir();
  fs.writeFileSync(path.join(dir, `${ID}.0.1.0-beta.4.nupkg`), '');
  const r = spawnSync(process.execPath, [SCRIPT, 'package-ids', dir], { cwd: repo, encoding: 'utf8' });
  assert.notEqual(r.status, 0);
  assert.match(r.stderr, /does not end with/);
});

test('package-ids refuses the old two-argument form', () => {
  const repo = gitRepo('1.0.0');
  const r = spawnSync(process.execPath, [SCRIPT, 'package-ids', repo, '1.0.0'], { cwd: repo, encoding: 'utf8' });
  assert.equal(r.status, 1);
  assert.match(r.stderr, /Usage: .*package-ids <folder>\n/);
});

test('package-ids fails when Version.props cannot be read', () => {
  const dir = tempDir();
  fs.writeFileSync(path.join(dir, `${ID}.1.0.0.nupkg`), '');
  assert.throws(() => runPackageIds({ folder: dir, root: tempDir(), env: {}, out: () => {} }),
    /Cannot read Version\.props at the repository root: /);
});

test('runPackageIds returns the IDs for the Version.props version', () => {
  const dir = tempDir();
  fs.writeFileSync(path.join(dir, `${ID}.1.0.0.nupkg`), '');
  const lines = [];
  assert.deepEqual(runPackageIds({ folder: dir, root: gitRepo('1.0.0'), env: {}, out: (l) => lines.push(l) }), [ID]);
  assert.deepEqual(lines, [ID]);
});

// ---- create command ----

function createRoot({ version = '0.1.0-beta.5', rows = [[ '0.1.0-beta.5', TAG, 'Adds package icon' ]] } = {}) {
  const root = tempDir();
  fs.writeFileSync(path.join(root, 'Version.props'),
    `<Project><PropertyGroup><VersionLine>0.1-beta</VersionLine><Version>${version}</Version></PropertyGroup></Project>\n`);
  fs.writeFileSync(path.join(root, 'RELEASES.md'),
    `# Releases\n\n| Version | Tag | Date | What changed |\n|---|---|---|---|\n${rows.map(([v, t, n]) => `| ${v} | ${t} | 2026-10-28 | ${n} |\n`).join('')}`);
  return root;
}

const ENV = { GITHUB_TOKEN: 'secret-token', GITHUB_REPOSITORY: 'carlcod-es/Umbraco.Community.Licensing' };

const response = (status, body) => ({
  status,
  text: async () => (typeof body === 'string' ? body : JSON.stringify(body ?? {})),
  json: async () => body,
});

function fakeFetch(...responses) {
  const calls = [];
  const fn = async (url, init) => {
    calls.push({ url, ...init });
    const next = responses.shift();
    assert.ok(next, `unexpected request ${init.method} ${url}`);
    return next;
  };
  return { fn, calls };
}

test('create: 404 then POST with the expected payload', async () => {
  const root = createRoot();
  const f = fakeFetch(response(404, { message: 'Not Found' }), response(201, { html_url: 'https://github.com/o/r/releases/tag/x' }));
  const lines = [];
  const result = await runCreate({ tag: TAG, ids: ID, root, env: ENV, fetch: f.fn, out: (l) => lines.push(l) });

  assert.equal(result.created, true);
  assert.equal(f.calls.length, 2);
  assert.equal(f.calls[0].method, 'GET');
  assert.equal(f.calls[0].url, `https://api.github.com/repos/${ENV.GITHUB_REPOSITORY}/releases/tags/${TAG}`);
  assert.equal(f.calls[1].method, 'POST');
  assert.equal(f.calls[1].url, `https://api.github.com/repos/${ENV.GITHUB_REPOSITORY}/releases`);
  assert.deepEqual(JSON.parse(f.calls[1].body), {
    tag_name: TAG,
    name: '0.1.0-beta.5',
    body: releaseBody('Adds package icon', [ID], '0.1.0-beta.5'),
    prerelease: true,
    make_latest: 'false',
  });
  assert.equal(f.calls[1].headers.Authorization, 'Bearer secret-token');
  assert.equal(f.calls[1].headers.Accept, 'application/vnd.github+json');
  assert.equal(f.calls[1].headers['X-GitHub-Api-Version'], '2022-11-28');
  assert.ok(f.calls[1].headers['User-Agent']);
  assert.ok(lines.some((l) => l.includes('https://github.com/o/r/releases/tag/x')));
  assert.ok(!lines.join('\n').includes('secret-token'));
});

test('create: release from 1.0 is not a prerelease and is made latest', async () => {
  const root = createRoot({ version: '1.0.0', rows: [['1.0.0', TAG, 'First']] });
  const f = fakeFetch(response(404, {}), response(201, { html_url: 'u' }));
  await runCreate({ tag: TAG, ids: ID, root, env: ENV, fetch: f.fn, out: () => {} });
  const payload = JSON.parse(f.calls[1].body);
  assert.equal(payload.prerelease, false);
  assert.equal(payload.make_latest, 'true');
});

test('create: GITHUB_API_URL is honoured', async () => {
  const root = createRoot();
  const f = fakeFetch(response(200, {}));
  await runCreate({ tag: TAG, ids: ID, root, env: { ...ENV, GITHUB_API_URL: 'https://ghe.example/api/v3/' }, fetch: f.fn, out: () => {} });
  assert.equal(f.calls[0].url, `https://ghe.example/api/v3/repos/${ENV.GITHUB_REPOSITORY}/releases/tags/${TAG}`);
});

test('Scenario: Page already exists - no POST, succeeds', async () => {
  const root = createRoot();
  const f = fakeFetch(response(200, { id: 1 }));
  const lines = [];
  const result = await runCreate({ tag: TAG, ids: ID, root, env: ENV, fetch: f.fn, out: (l) => lines.push(l) });
  assert.equal(result.created, false);
  assert.equal(f.calls.length, 1);
  assert.match(lines.join('\n'), /already exists.*unchanged/);
});

test('Scenario: Release log note missing - fails naming the version, no request', async () => {
  const root = createRoot({ rows: [['0.1.0-beta.4', 'release-20261020', 'Older']] });
  const f = fakeFetch();
  await assert.rejects(
    runCreate({ tag: TAG, ids: ID, root, env: ENV, fetch: f.fn, out: () => {} }),
    /no row for version '0\.1\.0-beta\.5'/,
  );
  assert.equal(f.calls.length, 0);
});

test('Scenario: Page creation fails after publishing - reported with status and message', async () => {
  const root = createRoot();
  const f = fakeFetch(response(403, { message: 'Resource not accessible by integration' }));
  await assert.rejects(
    runCreate({ tag: TAG, ids: ID, root, env: ENV, fetch: f.fn, out: () => {} }),
    (e) => /403/.test(e.message) && /Resource not accessible by integration/.test(e.message) && !e.message.includes('secret-token'),
  );
  assert.equal(f.calls.length, 1);
});

test('create: 403 on POST is reported with status and message; non-JSON falls back to raw text', async () => {
  const root = createRoot();
  await assert.rejects(
    runCreate({ tag: TAG, ids: ID, root, env: ENV, fetch: fakeFetch(response(404, {}), response(403, { message: 'Forbidden thing' })).fn, out: () => {} }),
    /403 Forbidden thing/,
  );
  await assert.rejects(
    runCreate({ tag: TAG, ids: ID, root, env: ENV, fetch: fakeFetch(response(404, {}), response(500, 'Server exploded')).fn, out: () => {} }),
    /500 Server exploded/,
  );
});

test('create: refuses a tag that differs from the log row, no request', async () => {
  const root = createRoot();
  const f = fakeFetch();
  await assert.rejects(
    runCreate({ tag: 'release-20261029', ids: ID, root, env: ENV, fetch: f.fn, out: () => {} }),
    /The release log row for version 0\.1\.0-beta\.5 names tag release-20261028, not release-20261029\./,
  );
  assert.equal(f.calls.length, 0);
});

test('create: fails when Version.props cannot be read', async () => {
  await assert.rejects(
    runCreate({ tag: TAG, ids: ID, root: tempDir(), env: ENV, fetch: fakeFetch().fn, out: () => {} }),
    /Cannot read Version\.props at the repository root: /,
  );
});

test('create: refuses missing token, bad repository and empty ID list', async () => {
  const root = createRoot();
  const f = fakeFetch();
  await assert.rejects(runCreate({ tag: TAG, ids: ID, root, env: { GITHUB_REPOSITORY: ENV.GITHUB_REPOSITORY }, fetch: f.fn, out: () => {} }), /GITHUB_TOKEN/);
  await assert.rejects(runCreate({ tag: TAG, ids: ID, root, env: { GITHUB_TOKEN: 't' }, fetch: f.fn, out: () => {} }), /GITHUB_REPOSITORY/);
  await assert.rejects(runCreate({ tag: TAG, ids: '  ', root, env: ENV, fetch: f.fn, out: () => {} }), /No package IDs/);
  assert.equal(f.calls.length, 0);
});

test('create: several space-separated IDs give one link each', async () => {
  const root = createRoot();
  const f = fakeFetch(response(404, {}), response(201, { html_url: 'u' }));
  await runCreate({ tag: TAG, ids: `${ID} ${ID}.Tool`, root, env: ENV, fetch: f.fn, out: () => {} });
  const { body } = JSON.parse(f.calls[1].body);
  assert.equal(body.match(/nuget\.org/g).length, 2);
});
