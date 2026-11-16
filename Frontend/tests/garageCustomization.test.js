import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { DecalGeometry } from 'three/addons/geometries/DecalGeometry.js';
import { DEFAULT_CUSTOM, normalizeCustom, hasCustomChanges, customLines, newSticker } from '../src/features/storage/garage3d/customConfig.js';
import { PAINTS } from '../src/features/storage/garage3d/garageConfig.js';
import { stickerRay, createCustomExtras } from '../src/features/storage/garage3d/createCustomExtras.js';
import { loadVehicleModelFixture } from './helpers/vehicleModelFixture.js';
import { measureVehicleFit, profileGeometry } from '../src/features/storage/garage3d/vehicleFit.js';
import { submitCustomRequest } from '../src/features/storage/garage3d/customRequestsApi.js';

test('saved configurations preserve all decal parameters and restore old configurations', () => {
  const sticker = { ...newSticker('layer-1'), design: 'text', surface: 'hood', text: 'CUSTOM 07', color: '#aabbcc', x: 0.3, y: -0.2, size: 0.9, rotation: 45 };
  const custom = { ...DEFAULT_CUSTOM, stickers: [sticker], roof: 'carbon', bodyKit: 'sport', wheelStyle: 'split', vehicleId: '9', branchId: '2', notes: 'Keep badge' };
  const restored = normalizeCustom(JSON.parse(JSON.stringify(custom)), PAINTS);
  const { id, ...savedDecal } = restored.stickers[0];
  const { id: originalId, ...originalDecal } = sticker;
  assert.deepEqual(savedDecal, originalDecal);
  for (const key of ['roof', 'bodyKit', 'wheelStyle', 'vehicleId', 'branchId', 'notes']) assert.equal(restored[key], custom[key]);
  assert.deepEqual(normalizeCustom({ rims: 'bronze' }, PAINTS).stickers, []);
  assert.equal(hasCustomChanges({ ...DEFAULT_CUSTOM, stickers: [] }), false);
  assert.equal(hasCustomChanges({ ...DEFAULT_CUSTOM, vehicleDescription: 'My car', stickers: [] }), false);
  assert.equal(hasCustomChanges({ ...DEFAULT_CUSTOM, notes: 'Custom interior' }), true);
  assert.equal(hasCustomChanges(custom), true);
});

test('invalid stored sticker data is bounded and selection summaries contain no prices', () => {
  const restored = normalizeCustom({ stickers: Array.from({ length: 20 }, () => ({ size: 500, x: Infinity, color: 'invalid', rotation: -500 })) }, PAINTS);
  assert.equal(restored.stickers.length, 6);
  assert.equal(restored.stickers[0].size, 1.2);
  assert.equal(restored.stickers[0].rotation, -180);
  assert.equal(restored.stickers[0].x, 0);
  assert.deepEqual(normalizeCustom({ accessories: ['audio', 'invalid', 'audio'] }, PAINTS).accessories, ['audio']);
  assert.ok(customLines(restored, PAINTS).every(line => !Object.hasOwn(line, 'price')));
  assert.deepEqual(customLines(DEFAULT_CUSTOM, PAINTS), []);
});

test('submission sends chosen branch, actual vehicle and every sticker parameter', async () => {
  const custom = { ...DEFAULT_CUSTOM, branchId: '2', vehicleId: '9', stickers: [newSticker('one')], notes: 'Keep badge' };
  let sent;
  await submitCustomRequest(custom, 'retry-id', async options => { sent = options; return { id: 1 }; });
  assert.equal(sent.data.branchId, 2);
  assert.equal(sent.data.vehicleId, 9);
  assert.equal(sent.data.clientMessageId, 'retry-id');
  assert.deepEqual(sent.data.stickers, custom.stickers);
  assert.equal(sent.data.notes, custom.notes);
});

