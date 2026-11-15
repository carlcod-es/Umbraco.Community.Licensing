import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { needsBuild, nodeVersionOk, spawnPlan } from './run-demo.mjs';

const temps = [];
after(() => {
  for (const dir of temps) fs.rmSync(dir, { recursive: true, force: true, maxRetries: 5, retryDelay: 100 });
});

function setup() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'run-demo-'));
  temps.push(root);
  const bundleDir = path.join(root, 'bundle');
  const srcDir = path.join(root, 'src');
  fs.mkdirSync(srcDir);
  return { root, bundleDir, srcDir, inputs: [srcDir, path.join(root, 'package.json')] };
}

function write(file, secondsAgo) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, 'x');
  const when = new Date(Date.now() - secondsAgo * 1000);
  fs.utimesSync(file, when, when);
}

test('bundle folder missing needs a build', () => {
  const { bundleDir, srcDir, inputs } = setup();
  write(path.join(srcDir, 'a.ts'), 100);
  assert.equal(needsBuild({ bundleDir, inputs }), true);
});

test('bundle folder empty needs a build', () => {
  const { bundleDir, srcDir, inputs } = setup();
  fs.mkdirSync(bundleDir);
  write(path.join(srcDir, 'a.ts'), 100);
  assert.equal(needsBuild({ bundleDir, inputs }), true);
});

test('source newer than the bundle needs a build', () => {
  const { bundleDir, srcDir, inputs } = setup();
  write(path.join(bundleDir, 'out.js'), 100);
  write(path.join(srcDir, 'a.ts'), 10);
  assert.equal(needsBuild({ bundleDir, inputs }), true);
});

test('bundle newer than the source needs no build', () => {
  const { bundleDir, srcDir, inputs } = setup();
  write(path.join(srcDir, 'a.ts'), 100);
  write(path.join(bundleDir, 'out.js'), 10);
  assert.equal(needsBuild({ bundleDir, inputs }), false);
});

test('nested source file newer than the bundle needs a build', () => {
  const { bundleDir, srcDir, inputs } = setup();
  write(path.join(srcDir, 'a.ts'), 200);
  write(path.join(bundleDir, 'out.js'), 100);
  write(path.join(srcDir, 'deep', 'er', 'b.ts'), 10);
  assert.equal(needsBuild({ bundleDir, inputs }), true);
});

test('a newer build input file (package.json) needs a build', () => {
  const { root, bundleDir, srcDir, inputs } = setup();
  write(path.join(srcDir, 'a.ts'), 200);
  write(path.join(bundleDir, 'out.js'), 100);
  write(path.join(root, 'package.json'), 10);
  assert.equal(needsBuild({ bundleDir, inputs }), true);
});

test('node version check', () => {
  assert.equal(nodeVersionOk('24.13.0'), true);
  assert.equal(nodeVersionOk('24.22.2'), true);
  assert.equal(nodeVersionOk('25.0.0'), true);
  assert.equal(nodeVersionOk('24.12.9'), false);
  assert.equal(nodeVersionOk('22.20.0'), false);
});

test('npm on Windows starts through cmd.exe with no shell option', () => {
  const plan = spawnPlan('npm', ['run', 'build'], 'win32', 'C:\\Windows\\System32\\cmd.exe');
  assert.equal(plan.command, 'C:\\Windows\\System32\\cmd.exe');
  assert.deepEqual(plan.args, ['/d', '/s', '/c', 'npm run build']);
  assert.equal(plan.options.shell, undefined);
  assert.equal(spawnPlan('npm', ['ci'], 'win32', undefined).command, 'cmd.exe');
});

test('npm on macOS and Linux, and dotnet anywhere, run unchanged', () => {
  for (const platform of ['darwin', 'linux']) {
    assert.deepEqual(spawnPlan('npm', ['ci'], platform), { command: 'npm', args: ['ci'], options: {} });
  }
  assert.deepEqual(spawnPlan('dotnet', ['run'], 'win32'), { command: 'dotnet', args: ['run'], options: {} });
});
