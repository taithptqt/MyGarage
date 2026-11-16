import * as THREE from 'three';

function wheelBounds(vehicle, meshes) {
  const boxes = new Map();
  const inverse = vehicle.matrixWorld.clone().invert();
  for (const mesh of meshes) {
    const transform = new THREE.Matrix4().multiplyMatrices(inverse, mesh.matrixWorld);
    const positions = mesh.geometry.attributes.position;
    for (let i = 0; i < positions.count; i++) {
      const point = new THREE.Vector3().fromBufferAttribute(positions, i).applyMatrix4(transform);
      const key = `${point.x < 0 ? 'left' : 'right'}-${point.z < 0 ? 'rear' : 'front'}`;
      if (!boxes.has(key)) boxes.set(key, new THREE.Box3());
      boxes.get(key).expandByPoint(point);
    }
  }
  return boxes;
}

function mountingSurface(vehicle, meshes, lowerY, rearY, trunkZ) {
  const vertices = [], inverse = vehicle.matrixWorld.clone().invert(), point = new THREE.Vector3();
  // Keep only triangles crossing the mounting planes, rather than raycasting
  // the entire detailed exterior and interior for every sample.
  for (const mesh of meshes) {
    const transform = new THREE.Matrix4().multiplyMatrices(inverse, mesh.matrixWorld);
    const attribute = mesh.geometry.attributes.position, index = mesh.geometry.index;
    const local = new Float32Array(attribute.count * 3);
    for (let i = 0; i < attribute.count; i++) point.fromBufferAttribute(attribute, i).applyMatrix4(transform).toArray(local, i * 3);
    const count = index ? index.count : attribute.count;
    for (let i = 0; i < count; i += 3) {
      const a = (index ? index.getX(i) : i) * 3, b = (index ? index.getX(i + 1) : i + 1) * 3, c = (index ? index.getX(i + 2) : i + 2) * 3;
      const minY = Math.min(local[a + 1], local[b + 1], local[c + 1]), maxY = Math.max(local[a + 1], local[b + 1], local[c + 1]);
      const minZ = Math.min(local[a + 2], local[b + 2], local[c + 2]), maxZ = Math.max(local[a + 2], local[b + 2], local[c + 2]);
      if ((minY <= lowerY && maxY >= lowerY) || (minY <= rearY && maxY >= rearY) || (minZ <= trunkZ && maxZ >= trunkZ && maxY > rearY)) {
        for (const offset of [a, b, c]) vertices.push(local[offset], local[offset + 1], local[offset + 2]);
      }
    }
  }
  const geometry = new THREE.BufferGeometry(); geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
  return new THREE.Mesh(geometry, new THREE.MeshBasicMaterial());
}

