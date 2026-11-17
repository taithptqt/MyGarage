import fs from 'node:fs';
import { createRequire } from 'node:module';
import * as THREE from 'three';

export async function loadVehicleModelFixture() {
  const module = { exports: {} };
  new Function('module', 'exports', 'require', '__dirname', fs.readFileSync('public/models/draco/draco_decoder.js', 'utf8'))(module, module.exports, createRequire(import.meta.url), process.cwd());
  const draco = await module.exports({});
  const data = fs.readFileSync('public/models/vinfast-lux-a20/model.glb');
  const jsonSize = data.readUInt32LE(12), gltf = JSON.parse(data.subarray(20, 20 + jsonSize)), binary = data.subarray(28 + jsonSize);
  const nodes = gltf.nodes.map(node => {
    const group = new THREE.Group();
    if (node.translation) group.position.fromArray(node.translation);
    if (node.rotation) group.quaternion.fromArray(node.rotation);
    if (node.scale) group.scale.fromArray(node.scale);
    return group;
  });
  gltf.nodes.forEach((node, index) => (node.children || []).forEach(child => nodes[index].add(nodes[child])));
  const root = new THREE.Group(); gltf.scenes[gltf.scene || 0].nodes.forEach(index => root.add(nodes[index]));
  const meshes = [];
  for (const [index, node] of gltf.nodes.entries()) {
    if (node.mesh == null) continue;
    for (const primitive of gltf.meshes[node.mesh].primitives) {
      const name = gltf.materials[primitive.material].name;
      const extension = primitive.extensions.KHR_draco_mesh_compression, view = gltf.bufferViews[extension.bufferView];
      const bytes = binary.subarray(view.byteOffset || 0, (view.byteOffset || 0) + view.byteLength);
      const decoder = new draco.Decoder(), buffer = new draco.DecoderBuffer(), decoded = new draco.Mesh();
      buffer.Init(new Int8Array(bytes), bytes.length);
      const status = decoder.DecodeBufferToMesh(buffer, decoded);
      if (!status.ok()) throw new Error(status.error_msg());
      const attribute = decoder.GetAttributeByUniqueId(decoded, extension.attributes.POSITION), values = new draco.DracoFloat32Array();
      decoder.GetAttributeFloatForAllPoints(decoded, attribute, values);
      const positions = new Float32Array(decoded.num_points() * 3);
      for (let i = 0; i < positions.length; i++) positions[i] = values.GetValue(i);
      const face = new draco.DracoInt32Array(), indices = [];
      for (let i = 0; i < decoded.num_faces(); i++) { decoder.GetFaceFromMesh(decoded, i, face); for (let j = 0; j < 3; j++) indices.push(face.GetValue(j)); }
      const geometry = new THREE.BufferGeometry(); geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3)); geometry.setIndex(indices); geometry.computeVertexNormals();
      const material = new THREE.MeshBasicMaterial(); material.name = name;
      const mesh = new THREE.Mesh(geometry, material); nodes[index].add(mesh); meshes.push(mesh);
      for (const resource of [values, face, decoded, buffer, decoder]) draco.destroy(resource);
    }
  }
  root.updateMatrixWorld(true); const bounds = new THREE.Box3();
  gltf.nodes.forEach((node, index) => { if (node.mesh != null) for (const primitive of gltf.meshes[node.mesh].primitives) {
    const accessor = gltf.accessors[primitive.attributes.POSITION];
    bounds.union(new THREE.Box3(new THREE.Vector3(...accessor.min), new THREE.Vector3(...accessor.max)).applyMatrix4(nodes[index].matrixWorld));
  } });
  const size = bounds.getSize(new THREE.Vector3()), scale = 4.95 / Math.max(...size.toArray());
  root.scale.setScalar(scale); root.position.set(-bounds.getCenter(new THREE.Vector3()).x * scale, -bounds.min.y * scale, -bounds.getCenter(new THREE.Vector3()).z * scale);
  root.updateMatrixWorld(true);
  const vehicle = new THREE.Group(); vehicle.add(root); vehicle.updateMatrixWorld(true);
  return { vehicle, root, meshes, shellMeshes: meshes, bodyMeshes: meshes.filter(mesh => /^EXT_Body/i.test(mesh.material.name)), rimMeshes: meshes.filter(mesh => /^rim_ao/i.test(mesh.material.name)), tyreMeshes: meshes.filter(mesh => /^EXT_TYRE/i.test(mesh.material.name)) };
}
