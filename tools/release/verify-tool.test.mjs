import { test } from 'node:test';
import assert from 'node:assert/strict';
import { checkPackFolder, checkVersionOutput, installArgs } from './verify-tool.mjs';

test('accepts the exact version, with or without a trailing newline', () => {
  assert.equal(checkVersionOutput('0.1.0-beta.7', '0.1.0-beta.7'), null);
  assert.equal(checkVersionOutput('0.1.0-beta.7\n', '0.1.0-beta.7'), null);
  assert.equal(checkVersionOutput('0.1.0-beta.7\r\n', '0.1.0-beta.7'), null);
});

test('rejects any other output', () => {
  for (const bad of ['1.0.0', '0.1.0-beta.7.0', '0.1.0-beta.7+abc123', '0.1.0-beta.6', '', ' 0.1.0-beta.7', '0.1.0-beta.7\n\n', 'ucl 0.1.0-beta.7']) {
    assert.match(checkVersionOutput(bad, '0.1.0-beta.7'), /release version/, JSON.stringify(bad));
  }
  assert.match(checkVersionOutput(undefined, '0.1.0-beta.7'), /reported ''/);
});

test('installs only the pinned version from the pack folder', () => {
  assert.deepEqual(installArgs('p', 't', '0.1.0-beta.7'), [
    'tool', 'install', 'Umbraco.Community.Licensing.Tool', '--tool-path', 't', '--add-source', 'p', '--version', '[0.1.0-beta.7]',
  ]);
});

const V = '0.1.0-beta.7';
const LIB = `Umbraco.Community.Licensing.${V}.nupkg`;
const TOOL = `Umbraco.Community.Licensing.Tool.${V}.nupkg`;

test('pack folder: accepts exactly the library and tool packages, ignoring other files', () => {
  assert.equal(checkPackFolder([TOOL, LIB], V), null);
  assert.equal(checkPackFolder([LIB, TOOL, 'x.snupkg', 'readme.txt'], V), null);
});

test('pack folder: rejects a missing library', () => {
  assert.match(checkPackFolder([TOOL], V), /missing Umbraco\.Community\.Licensing\.0\.1\.0-beta\.7\.nupkg\./);
});

test('pack folder: rejects a missing tool', () => {
  assert.match(checkPackFolder([LIB], V), /missing Umbraco\.Community\.Licensing\.Tool\./);
});

test('pack folder: rejects an extra package', () => {
  assert.match(checkPackFolder([LIB, TOOL, `Other.Pkg.${V}.nupkg`], V), /unexpected Other\.Pkg/);
});

test('pack folder: rejects a package of the wrong version', () => {
  const reason = checkPackFolder([LIB, 'Umbraco.Community.Licensing.Tool.0.1.0-beta.6.nupkg'], V);
  assert.match(reason, /missing/);
  assert.match(reason, /unexpected/);
});
