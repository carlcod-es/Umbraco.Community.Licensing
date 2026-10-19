import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  parseLine, invalidLineMessage, nextVersion, checkVersionAgainstLine,
  isValidTag, tagFormatMessage, nextTag, localDate,
  parseLog, insertRow, countVersion, findRow, versionInLogMessage, assertVersionNotInLog,
  readVersionFile, writeVersion, checkRelease,
} from './release-rules.mjs';

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const repoFile = (name) => readFileSync(join(repoRoot, name), 'utf8');

const HEADER = '| Version | Tag | Date | What changed |';
const SEP = '|---|---|---|---|';
const logText = (rows, eol = '\n') =>
  ['# Releases', '', 'Intro text.', '', HEADER, SEP, ...rows, '', 'Trailing text | with pipe.', ''].join(eol);
const row = (v, t = 'release-20261020', d = '2026-10-20', n = 'note') => `| ${v} | ${t} | ${d} | ${n} |`;
const propsText = (line, version, eol = '\n') =>
  ['<Project>', '  <PropertyGroup>', `    <VersionLine>${line}</VersionLine>`, `    <Version>${version}</Version>`, '  </PropertyGroup>', '</Project>', ''].join(eol);

// ---- 3.1 ----

test('Scenario: First beta', () => {
  assert.equal(nextVersion('0.1-beta', []), '0.1.0-beta.1');
});

test('Scenario: Numbers continue past a failed attempt', () => {
  assert.equal(nextVersion('0.1-beta', ['0.1.0-beta.2', '0.1.0-beta.1']), '0.1.0-beta.3');
});

test('Scenario: First release on line 1', () => {
  assert.equal(nextVersion('1', ['0.1.0-beta.1', '0.2.0-beta.4']), '1.0.0');
});

test('Scenario: Next release on line 1', () => {
  assert.equal(nextVersion('1', ['1.4.0', '1.3.0', '1.0.0', '0.1.0-beta.1']), '1.5.0');
});

test('Scenario: New major version', () => {
  assert.equal(nextVersion('2', ['1.7.0', '1.0.0']), '2.0.0');
});

test('Scenario: Invalid line', () => {
  for (const line of ['0.1', '1-beta', '1.0', '0', 'abc', '', '01', '0.0-beta', '0.01-beta', '00.1-beta', ' 1', '1 ', '0.1-beta ', '-1']) {
    assert.equal(parseLine(line), null, line);
    assert.throws(() => nextVersion(line, []), (e) => e.message === invalidLineMessage(line), line);
    const msg = invalidLineMessage(line);
    assert.ok(msg.includes(`'${line}'`) && msg.includes('0.M-beta') && msg.includes("'X'"));
  }
});

test('parseLine accepts the two forms', () => {
  assert.deepEqual(parseLine('0.1-beta'), { kind: 'beta', minor: 1 });
  assert.deepEqual(parseLine('0.12-beta'), { kind: 'beta', minor: 12 });
  assert.deepEqual(parseLine('1'), { kind: 'release', major: 1 });
  assert.deepEqual(parseLine('20'), { kind: 'release', major: 20 });
});

test('nextVersion compares numerically, not as strings', () => {
  assert.equal(nextVersion('0.1-beta', ['0.1.0-beta.9', '0.1.0-beta.10']), '0.1.0-beta.11');
  assert.equal(nextVersion('1', ['1.9.0', '1.10.0']), '1.11.0');
});

test('nextVersion ignores log versions of other lines and forms', () => {
  assert.equal(nextVersion('0.1-beta', ['0.2.0-beta.7', '0.1.0', '0.1.1-beta.5', '0.1.0-beta.0', 'junk', '1.0.0']), '0.1.0-beta.1');
  assert.equal(nextVersion('1', ['2.3.0', '1.2.1', '1.2.0-beta.1', '1.2', '0.1.0-beta.1']), '1.0.0');
  assert.equal(nextVersion('0.1-beta', ['0.10.0-beta.3']), '0.1.0-beta.1');
  assert.equal(nextVersion('1', ['10.4.0']), '1.0.0');
});

