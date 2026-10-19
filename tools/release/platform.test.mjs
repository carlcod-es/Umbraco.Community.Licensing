import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { platformLine } from './check.mjs';

const SCRIPT = path.join(path.dirname(fileURLToPath(import.meta.url)), 'platform.mjs');

test('platform.mjs prints the platform line and exits 0', () => {
  const result = spawnSync(process.execPath, [SCRIPT], { encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout.replace(/\r\n/g, '\n'), `${platformLine()}\n`);
});
