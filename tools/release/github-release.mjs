// GitHub Release page for a published release.
//   node tools/release/github-release.mjs package-ids <folder> <version>
//   node tools/release/github-release.mjs create <tag> <ids>
// See docs/adrs/0016-github-release-job.md.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { findRow, parseLog, readVersionFile } from './release-rules.mjs';

// ---- Pure functions ----

// Package IDs from `.nupkg` file names; other files are ignored. Case is kept as the file name has it.
export function packageIdsFromFiles(fileNames, version) {
  const suffix = `.${version}.nupkg`;
  const packages = fileNames.filter((n) => n.endsWith('.nupkg'));
  if (packages.length === 0) throw new Error(`No .nupkg file found for version '${version}'.`);
  const ids = packages.map((name) => {
    if (!name.endsWith(suffix) || name.length === suffix.length) {
      throw new Error(`Package file '${name}' does not end with '${suffix}'.`);
    }
    return name.slice(0, name.length - suffix.length);
  });
  return ids.sort();
}

export function releaseBody(note, ids, version) {
  if (!Array.isArray(ids) || ids.length === 0) throw new Error('No package IDs given.');
  const links = ids.map((id) => `- [${id} ${version}](https://www.nuget.org/packages/${id}/${version})`);
  return `${note}\n\n${links.join('\n')}`;
}

// Below 1.0 every version is a prerelease (the release checks enforce it), so the prerelease part decides.
export function isPrerelease(version) {
  return version.includes('-');
}

export function splitIds(ids) {
  return String(ids ?? '').split(/\s+/).filter((s) => s !== '');
}

// ---- package-ids ----

export function runPackageIds({ folder, version, env = process.env, out = console.log }) {
  const ids = packageIdsFromFiles(fs.readdirSync(folder), version);
  const joined = ids.join(' ');
  out(joined);
  if (env.GITHUB_OUTPUT) fs.appendFileSync(env.GITHUB_OUTPUT, `package-ids=${joined}\n`);
  return ids;
}

// ---- create ----

async function failure(res, what) {
  const text = await res.text();
  let message = text;
  try {
    const m = JSON.parse(text)?.message;
    if (typeof m === 'string' && m !== '') message = m;
  } catch {
    // not JSON: keep the raw text
  }
  return new Error(`${what} failed: HTTP ${res.status}${message ? ` ${message}` : ''}`);
}

export async function runCreate({ tag, ids, root, env = process.env, fetch = globalThis.fetch, out = console.log }) {
  const idList = splitIds(ids);
  if (idList.length === 0) throw new Error('No package IDs given.');

  const { version } = readVersionFile(fs.readFileSync(path.join(root, 'Version.props'), 'utf8'));
  const { rows } = parseLog(fs.readFileSync(path.join(root, 'RELEASES.md'), 'utf8'));
  const row = findRow(rows, version);
  if (!row) throw new Error(`The release log has no row for version '${version}'.`);

  const token = env.GITHUB_TOKEN;
  if (!token) throw new Error('GITHUB_TOKEN is not set.');
  const repo = env.GITHUB_REPOSITORY;
  if (!repo || !/^[^/\s]+\/[^/\s]+$/.test(repo)) throw new Error('GITHUB_REPOSITORY is not set or is not of the form owner/repo.');
  const api = (env.GITHUB_API_URL || 'https://api.github.com').replace(/\/+$/, '');

  const headers = {
    Authorization: `Bearer ${token}`,
    Accept: 'application/vnd.github+json',
    'X-GitHub-Api-Version': '2022-11-28',
    'User-Agent': 'Umbraco.Community.Licensing-release',
  };

  const get = await fetch(`${api}/repos/${repo}/releases/tags/${encodeURIComponent(tag)}`, { method: 'GET', headers });
  if (get.status === 200) {
    out(`Release for tag ${tag} already exists; left unchanged.`);
    return { created: false };
  }
  if (get.status !== 404) throw await failure(get, `Looking up the Release for tag ${tag}`);

  const prerelease = isPrerelease(version);
  const payload = {
    tag_name: tag,
    name: version,
    body: releaseBody(row.note, idList, version),
    prerelease,
    make_latest: prerelease ? 'false' : 'true',
  };
  const post = await fetch(`${api}/repos/${repo}/releases`, {
    method: 'POST',
    headers: { ...headers, 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  if (post.status !== 201) throw await failure(post, `Creating the Release for tag ${tag}`);
  const created = await post.json();
  out(`Created Release ${version}: ${created.html_url}`);
  return { created: true, url: created.html_url };
}

// ---- CLI ----

function repoRoot(cwd) {
  return execFileSync('git', ['rev-parse', '--show-toplevel'], { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
}

async function main(argv) {
  const [command, ...args] = argv;
  try {
    if (command === 'package-ids' && args.length === 2) {
      runPackageIds({ folder: args[0], version: args[1] });
      return 0;
    }
    if (command === 'create' && args.length === 2) {
      await runCreate({ tag: args[0], ids: args[1], root: repoRoot(process.cwd()) });
      return 0;
    }
    console.error('Usage: node tools/release/github-release.mjs package-ids <folder> <version>\n       node tools/release/github-release.mjs create <tag> <ids>');
    return 1;
  } catch (e) {
    console.error(e.message);
    return 1;
  }
}

function isMainModule() {
  if (!process.argv[1]) return false;
  const real = (p) => fs.realpathSync.native(p);
  return real(fileURLToPath(import.meta.url)) === real(path.resolve(process.argv[1]));
}

if (isMainModule()) {
  process.exitCode = await main(process.argv.slice(2));
}