test('Scenario: Beta status disagrees (version check)', () => {
  const a = checkVersionAgainstLine('1.0.0-beta.1', '1');
  assert.equal(a.length, 1);
  assert.ok(a[0].includes('1.0.0-beta.1') && a[0].includes("'1'"));
  const b = checkVersionAgainstLine('0.1.0', '0.1-beta');
  assert.equal(b.length, 1);
  assert.ok(b[0].includes('0.1.0') && b[0].includes('0.1-beta'));
});

test('checkVersionAgainstLine passes consistent pairs', () => {
  assert.deepEqual(checkVersionAgainstLine('0.1.0-beta.1', '0.1-beta'), []);
  assert.deepEqual(checkVersionAgainstLine('0.3.0-beta.12', '0.3-beta'), []);
  assert.deepEqual(checkVersionAgainstLine('1.0.0', '1'), []);
  assert.deepEqual(checkVersionAgainstLine('2.5.0', '2'), []);
});

test('checkVersionAgainstLine rejects version not belonging to the line', () => {
  for (const [v, l] of [['0.2.0-beta.1', '0.1-beta'], ['2.0.0', '1'], ['1.0.0', '2'], ['0.1.0-beta.1', '1'], ['1.0.0', '0.1-beta'], ['0.1.0-beta.0', '0.1-beta']]) {
    const r = checkVersionAgainstLine(v, l);
    assert.ok(r.length >= 1, `${v} / ${l}`);
    assert.ok(r.every((s) => s.includes(v) && s.includes(l)), `${v} / ${l}`);
  }
});

test('checkVersionAgainstLine rejects other version forms', () => {
  for (const v of ['1.0', 'abc', '', '1.0.1', '1.2.3', '0.1.1-beta.1', '1.0.0-rc.1', '01.0.0', '1.0.0+build']) {
    assert.ok(checkVersionAgainstLine(v, '1').length >= 1, v);
  }
});

test('checkVersionAgainstLine reports an invalid line', () => {
  const r = checkVersionAgainstLine('1.0.0', 'abc');
  assert.ok(r.includes(invalidLineMessage('abc')));
});

// ---- 3.2 ----

test('Scenario: First release of the day', () => {
  assert.equal(nextTag(new Date(2026, 9, 20), []), 'release-20261020');
  assert.equal(nextTag(new Date(2026, 9, 20), ['release-20261019', 'release-20261021', 'v1', 'release-202610200']), 'release-20261020');
});

test('Scenario: Third release of the day', () => {
  assert.equal(nextTag(new Date(2026, 9, 20), ['release-20261020', 'release-20261020-2']), 'release-20261020-3');
});

test('Scenario: Malformed tag (format)', () => {
  for (const tag of ['release-2026-10-20', 'release-20261020-1']) {
    assert.equal(isValidTag(tag), false, tag);
    assert.ok(tagFormatMessage(tag).includes(tag));
    assert.ok(tagFormatMessage(tag).includes('release-yyyymmdd'));
  }
});

test('isValidTag accepts and rejects', () => {
  for (const t of ['release-20261020', 'release-20261020-2', 'release-20261020-10', 'release-20240229']) assert.equal(isValidTag(t), true, t);
  for (const t of ['release-20261020-0', 'release-20261020-02', 'release-20261340', 'release-20260230', 'release-20250229',
    'release-20261020-', 'release-20261020-x', 'xrelease-20261020', 'release-20261020x', ' release-20261020', 'release-20261020\n',
    'Release-20261020', 'release-2026102', 'release-202610201', 'release-00001020', 'release-20260001', '', 'release-20261020-2-3']) {
    assert.equal(isValidTag(t), false, JSON.stringify(t));
  }
});

test('nextTag picks the lowest free number', () => {
  const d = new Date(2026, 9, 20);
  assert.equal(nextTag(d, ['release-20261020', 'release-20261020-3']), 'release-20261020-2');
  assert.equal(nextTag(d, ['release-20261020-2']), 'release-20261020-3');
  assert.equal(nextTag(d, ['release-20261020-3']), 'release-20261020-2');
  assert.equal(nextTag(d, ['release-20261020', 'release-20261020-2', 'release-20261020-3', 'release-20261020-5']), 'release-20261020-4');
  assert.equal(nextTag(d, ['release-20261020-1', 'release-20261020-x', 'release-20261020-02']), 'release-20261020');
});