test('all decal surfaces project onto the actual compressed vehicle body', async () => {
  const { bodyMeshes } = await loadVehicleModelFixture();
  for (const surface of ['left', 'right', 'hood', 'roof']) for (const x of [-1, 0, 1]) for (const y of [-1, 0, 1]) {
    const sticker = { ...newSticker(surface), surface, x, y }, ray = stickerRay(sticker);
    const hit = new THREE.Raycaster(new THREE.Vector3(...ray.origin), new THREE.Vector3(...ray.direction)).intersectObjects(bodyMeshes)[0];
    assert.ok(hit, `${surface} at ${x}/${y} must intersect painted body`);
    const normal = hit.face.normal.clone().applyNormalMatrix(new THREE.Matrix3().getNormalMatrix(hit.object.matrixWorld));
    const orientation = new THREE.Euler().setFromQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 0, 1), normal));
    const decal = new DecalGeometry(hit.object, hit.point, orientation, new THREE.Vector3(0.7, 0.35, 0.28));
    assert.ok(decal.attributes.position.count > 0, `${surface} must generate visible decal geometry`);
    decal.dispose();
  }
  let roofVertices = 0, mirrorVertices = 0;
  for (const mesh of bodyMeshes) for (let i = 0; i < mesh.geometry.attributes.position.count; i++) {
    const point = new THREE.Vector3().fromBufferAttribute(mesh.geometry.attributes.position, i).applyMatrix4(mesh.matrixWorld);
    if (point.y >= 1.31 && Math.abs(point.x) < 0.77 && point.z >= -0.85 && point.z < 0.45) roofVertices++;
    if (Math.abs(point.x) >= 0.97 && point.y >= 0.85 && point.y < 1.17 && point.z >= 0.45 && point.z < 1.35) mirrorVertices++;
  }
  assert.ok(roofVertices > 0, 'roof finish must affect actual painted geometry');
  assert.ok(mirrorVertices > 0, 'mirror finish must affect actual painted geometry');
  bodyMeshes.forEach(mesh => { mesh.geometry.dispose(); mesh.material.dispose(); });
});

test('replacement wheels remain concentric with all four original tyres and restore cleanly', async () => {
  const fixture = await loadVehicleModelFixture();
  const { vehicle, bodyMeshes, rimMeshes, tyreMeshes, shellMeshes } = fixture;
  const fit = measureVehicleFit(vehicle, bodyMeshes, rimMeshes, tyreMeshes, shellMeshes);
  assert.equal(fit.mounts.length, 4);
  const extras = createCustomExtras(vehicle, bodyMeshes, rimMeshes, tyreMeshes, shellMeshes);
  for (const style of ['five', 'split']) {
    extras.set({ ...DEFAULT_CUSTOM, wheelStyle: style }, '#aaaaaa');
    for (const mount of fit.mounts) {
      const wheel = vehicle.getObjectByName(`custom-wheel-${mount.key}`);
      assert.ok(wheel);
      const bounds = new THREE.Box3().setFromObject(wheel), center = bounds.getCenter(new THREE.Vector3());
      const tyreCenter = mount.tyreBounds.getCenter(new THREE.Vector3());
      assert.ok(Math.abs(center.y - tyreCenter.y) < 0.001, `${mount.key}: vertical center`);
      assert.ok(Math.abs(center.z - tyreCenter.z) < 0.001, `${mount.key}: longitudinal center`);
      const radius = bounds.getSize(new THREE.Vector3()).y / 2;
      assert.ok(radius <= mount.radius + 0.001 && radius > mount.radius * 0.97, `${mount.key}: rim radius matches original`);
      const outer = mount.side < 0 ? bounds.min.x : bounds.max.x;
      const originalOuter = mount.side < 0 ? mount.rimBounds.min.x : mount.rimBounds.max.x;
      assert.ok(Math.abs(outer - originalOuter) < 0.006, `${mount.key}: no protruding rim`);
    }
    assert.ok(rimMeshes.every(mesh => !mesh.visible));
  }
  extras.set({ ...DEFAULT_CUSTOM });
  assert.ok(rimMeshes.every(mesh => mesh.visible));
  let replacementCount = 0;
  vehicle.traverse(object => { if (object.name.startsWith('custom-wheel-')) replacementCount++; });
  assert.equal(replacementCount, 0);
});

test('skirts clear both wheel arches and mounting points remain stable across garage bays', async () => {
  const { vehicle, bodyMeshes, rimMeshes, tyreMeshes, shellMeshes } = await loadVehicleModelFixture();
  const fit = measureVehicleFit(vehicle, bodyMeshes, rimMeshes, tyreMeshes, shellMeshes);
  for (const profile of fit.profiles) {
    assert.ok(profile.points.every(Boolean), `${profile.kind}: every point seats on original geometry`);
    if (profile.kind !== 'skirt') continue;
    const geometry = profileGeometry(profile.points, profile.outward);
    geometry.computeBoundingBox();
    const rear = fit.mounts.find(mount => mount.side === profile.side && mount.key.endsWith('rear'));
    const front = fit.mounts.find(mount => mount.side === profile.side && mount.key.endsWith('front'));
    assert.ok(geometry.boundingBox.min.z >= rear.tyreBounds.max.z + 0.05, 'skirt clears rear tyre');
    assert.ok(geometry.boundingBox.max.z <= front.tyreBounds.min.z - 0.05, 'skirt clears front tyre');
    geometry.dispose();
  }
  assert.ok(fit.supports.every(Boolean));
  vehicle.position.set(4.2, 0, 3.8); vehicle.rotation.y = 0.4;
  const relocated = measureVehicleFit(vehicle, bodyMeshes, rimMeshes, tyreMeshes, shellMeshes);
  for (const mount of fit.mounts) assert.ok(mount.center.distanceTo(relocated.mounts.find(item => item.key === mount.key).center) < 0.000001);
});
