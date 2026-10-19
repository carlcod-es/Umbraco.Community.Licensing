// Pure release rules: no I/O, no git, no clock. See docs/adrs/0009-release-command-version-file-and-log.md.

const BETA_LINE = /^0\.([1-9]\d*)-beta$/;
const RELEASE_LINE = /^([1-9]\d*)$/;
const BETA_VERSION = /^0\.([1-9]\d*)\.0-beta\.([1-9]\d*)$/;
const RELEASE_VERSION = /^([1-9]\d*)\.(0|[1-9]\d*)\.0$/;
const ANY_VERSION = /^(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+)?$/;

// ---- Version line and version ----

export function parseLine(line) {
  if (typeof line !== 'string') return null;
  let m = BETA_LINE.exec(line);
  if (m) return { kind: 'beta', minor: Number(m[1]) };
  m = RELEASE_LINE.exec(line);
  if (m) return { kind: 'release', major: Number(m[1]) };
  return null;
}

export function invalidLineMessage(line) {
  return `Version line '${line}' is not valid. Accepted forms: '0.M-beta' with M of 1 or more, or 'X' with X of 1 or more.`;
}

export function nextVersion(line, logVersions) {
  const parsed = parseLine(line);
  if (!parsed) throw new Error(invalidLineMessage(line));
  let highest = null;
  for (const version of logVersions) {
    let m;
    if (parsed.kind === 'beta') {
      m = BETA_VERSION.exec(version);
      if (!m || Number(m[1]) !== parsed.minor) continue;
      highest = Math.max(highest ?? 0, Number(m[2]));
    } else {
      m = RELEASE_VERSION.exec(version);
      if (!m || Number(m[1]) !== parsed.major) continue;
      highest = Math.max(highest ?? 0, Number(m[2]));
    }
  }
  if (parsed.kind === 'beta') return `0.${parsed.minor}.0-beta.${(highest ?? 0) + 1}`;
  return `${parsed.major}.${highest === null ? 0 : highest + 1}.0`;
}

export function checkVersionAgainstLine(version, line) {
  const reasons = [];
  const parsed = parseLine(line);
  if (!parsed) reasons.push(invalidLineMessage(line));

  const m = ANY_VERSION.exec(version);
  if (!m) {
    reasons.push(`Version '${version}' (version line '${line}') is not of the form '0.M.0-beta.N' or 'X.N.0'.`);
    return reasons;
  }
  const major = Number(m[1]);
  const prerelease = m[4] !== undefined;
  if (major === 0 && !prerelease) {
    reasons.push(`Version '${version}' is below 1.0 but is not a prerelease (version line '${line}').`);
    return reasons;
  }
  if (major >= 1 && prerelease) {
    reasons.push(`Version '${version}' is 1.0 or above but is a prerelease (version line '${line}').`);
    return reasons;
  }
  if (!BETA_VERSION.test(version) && !RELEASE_VERSION.test(version)) {
    reasons.push(`Version '${version}' (version line '${line}') is not of the form '0.M.0-beta.N' or 'X.N.0'.`);
    return reasons;
  }
  if (parsed) {
    const bm = BETA_VERSION.exec(version);
    const rm = RELEASE_VERSION.exec(version);
    const belongs = parsed.kind === 'beta'
      ? bm !== null && Number(bm[1]) === parsed.minor
      : rm !== null && Number(rm[1]) === parsed.major;
    if (!belongs) reasons.push(`Version '${version}' does not belong to version line '${line}'.`);
  }
  return reasons;
}

// ---- Tag ----

const TAG = /^release-(\d{4})(\d{2})(\d{2})(?:-([2-9]|[1-9]\d+))?$/;

export function isValidTag(tag) {
  if (typeof tag !== 'string') return false;
  const m = TAG.exec(tag);
  if (!m) return false;
  const [year, month, day] = [Number(m[1]), Number(m[2]), Number(m[3])];
  if (year < 1) return false;
  const d = new Date(Date.UTC(2000, month - 1, day));
  d.setUTCFullYear(year);
  return d.getUTCFullYear() === year && d.getUTCMonth() === month - 1 && d.getUTCDate() === day;
}