test('nextTag uses the local date and zero pads', () => {
  assert.equal(nextTag(new Date(2026, 0, 5, 23, 59, 59), []), 'release-20260105');
  assert.equal(nextTag(new Date(2026, 11, 31, 0, 0, 0), []), 'release-20261231');
});

test('localDate uses local fields, not UTC', () => {
  assert.equal(localDate(new Date(2026, 9, 20, 23, 59, 59)), '2026-10-20');
  assert.equal(localDate(new Date(2026, 9, 20, 0, 0, 1)), '2026-10-20');
  assert.equal(localDate(new Date(2026, 0, 5, 0, 0, 1)), '2026-01-05');
  assert.equal(nextTag(new Date(2026, 9, 20, 23, 59, 59), []), 'release-20261020');
  assert.equal(nextTag(new Date(2026, 9, 21, 0, 0, 1), []), 'release-20261021');
});

// ---- 3.3 ----

test('Scenario: Row added', () => {
  for (const eol of ['\n', '\r\n']) {
    const text = logText([row('0.1.0-beta.0', 'release-20261001', '2026-10-01', 'Old')], eol);
    const out = insertRow(text, { version: '0.1.0-beta.1', tag: 'release-20261020', date: '2026-10-20', note: 'First beta' });
    const rows = parseLog(out).rows;
    assert.deepEqual(rows[0], { version: '0.1.0-beta.1', tag: 'release-20261020', date: '2026-10-20', note: 'First beta' });
    assert.equal(rows.length, 2);
    assert.equal(rows[1].version, '0.1.0-beta.0');
  }
});

test('insertRow changes nothing but the inserted line (LF and CRLF)', () => {
  for (const eol of ['\n', '\r\n']) {
    const text = logText([row('0.1.0-beta.1')], eol);
    const out = insertRow(text, { version: '0.1.0-beta.2', tag: 'release-20261021', date: '2026-10-21', note: 'Second' });
    const expected = logText([row('0.1.0-beta.2', 'release-20261021', '2026-10-21', 'Second'), row('0.1.0-beta.1')], eol);
    assert.equal(out, expected);
  }
});

test('insertRow uses the separator line ending in a mixed file', () => {
  const text = `${HEADER}\r\n${SEP}\r\n${row('0.1.0-beta.1')}\n`;
  const out = insertRow(text, { version: '0.1.0-beta.2', tag: 't', date: 'd', note: 'n' });
  assert.equal(out, `${HEADER}\r\n${SEP}\r\n| 0.1.0-beta.2 | t | d | n |\r\n${row('0.1.0-beta.1')}\n`);
  assert.equal(parseLog(out).rows.length, 2);
});

test('insertRow when the separator is the last line', () => {
  const r = { version: '1.0.0', tag: 'release-20261020', date: '2026-10-20', note: 'n' };
  assert.equal(insertRow(`${HEADER}\n${SEP}`, r), `${HEADER}\n${SEP}\n| 1.0.0 | release-20261020 | 2026-10-20 | n |`);
  assert.equal(insertRow(`${HEADER}\r\n${SEP}`, r), `${HEADER}\r\n${SEP}\r\n| 1.0.0 | release-20261020 | 2026-10-20 | n |`);
  assert.equal(insertRow(`${HEADER}\n${SEP}\n`, r), `${HEADER}\n${SEP}\n| 1.0.0 | release-20261020 | 2026-10-20 | n |\n`);
  assert.equal(parseLog(insertRow(`${HEADER}\n${SEP}`, r)).rows.length, 1);
});

test('insertRow escapes pipes in the note and parseLog unescapes them', () => {
  const text = logText([]);
  const out = insertRow(text, { version: '1.0.0', tag: 'release-20261020', date: '2026-10-20', note: 'a | b || c' });
  assert.ok(out.includes('| a \\| b \\|\\| c |'));
  assert.equal(parseLog(out).rows[0].note, 'a | b || c');
});

test('insertRow enforces a single-line, non-empty note', () => {
  const base = { version: '1.0.0', tag: 'release-20261020', date: '2026-10-20' };
  for (const note of ['', '   ', '\t', 'a\nb', 'a\r\nb', 'a\rb', '\n']) {
    assert.throws(() => insertRow(logText([]), { ...base, note }), Error, JSON.stringify(note));
  }
});

