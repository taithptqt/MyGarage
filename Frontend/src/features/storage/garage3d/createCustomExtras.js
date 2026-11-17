import * as THREE from 'three';
import { DecalGeometry } from 'three/addons/geometries/DecalGeometry.js';
import { measureVehicleFit, profileGeometry } from './vehicleFit.js';

export function drawSticker(canvas, sticker) {
  const ctx = canvas.getContext('2d');
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.save(); ctx.scale(canvas.width / 512, canvas.height / 256);
  ctx.fillStyle = sticker.color;
  switch (sticker.design) {
    case 'stripes':
      for (const x of [90, 230, 370]) { ctx.beginPath(); ctx.moveTo(x, 24); ctx.lineTo(x + 75, 24); ctx.lineTo(x - 15, 232); ctx.lineTo(x - 90, 232); ctx.closePath(); ctx.fill(); }
      break;
    case 'checker':
      for (let x = 0; x < 8; x++) for (let y = 0; y < 4; y++) if ((x + y) % 2 === 0) ctx.fillRect(x * 64, y * 64, 64, 64);
      break;
    case 'lightning':
      ctx.beginPath(); [[300, 10], [130, 145], [235, 145], [190, 246], [390, 100], [280, 100]].forEach(([x, y], i) => i ? ctx.lineTo(x, y) : ctx.moveTo(x, y)); ctx.closePath(); ctx.fill();
      break;
    case 'number':
      ctx.beginPath(); ctx.ellipse(256, 128, 120, 120, 0, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = '#172136'; ctx.font = 'bold 130px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(sticker.text || '07', 256, 138, 210);
      break;
    default:
      ctx.font = 'italic bold 92px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(sticker.text || 'MY GARAGE', 256, 132, 480);
  }
  ctx.restore();
}

export function stickerRay(sticker) {
  if (sticker.surface === 'hood' || sticker.surface === 'roof') return {
    origin: [sticker.x * 0.5, 4, sticker.surface === 'hood' ? 1.65 + sticker.y * 0.22 : -0.25 + sticker.y * 0.35], direction: [0, -1, 0],
  };
  return { origin: [sticker.surface === 'left' ? -3 : 3, 0.79 + sticker.y * 0.14, sticker.x * 0.8], direction: [sticker.surface === 'left' ? 1 : -1, 0, 0] };
}

export function installBodyFinish(material, uniforms) {
  material.onBeforeCompile = shader => {
    Object.assign(shader.uniforms, uniforms);
    shader.vertexShader = shader.vertexShader.replace('#include <common>', '#include <common>\nvarying vec3 customPoint; uniform mat4 customInverse;')
      .replace('#include <begin_vertex>', '#include <begin_vertex>\ncustomPoint = (customInverse * modelMatrix * vec4(transformed, 1.0)).xyz;');
    shader.fragmentShader = shader.fragmentShader.replace('#include <common>', '#include <common>\nvarying vec3 customPoint; uniform float customRoof; uniform float customMirrors;')
      .replace('#include <color_fragment>', `#include <color_fragment>
        float roofMask = step(1.31, customPoint.y) * (1.0-step(0.77, abs(customPoint.x))) * step(-0.85, customPoint.z) * (1.0-step(0.45, customPoint.z));
        float mirrorMask = step(0.97, abs(customPoint.x)) * step(0.85, customPoint.y) * (1.0-step(1.17, customPoint.y)) * step(0.45, customPoint.z) * (1.0-step(1.35, customPoint.z));
        float weave = mod(floor(customPoint.x*150.0) + floor(customPoint.z*150.0) + floor(customPoint.y*150.0), 2.0);
        if (customRoof > 0.5 && roofMask > 0.5) diffuseColor.rgb = vec3(customRoof > 1.5 ? 0.025 + weave*0.035 : 0.012);
        if (customMirrors > 0.5 && mirrorMask > 0.5) diffuseColor.rgb = vec3(customMirrors > 1.5 ? 0.025 + weave*0.035 : 0.012);
      `);
  };
  material.customProgramCacheKey = () => 'mygarage-body-finish-v1';
  material.needsUpdate = true;
}

export function createCustomExtras(vehicle, bodyMeshes, rimMeshes, tyreMeshes, shellMeshes) {
  const group = new THREE.Group(); vehicle.add(group);
  const decals = new Map();
  const kit = new THREE.Group(), diffuser = new THREE.Group(), lip = new THREE.Group(), wing = new THREE.Group(), wheels = new THREE.Group();
  group.add(kit, diffuser, lip, wing, wheels);
  kit.visible = diffuser.visible = lip.visible = wing.visible = false;
  const fit = measureVehicleFit(vehicle, bodyMeshes, rimMeshes, tyreMeshes, shellMeshes);
  const black = new THREE.MeshStandardMaterial({ color: '#141c26', roughness: 0.4, metalness: 0.5 });
  const wheelMaterial = new THREE.MeshStandardMaterial({ color: '#b9c0ca', roughness: 0.28, metalness: 0.85 });
  const part = (parent, size, position) => { const mesh = new THREE.Mesh(new THREE.BoxGeometry(...size), black); mesh.position.set(...position); parent.add(mesh); return mesh; };
  for (const profile of fit.profiles) {
    const parent = profile.kind === 'lip' ? lip : profile.kind === 'rear' ? diffuser : kit;
    parent.add(new THREE.Mesh(profileGeometry(profile.points, profile.outward), black));
    if (profile.kind === 'rear') for (const index of [3, 6, 8, 10, 13]) {
      const point = profile.points[index];
      if (point) part(diffuser, [0.025, 0.065, 0.075], [point.x, point.y - 0.02, point.z - 0.01]);
    }
  }
  if (fit.supports.every(Boolean)) {
    const top = Math.max(...fit.supports.map(point => point.y)) + 0.18;
    const trunkZ = fit.supports[0].z;
    part(wing, [(fit.body.max.x - fit.body.min.x) * 0.77, 0.035, 0.22], [0, top, trunkZ]);
    for (const base of fit.supports) part(wing, [0.045, top - base.y, 0.09], [base.x, (top + base.y) / 2, base.z]);
  }
  let wheelStyle = '';
  const disposeMesh = object => {
    object.traverse(child => { child.geometry?.dispose(); });
    object.removeFromParent();
  };
  function updateWheels(style) {
    if (style === wheelStyle) return;
    wheelStyle = style;
    [...wheels.children].forEach(disposeMesh);
    rimMeshes.forEach(mesh => { mesh.visible = style === 'stock'; });
    if (style === 'stock') return;
    for (const mount of fit.mounts) {
      const radius = mount.radius, tube = radius * 0.045;
      const wheel = new THREE.Group(); wheel.name = `custom-wheel-${mount.key}`; wheel.position.copy(mount.center); wheels.add(wheel);
      const ring = new THREE.Mesh(new THREE.TorusGeometry(radius - tube, tube, 10, 48), wheelMaterial); ring.rotation.y = Math.PI / 2; wheel.add(ring);
      const hubRadius = radius * 0.22;
      const hub = new THREE.Mesh(new THREE.CylinderGeometry(hubRadius, hubRadius, mount.depth * 0.08, 20), wheelMaterial); hub.rotation.z = Math.PI / 2; wheel.add(hub);
      const count = style === 'five' ? 5 : 10;
      for (let i = 0; i < count; i++) {
        const angle = i / count * Math.PI * 2;
        const length = radius - tube - hubRadius;
        const offset = hubRadius + length / 2;
        const spoke = new THREE.Mesh(new THREE.BoxGeometry(mount.depth * 0.08, length, radius * (style === 'five' ? 0.15 : 0.075)), wheelMaterial);
        spoke.rotation.x = angle; spoke.position.set(0, Math.cos(angle) * offset, Math.sin(angle) * offset); wheel.add(spoke);
      }
    }
  }
  function updateDecals(stickers) {
    const ids = new Set(stickers.map(sticker => sticker.id));
    for (const [id, record] of decals) if (!ids.has(id)) { disposeMesh(record.mesh); record.material.dispose(); record.texture.dispose(); decals.delete(id); }
    vehicle.updateWorldMatrix(true, true);
    for (const [index, sticker] of stickers.entries()) {
      const signature = JSON.stringify(sticker), current = decals.get(sticker.id);
      if (current?.signature === signature) { current.mesh.renderOrder = index + 1; current.material.polygonOffsetFactor = -4 - index; continue; }
      if (current) { disposeMesh(current.mesh); current.material.dispose(); current.texture.dispose(); decals.delete(sticker.id); }
      const ray = stickerRay(sticker);
      const raycaster = new THREE.Raycaster(vehicle.localToWorld(new THREE.Vector3(...ray.origin)), new THREE.Vector3(...ray.direction));
      const hit = raycaster.intersectObjects(bodyMeshes, false)[0];
      if (!hit) continue;
      const normal = hit.face.normal.clone().applyNormalMatrix(new THREE.Matrix3().getNormalMatrix(hit.object.matrixWorld));
      const rotation = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 0, 1), normal)
        .multiply(new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 0, 1), THREE.MathUtils.degToRad(sticker.rotation)));
      const geometry = new DecalGeometry(hit.object, hit.point, new THREE.Euler().setFromQuaternion(rotation), new THREE.Vector3(sticker.size, sticker.size * 0.5, 0.28));
      geometry.applyMatrix4(vehicle.matrixWorld.clone().invert());
      const canvas = document.createElement('canvas'); canvas.width = 512; canvas.height = 256; drawSticker(canvas, sticker);
      const texture = new THREE.CanvasTexture(canvas); texture.colorSpace = THREE.SRGBColorSpace;
      const material = new THREE.MeshStandardMaterial({ map: texture, transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -4 - index, roughness: 0.5 });
      const mesh = new THREE.Mesh(geometry, material); mesh.renderOrder = index + 1; group.add(mesh);
      decals.set(sticker.id, { signature, mesh, texture, material });
    }
  }
  return {
    dispose() { if (!wheels.children.length) wheelMaterial.dispose(); },
    set(config, rimColor) {
      kit.visible = config.bodyKit !== 'stock'; diffuser.visible = config.bodyKit === 'sport'; lip.visible = config.spoiler === 'lip';
      wing.visible = config.spoiler === 'sport';
      updateWheels(config.wheelStyle || 'stock'); wheelMaterial.color.set(rimColor || '#b9c0ca');
      updateDecals(config.stickers || []);
    },
  };
}
