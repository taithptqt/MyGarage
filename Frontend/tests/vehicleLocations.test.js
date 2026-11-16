import test from 'node:test';
import assert from 'node:assert/strict';
import { layoutGarageBays } from '../src/features/storage/garage3d/garageLocationConfig.js';
import { getMyGarageLocations, getTechnicianLocationWorkspace, reportVehicleLocation } from '../src/features/storage/api/vehicleLocationApi.js';
import { createGarageScene } from '../src/features/storage/garage3d/createGarageScene.js';

test('garage layout keeps real branch bay IDs and codes without inventing demo positions', () => {
  assert.deepEqual(layoutGarageBays([]), []);
  const bays = [{ id: 101, branchId: 9, code: 'KHO-A12', name: 'Kho A' }, { id: 205, branchId: 9, code: 'BAI-B03', name: 'Bãi B' }];
  const layout = layoutGarageBays(bays);
  assert.equal(layout.length, 2);
  assert.deepEqual(layout.map(({ id, code, branchId }) => ({ id, code, branchId })), bays.map(({ id, code, branchId }) => ({ id, code, branchId })));
  assert.ok(layout.every(bay => Number.isFinite(bay.x) && Number.isFinite(bay.z)));
});

test('actual garage never falls back to a demo slot for an unconfirmed vehicle', () => {
  const bays = [{ id: 101, code: 'KHO-A12', x: 0, z: 0 }];
  assert.throws(() => createGarageScene(null, {}, { bays, readOnly: true, initialBayId: 999, parkedVehicles: [{ bayId: 999 }] }), /Chưa xác nhận vị trí/);
  assert.throws(() => createGarageScene(null, {}, { bays, readOnly: true, initialBayId: 101, parkedVehicles: [] }), /Chưa xác nhận vị trí/);
});

test('customer location request uses signed-in customer scope, with no customer or branch override', async () => {
  const calls = []; await getMyGarageLocations(async options => { calls.push(options); return {}; });
  assert.equal(calls[0].url, '/api/v1/customers/me/garage-locations');
  assert.equal(calls[0].method, 'GET'); assert.equal(calls[0].data, undefined);
});

test('technician sends only selected order, actual bay, previous bay and report note', async () => {
  const calls = [], send = async options => { calls.push(options); return {}; };
  await getTechnicianLocationWorkspace(send);
  await reportVehicleLocation(73, { bayId: 205, expectedBayId: 101, note: 'Moved to storage' }, send);
  assert.equal(calls[0].url, '/api/v1/technician/vehicle-locations');
  assert.equal(calls[1].url, '/api/v1/technician/vehicle-locations/73');
  assert.equal(calls[1].method, 'PUT'); assert.deepEqual(calls[1].data, { bayId: 205, expectedBayId: 101, note: 'Moved to storage' });
});