test('insertRow trims the note', () => {
  const out = insertRow(logText([]), { version: '1.0.0', tag: 't', date: 'd', note: '  hi  ' });
  assert.equal(parseLog(out).rows[0].note, 'hi');
  assert.ok(out.includes('| hi |'));
});

test('insertRow throws when there is no valid table', () => {
  assert.throws(() => insertRow('# Releases\n', { version: 'v', tag: 't', date: 'd', note: 'n' }), /no table/);
});

test('Scenario: Failed release keeps its row', () => {
  const rows = parseLog(logText([row('0.1.0-beta.2'), row('0.1.0-beta.1')])).rows;
  assert.equal(nextVersion('0.1-beta', rows.map((r) => r.version)), '0.1.0-beta.3');
});

test('Scenario: Version already in the log', () => {
  const rows = parseLog(logText([row('0.1.0-beta.1')])).rows;
  assert.equal(countVersion(rows, '0.1.0-beta.1'), 1);
  assert.equal(countVersion(rows, '0.1.0-beta.2'), 0);
  assert.ok(versionInLogMessage('0.1.0-beta.1').includes('0.1.0-beta.1'));
});

test('Scenario: Version already in the log (guard)', () => {
  const rows = parseLog(logText([row('0.1.0-beta.1')])).rows;
  assert.throws(() => assertVersionNotInLog(rows, '0.1.0-beta.1'), { message: versionInLogMessage('0.1.0-beta.1') });
  assert.doesNotThrow(() => assertVersionNotInLog(rows, '0.1.0-beta.2'));
  assert.doesNotThrow(() => assertVersionNotInLog([], '0.1.0-beta.1'));
});

test('Scenario: Duplicate version in the log (rule)', () => {
  const rows = parseLog(logText([row('0.1.0-beta.2', 'release-20261021'), row('0.1.0-beta.2', 'release-20261020'), row('0.1.0-beta.1')])).rows;
  assert.equal(countVersion(rows, '0.1.0-beta.2'), 2);
  assert.equal(findRow(rows, '0.1.0-beta.2').tag, 'release-20261021');
  assert.equal(findRow(rows, '9.9.9'), undefined);
});

test('parseLog keeps order, trims cells, reads the first table only (LF and CRLF)', () => {
  for (const eol of ['\n', '\r\n']) {
    const text = [HEADER, SEP, '|  b2  |t2| 2026-10-21 |  Two  |', row('b1'), '', '| Version | Tag | Date | What changed |', SEP, row('other')].join(eol);
    const { rows } = parseLog(text);
    assert.deepEqual(rows.map((r) => r.version), ['b2', 'b1']);
    assert.deepEqual(rows[0], { version: 'b2', tag: 't2', date: '2026-10-21', note: 'Two' });
  }
});

test('parseLog accepts an empty note cell and a row without a trailing pipe', () => {
  const { rows } = parseLog(`${HEADER}\n${SEP}\n| a | b | c | |\n| d | e | f | g\n`);
  assert.equal(rows[0].note, '');
  assert.equal(rows[1].note, 'g');
});

test('parseLog throws naming the line for malformed input', () => {
  assert.throws(() => parseLog('# nothing\n'), /no table/);
  assert.throws(() => parseLog(''), /no table/);
  assert.throws(() => parseLog('| Version | Tag | Date |\n|---|---|---|\n'), /line 1/);
  assert.throws(() => parseLog('| Version | Tag | Date | Notes |\n|---|---|---|---|\n'), /line 1/);
  assert.throws(() => parseLog(`${HEADER}\n`), /line 2/);
  assert.throws(() => parseLog(`${HEADER}\nnot a separator\n`), /line 2/);
  assert.throws(() => parseLog(`${HEADER}\n${SEP}\n${row('a')}\n| a | b | c |\n`), /line 4/);
  assert.throws(() => parseLog(`${HEADER}\n${SEP}\n| a | b | c | d | e |\n`), /line 3/);
});