export function measureVehicleFit(vehicle, bodyMeshes, rimMeshes, tyreMeshes, shellMeshes = bodyMeshes) {
  vehicle.updateWorldMatrix(true, true);
  const rims = wheelBounds(vehicle, rimMeshes), tyres = wheelBounds(vehicle, tyreMeshes);
  const inverse = vehicle.matrixWorld.clone().invert();
  const body = new THREE.Box3();
  for (const mesh of bodyMeshes) {
    const transform = new THREE.Matrix4().multiplyMatrices(inverse, mesh.matrixWorld);
    const positions = mesh.geometry.attributes.position;
    for (let i = 0; i < positions.count; i++) body.expandByPoint(new THREE.Vector3().fromBufferAttribute(positions, i).applyMatrix4(transform));
  }
  const mounts = [...rims].map(([key, bounds]) => {
    const side = key.startsWith('left') ? -1 : 1;
    const tyre = tyres.get(key) || bounds;
    const center = tyre.getCenter(new THREE.Vector3());
    const outerX = side < 0 ? bounds.min.x : bounds.max.x;
    center.x = outerX - side * 0.008;
    return { key, side, center, radius: (bounds.max.y - bounds.min.y) / 2, depth: bounds.max.x - bounds.min.x, rimBounds: bounds, tyreBounds: tyre };
  });
  const width = body.max.x - body.min.x;
  const trunkZ = body.min.z + (body.max.z - body.min.z) * 0.073;
  const surface = mountingSurface(vehicle, shellMeshes, body.min.y + 0.055, body.min.y + 0.14, trunkZ);
  const cast = (origin, direction) => new THREE.Raycaster(origin, direction).intersectObject(surface, false)[0]?.point || null;
  const profiles = [];
  for (const side of [-1, 1]) {
    const rear = mounts.find(mount => mount.side === side && mount.key.endsWith('rear'));
    const front = mounts.find(mount => mount.side === side && mount.key.endsWith('front'));
    if (!rear || !front) continue;
    const start = rear.tyreBounds.max.z + 0.06, end = front.tyreBounds.min.z - 0.06;
    const points = Array.from({ length: 13 }, (_, i) => cast(new THREE.Vector3(side * (width + 1), body.min.y + 0.055, THREE.MathUtils.lerp(start, end, i / 12)), new THREE.Vector3(-side, 0, 0)));
    profiles.push({ kind: 'skirt', side, start, end, points, outward: new THREE.Vector3(side, 0, 0) });
  }
  const bumperWidth = (mounts.length ? Math.min(...mounts.map(mount => Math.abs(mount.center.x))) : width * 0.4) * 0.95;
  for (const side of [-1, 1]) {
    const y = body.min.y + (side > 0 ? 0.055 : 0.14);
    const points = Array.from({ length: 17 }, (_, i) => cast(new THREE.Vector3(THREE.MathUtils.lerp(-bumperWidth, bumperWidth, i / 16), y, side * 4), new THREE.Vector3(0, 0, -side)));
    profiles.push({ kind: side > 0 ? 'front' : 'rear', points, outward: new THREE.Vector3(0, 0, side) });
  }
  const trunkWidth = width * 0.34;
  const trunk = Array.from({ length: 17 }, (_, i) => cast(new THREE.Vector3(THREE.MathUtils.lerp(-trunkWidth, trunkWidth, i / 16), body.max.y + 1, trunkZ), new THREE.Vector3(0, -1, 0)));
  profiles.push({ kind: 'lip', points: trunk, outward: new THREE.Vector3(0, 0, -1) });
  const supports = [-1, 1].map(side => cast(new THREE.Vector3(side * width * 0.26, body.max.y + 1, trunkZ), new THREE.Vector3(0, -1, 0)));
  surface.geometry.dispose(); surface.material.dispose();
  return { mounts, body, profiles, supports };
}

export function profileGeometry(points, outward, height = 0.035, depth = 0.05) {
  const vertices = [], indices = [];
  // Missing surface hits split the strip; never bridge a hole or invent a mounting surface.
  for (let i = 0; i < points.length - 1; i++) {
    if (!points[i] || !points[i + 1]) continue;
    const offset = vertices.length / 3;
    for (const point of [points[i], points[i + 1]]) for (const y of [-height / 2, height / 2]) for (const extent of [-0.012, depth - 0.012]) {
      const vertex = point.clone().addScaledVector(outward, extent); vertex.y += y;
      vertices.push(...vertex.toArray());
    }
    const center = new THREE.Vector3();
    for (let j = 0; j < 8; j++) center.add(new THREE.Vector3().fromArray(vertices, (offset + j) * 3));
    center.multiplyScalar(1 / 8);
    for (const [a, b, c, d] of [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]]) {
      const first = new THREE.Vector3().fromArray(vertices, (offset + a) * 3), second = new THREE.Vector3().fromArray(vertices, (offset + b) * 3), third = new THREE.Vector3().fromArray(vertices, (offset + c) * 3);
      const inward = second.clone().sub(first).cross(third.clone().sub(first)).dot(first.clone().sub(center)) < 0;
      const face = inward ? [a, c, b, a, d, c] : [a, b, c, a, c, d];
      indices.push(...face.map(index => offset + index));
    }
  }
  const geometry = new THREE.BufferGeometry(); geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3)); geometry.setIndex(indices); geometry.computeVertexNormals();
  return geometry;
}