export function tagFormatMessage(tag) {
  return `Tag '${tag}' does not match 'release-yyyymmdd' (a real date) with an optional '-N' suffix where N is 2 or more.`;
}

const pad = (n, w) => String(n).padStart(w, '0');

// yyyy-mm-dd from the Date's local fields; the tag and the log row both use it.
export function localDate(date) {
  return `${pad(date.getFullYear(), 4)}-${pad(date.getMonth() + 1, 2)}-${pad(date.getDate(), 2)}`;
}

export function nextTag(date, existingTags) {
  const base = `release-${localDate(date).replaceAll('-', '')}`;
  const used = new Set();
  let baseUsed = false;
  for (const tag of existingTags) {
    if (tag === base) { baseUsed = true; continue; }
    if (tag.startsWith(`${base}-`)) {
      const suffix = tag.slice(base.length + 1);
      if (/^([2-9]|[1-9]\d+)$/.test(suffix)) used.add(Number(suffix));
    }
  }
  if (!baseUsed && used.size === 0) return base;
  let n = 2;
  while (used.has(n)) n++;
  return `${base}-${n}`;
}

// ---- Release log ----

const LOG_HEADER = ['Version', 'Tag', 'Date', 'What changed'];

// Lines with their own endings; concatenating every `raw` gives the text back exactly.
function splitLines(text) {
  const lines = [];
  let start = 0;
  while (start < text.length) {
    const nl = text.indexOf('\n', start);
    const end = nl === -1 ? text.length : nl + 1;
    const raw = text.slice(start, end);
    const eol = raw.endsWith('\r\n') ? '\r\n' : raw.endsWith('\n') ? '\n' : '';
    lines.push({ raw, content: raw.slice(0, raw.length - eol.length), eol, offset: start });
    start = end;
  }
  return lines;
}

function splitCells(content, lineNo) {
  const trimmed = content.trim();
  if (!trimmed.startsWith('|')) throw new Error(`Release log line ${lineNo} is not a table row: ${content}`);
  const cells = [];
  let cur = '';
  for (let i = 1; i < trimmed.length; i++) {
    const c = trimmed[i];
    if (c === '\\' && trimmed[i + 1] === '|') { cur += '|'; i++; }
    else if (c === '|') { cells.push(cur); cur = ''; }
    else cur += c;
  }
  if (cur.trim() !== '') cells.push(cur);
  return cells.map((c) => c.trim());
}

function locateTable(text) {
  const lines = splitLines(text);
  const headerIdx = lines.findIndex((l) => l.content.trimStart().startsWith('|'));
  if (headerIdx === -1) throw new Error('Release log has no table.');
  const header = splitCells(lines[headerIdx].content, headerIdx + 1);
  if (header.length !== 4 || header.some((h, i) => h !== LOG_HEADER[i])) {
    throw new Error(`Release log header on line ${headerIdx + 1} must be '| ${LOG_HEADER.join(' | ')} |': ${lines[headerIdx].content}`);
  }
  const sep = lines[headerIdx + 1];
  const sepOk = sep !== undefined
    && /^\s*\|(\s*:?-+:?\s*\|){3}\s*:?-+:?\s*\|?\s*$/.test(sep.content);
  if (!sepOk) {
    throw new Error(`Release log line ${headerIdx + 2} must be the table separator row: ${sep?.content ?? '(end of file)'}`);
  }
  return { lines, sepIdx: headerIdx + 1 };
}

export function parseLog(text) {
  const { lines, sepIdx } = locateTable(text);
  const rows = [];
  for (let i = sepIdx + 1; i < lines.length; i++) {
    if (!lines[i].content.trimStart().startsWith('|')) break;
    const cells = splitCells(lines[i].content, i + 1);
    if (cells.length !== 4) {
      throw new Error(`Release log line ${i + 1} must have exactly four cells, found ${cells.length}: ${lines[i].content}`);
    }
    rows.push({ version: cells[0], tag: cells[1], date: cells[2], note: cells[3] });
  }
  return { rows };
}