test("Repository RELEASES.md parses with zero rows", () => {
  assert.deepEqual(parseLog(repoFile('RELEASES.md')).rows, []);
});

test('insertRow on the repository RELEASES.md parses back to one row', () => {
  const text = repoFile('RELEASES.md');
  const r = { version: '0.1.0-beta.1', tag: 'release-20261020', date: '2026-10-20', note: 'First beta' };
  const out = insertRow(text, r);
  assert.deepEqual(parseLog(out).rows, [r]);
  assert.ok(out.length > text.length);
});

// ---- 3.4 ----

test('readVersionFile reads line and version (LF and CRLF)', () => {
  for (const eol of ['\n', '\r\n']) {
    assert.deepEqual(readVersionFile(propsText('0.1-beta', '0.1.0-beta.1', eol)), { line: '0.1-beta', version: '0.1.0-beta.1' });
  }
});

test('readVersionFile trims inner text and does not confuse Version with VersionLine', () => {
  const text = '<Project><PropertyGroup><VersionLine> 1 </VersionLine>\n<Version>\n 1.2.0 \n</Version></PropertyGroup></Project>';
  assert.deepEqual(readVersionFile(text), { line: '1', version: '1.2.0' });
  // VersionLine alone must not satisfy <Version>.
  assert.throws(() => readVersionFile('<VersionLine>1</VersionLine>'), /<Version>/);
});

test('readVersionFile throws when an element is missing or repeated', () => {
  assert.throws(() => readVersionFile('<Version>1.0.0</Version>'), /VersionLine/);
  assert.throws(() => readVersionFile('<VersionLine>1</VersionLine>'), /<Version>/);
  assert.throws(() => readVersionFile(propsText('1', '1.0.0') + '<Version>1.1.0</Version>'), /more than one <Version>/);
  assert.throws(() => readVersionFile(propsText('1', '1.0.0') + '<VersionLine>2</VersionLine>'), /more than one <VersionLine>/);
});

test('Round trip of the repository Version.props', () => {
  const text = repoFile('Version.props');
  assert.deepEqual(readVersionFile(text), { line: '0.1-beta', version: '0.1.0-beta.1' });
  assert.equal(writeVersion(text, '0.1.0-beta.1'), text);
  const out = writeVersion(text, '0.1.0-beta.2');
  assert.equal(out, text.replace('<Version>0.1.0-beta.1</Version>', '<Version>0.1.0-beta.2</Version>'));
  assert.notEqual(out, text);
  assert.deepEqual(readVersionFile(out), { line: '0.1-beta', version: '0.1.0-beta.2' });
});

test('writeVersion leaves every other byte unchanged (LF and CRLF)', () => {
  for (const eol of ['\n', '\r\n']) {
    const out = writeVersion(propsText('0.1-beta', '0.1.0-beta.1', eol), '0.1.0-beta.3');
    assert.equal(out, propsText('0.1-beta', '0.1.0-beta.3', eol));
  }
  const noNewline = '<Project>\n\t<Version>1.0.0</Version>\n\t<VersionLine>1</VersionLine></Project>';
  assert.equal(writeVersion(noNewline, '1.1.0'), '<Project>\n\t<Version>1.1.0</Version>\n\t<VersionLine>1</VersionLine></Project>');
});

test('writeVersion replaces whitespace-padded inner text and rejects bad input', () => {
  assert.equal(writeVersion('<Version> 1.0.0 </Version>', '1.1.0'), '<Version>1.1.0</Version>');
  assert.throws(() => writeVersion('<VersionLine>1</VersionLine>', '1.0.0'), /<Version>/);
  assert.throws(() => writeVersion('<Version>1</Version><Version>2</Version>', '1.0.0'), /more than one/);
  assert.throws(() => writeVersion('<Version>1.0.0</Version>', '1.0.0</Version><x>'), Error);
  assert.throws(() => writeVersion('<Version>1.0.0</Version>', ''), Error);
});

// ---- 3.5 ----

const passing = (eol) => ({
  tag: 'release-20261020',
  versionFileText: propsText('0.1-beta', '0.1.0-beta.1', eol),
  logText: logText([row('0.1.0-beta.1')], eol),
});

