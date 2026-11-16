import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';
import { build } from 'esbuild';
import { JSDOM } from 'jsdom';
import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { reportVehicleRepair } from '../src/features/storage/api/vehicleLocationApi.js';

test('repair callouts follow projection, switch parts, hide offscreen and disappear after completion', async () => {
  const require = createRequire(import.meta.url);
  const compiled = await build({ entryPoints: ['src/features/storage/garage3d/RepairCallouts.jsx'], bundle: true, write: false, format: 'esm', platform: 'node', jsx: 'automatic', external: ['react', 'react/jsx-runtime'] });
  let code = compiled.outputFiles[0].text;
  for (const name of ['react', 'react/jsx-runtime']) code = code.replaceAll(`from "${name}"`, `from "${pathToFileURL(require.resolve(name)).href}"`);
  const { default: RepairCallouts } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`);
  const dom = new JSDOM('<div id="root"></div>');
  const saved = Object.fromEntries(['window', 'document', 'requestAnimationFrame', 'cancelAnimationFrame', 'IS_REACT_ACT_ENVIRONMENT'].map(key => [key, globalThis[key]]));
  Object.assign(globalThis, { window: dom.window, document: dom.window.document, IS_REACT_ACT_ENVIRONMENT: true });
  let nextId = 0, point = { x: 180, y: 230, visible: true };
  const frames = new Map(), calls = [];
  globalThis.requestAnimationFrame = callback => { frames.set(++nextId, callback); return nextId; };
  globalThis.cancelAnimationFrame = id => frames.delete(id);
  const scene = { current: { projectVehiclePoint: (id, position) => { calls.push({ id, position }); return point; } } };
  const root = createRoot(document.getElementById('root'));
  const vehicle = { vehicleId: 7, bayId: 5, repairs: [
    { partId: 'tyre-right-front', note: 'Đang thay lốp', technician: 'KTV An' },
    { partId: 'windshield', note: 'Thay kính nứt', technician: 'KTV Bình' },
  ] };
  const render = value => act(() => root.render(React.createElement(RepairCallouts, { scene, vehicle: value })));
  const tick = () => { const pending = [...frames.values()]; frames.clear(); pending.forEach(callback => callback()); };
  try {
    await render(vehicle);
    assert.equal(document.querySelectorAll('.garage3d-repair-marker').length, 2);
    assert.match(document.querySelector('article').textContent, /Đang thay lốp.*KTV An/);
    assert.ok(calls.every(call => call.id === 7));
    assert.match(document.querySelector('path').getAttribute('d'), /^M 180 230/);
    point = { x: 310, y: 170, visible: true }; tick();
    assert.match(document.querySelector('path').getAttribute('d'), /^M 310 170/);
    await act(() => document.querySelector('[aria-label="Kính chắn gió trước"]').click());
    assert.match(document.querySelector('article').textContent, /Thay kính nứt.*KTV Bình/);
    point.visible = false; tick();
    assert.equal(document.querySelector('article').hidden, true);
    assert.equal(document.querySelector('path').getAttribute('visibility'), 'hidden');
    assert.ok([...document.querySelectorAll('.garage3d-repair-marker')].every(marker => marker.hidden));
    await render({ ...vehicle, repairs: [] });
    assert.equal(document.querySelector('article'), null);
  } finally {
    await act(() => root.unmount()); dom.window.close(); Object.assign(globalThis, saved);
  }
  assert.equal(frames.size, 0);
});

test('technician report sends part, content and completion through authenticated dual routes', async () => {
  let sent;
  const data = { partId: 'windshield', note: 'Đã thay kính', completed: true };
  await reportVehicleRepair(42, data, async options => { sent = options; return {}; });
  assert.equal(sent.method, 'PUT'); assert.equal(sent.url, '/api/v1/technician/vehicle-repairs/42');
  assert.deepEqual(sent.data, data);
});