export function insertRow(text, { version, tag, date, note }) {
  if (typeof note !== 'string' || note.trim() === '') throw new Error('The release note must not be empty.');
  if (/[\r\n]/.test(note)) throw new Error('The release note must be a single line.');
  for (const [name, value] of [['version', version], ['tag', tag], ['date', date]]) {
    if (typeof value !== 'string' || value.trim() === '' || /[\r\n]/.test(value)) {
      throw new Error(`The release ${name} must be a non-empty single line.`);
    }
  }
  const { lines, sepIdx } = locateTable(text);
  const esc = (s) => s.trim().replaceAll('|', '\\|');
  const row = `| ${esc(version)} | ${esc(tag)} | ${esc(date)} | ${esc(note)} |`;
  const sep = lines[sepIdx];
  const end = sep.offset + sep.raw.length;
  if (sep.eol !== '') return text.slice(0, end) + row + sep.eol + text.slice(end);
  const style = text.includes('\r\n') ? '\r\n' : '\n';
  return text + style + row;
}

export function countVersion(rows, version) {
  return rows.filter((r) => r.version === version).length;
}

export function findRow(rows, version) {
  return rows.find((r) => r.version === version);
}

export function versionInLogMessage(version) {
  return `Version '${version}' is already in the release log.`;
}

export function assertVersionNotInLog(rows, version) {
  if (countVersion(rows, version) > 0) throw new Error(versionInLogMessage(version));
}

// ---- Version file ----

const ELEMENT_NAMES = { line: 'VersionLine', version: 'Version' };

function elementMatches(text, name) {
  return [...text.matchAll(new RegExp(`<${name}>([\\s\\S]*?)</${name}>`, 'g'))];
}

function singleElement(text, name) {
  const found = elementMatches(text, name);
  if (found.length === 0) throw new Error(`Version file has no <${name}> element.`);
  if (found.length > 1) throw new Error(`Version file has more than one <${name}> element.`);
  return found[0];
}

export function readVersionFile(text) {
  return {
    line: singleElement(text, ELEMENT_NAMES.line)[1].trim(),
    version: singleElement(text, ELEMENT_NAMES.version)[1].trim(),
  };
}

export function writeVersion(text, version) {
  if (typeof version !== 'string' || version === '' || /[<>&\s]/.test(version)) {
    throw new Error(`Version '${version}' cannot be written to the version file.`);
  }
  const m = singleElement(text, ELEMENT_NAMES.version);
  const innerStart = m.index + '<Version>'.length;
  return text.slice(0, innerStart) + version + text.slice(innerStart + m[1].length);
}

// ---- Release check ----

export function checkRelease({ tag, versionFileText, logText }) {
  const reasons = [];
  if (!isValidTag(tag)) reasons.push(tagFormatMessage(tag));

  let file = null;
  try {
    file = readVersionFile(versionFileText);
  } catch (e) {
    reasons.push(e.message);
  }
  let rows = null;
  try {
    rows = parseLog(logText).rows;
  } catch (e) {
    reasons.push(e.message);
  }

  if (file) reasons.push(...checkVersionAgainstLine(file.version, file.line));

  if (file && rows) {
    const count = countVersion(rows, file.version);
    if (count > 1) reasons.push(`Version '${file.version}' appears ${count} times in the release log.`);
    if (count === 0) {
      reasons.push(`The release log has no row for version '${file.version}'. A release starts with the release command.`);
    } else {
      const row = findRow(rows, file.version);
      if (row.tag !== tag) {
        reasons.push(`The release log row for version '${file.version}' names tag '${row.tag}', but the pushed tag is '${tag}'.`);
      }
    }
  }
  return reasons;
}