test('checkRelease passes a consistent tag, version file and log (LF and CRLF)', () => {
  assert.deepEqual(checkRelease(passing('\n')), []);
  assert.deepEqual(checkRelease(passing('\r\n')), []);
});

test('Scenario: Malformed tag (check)', () => {
  for (const tag of ['release-2026-10-20', 'release-20261020-1']) {
    const r = checkRelease({ ...passing('\n'), tag });
    assert.ok(r.includes(tagFormatMessage(tag)), tag);
  }
});

test('Scenario: Duplicate version in the log (check)', () => {
  for (const eol of ['\n', '\r\n']) {
    const r = checkRelease({ ...passing(eol), logText: logText([row('0.1.0-beta.1'), row('0.1.0-beta.1')], eol) });
    assert.equal(r.length, 1);
    assert.ok(r[0].includes('0.1.0-beta.1'));
  }
});

test('Scenario: Beta status disagrees', () => {
  const a = checkRelease({ ...passing('\n'), versionFileText: propsText('1', '1.0.0-beta.1'), logText: logText([row('1.0.0-beta.1')]) });
  assert.equal(a.length, 1);
  assert.ok(a[0].includes('1.0.0-beta.1') && a[0].includes("'1'"));
  const b = checkRelease({ ...passing('\n'), versionFileText: propsText('0.1-beta', '0.1.0'), logText: logText([row('0.1.0')]) });
  assert.equal(b.length, 1);
  assert.ok(b[0].includes('0.1.0') && b[0].includes('0.1-beta'));
});

test('Scenario: Tag pushed without the release command', () => {
  for (const eol of ['\n', '\r\n']) {
    const r = checkRelease({ ...passing(eol), tag: 'release-20261021' });
    assert.equal(r.length, 1);
    assert.ok(r[0].includes('release-20261020') && r[0].includes('release-20261021'));
  }
});

test('Scenario: Tag with no log row', () => {
  for (const eol of ['\n', '\r\n']) {
    const r = checkRelease({ ...passing(eol), tag: 'release-20261015', logText: logText([], eol) });
    assert.equal(r.length, 1);
    assert.ok(r[0].includes('0.1.0-beta.1') && r[0].includes('release command'));
  }
});

test('checkRelease uses the first row for the version when duplicated', () => {
  const r = checkRelease({ ...passing('\n'), logText: logText([row('0.1.0-beta.1', 'release-20261020'), row('0.1.0-beta.1', 'release-20261001')]) });
  assert.equal(r.length, 1);
  const r2 = checkRelease({ ...passing('\n'), logText: logText([row('0.1.0-beta.1', 'release-20261001'), row('0.1.0-beta.1', 'release-20261020')]) });
  assert.equal(r2.length, 2);
});

test('checkRelease collects every reason at once', () => {
  const r = checkRelease({
    tag: 'release-2026-10-20',
    versionFileText: propsText('1', '1.0.0-beta.1'),
    logText: logText([row('1.0.0-beta.1', 'release-20261020'), row('1.0.0-beta.1', 'release-20261019')]),
  });
  assert.equal(r.length, 4);
  assert.ok(r.includes(tagFormatMessage('release-2026-10-20')));
  assert.ok(r.some((s) => s.includes('prerelease')));
  assert.ok(r.some((s) => s.includes('2 times')));
  assert.ok(r.some((s) => s.includes('release-20261020') && s.includes('release-2026-10-20')));
});

test('checkRelease turns unparseable inputs into reasons and still reports the rest', () => {
  const noFile = checkRelease({ tag: 'bad', versionFileText: '<Project/>', logText: logText([]) });
  assert.equal(noFile.length, 2);
  assert.ok(noFile.some((s) => s.includes('VersionLine')));
  const noLog = checkRelease({ ...passing('\n'), logText: '# nothing' });
  assert.equal(noLog.length, 1);
  assert.match(noLog[0], /no table/);
  const bothBad = checkRelease({ tag: 'release-20261020', versionFileText: '', logText: '' });
  assert.equal(bothBad.length, 2);
  const versionBadLogOk = checkRelease({ tag: 'release-20261020', versionFileText: propsText('abc', '1.0.0'), logText: logText([]) });
  assert.ok(versionBadLogOk.some((s) => s.includes('abc')));
});
