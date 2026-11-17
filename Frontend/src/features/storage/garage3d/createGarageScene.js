import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { DRACOLoader } from 'three/addons/loaders/DRACOLoader.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { BAYS, MODEL_URL, VIEWS } from './garageConfig.js';
import { CUSTOM_GROUPS } from './customConfig.js';
import { createCustomExtras, installBodyFinish } from './createCustomExtras.js';

export function createGarageScene(host, callbacks, options = {}) {
  const bays = options.bays || BAYS;
  if (!bays.length) throw new Error('Chưa có vị trí để hiển thị.');
  if (options.readOnly && (!bays.some(bay => bay.id === options.initialBayId) || !options.parkedVehicles?.some(vehicle => vehicle.bayId === options.initialBayId))) throw new Error('Chưa xác nhận vị trí xe trong garage này.');
  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, window.innerWidth < 800 ? 1 : 1.5));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.25;
  host.appendChild(renderer.domElement);
  renderer.domElement.setAttribute('aria-label', 'Model VinFast Lux A2.0 trong garage 3D. Dùng các nút bên cạnh để thay đổi góc nhìn.');
  const scene = new THREE.Scene();
  scene.background = new THREE.Color('#172136');
  scene.fog = new THREE.Fog('#172136', 25, 65);
  const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.dampingFactor = 0.075;
  controls.minDistance = 3.5;
  controls.maxDistance = 38;
  controls.maxPolarAngle = Math.PI / 2 - 0.025;
  controls.autoRotateSpeed = 0.6;
  controls.enablePan = true;
  const environment = new RoomEnvironment();
  const pmrem = new THREE.PMREMGenerator(renderer);
  const environmentMap = pmrem.fromScene(environment, 0.04);
  scene.environment = environmentMap.texture;
  environment.dispose();
  pmrem.dispose();
  scene.add(new THREE.HemisphereLight(0xd5eaff, 0x303849, 3));
  const key = new THREE.DirectionalLight(0xffffff, 3);
  key.position.set(4, 10, 5);
  scene.add(key);
  const rim = new THREE.DirectionalLight(0x89baff, 2);
  rim.position.set(-8, 5, -8);
  scene.add(rim);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(60, 60), new THREE.MeshStandardMaterial({ color: '#1e2b42', roughness: 0.85, metalness: 0.15 }));
  floor.rotation.x = -Math.PI / 2;
  scene.add(floor);
  const grid = new THREE.GridHelper(50, 50, '#34445d', '#25344d');
  grid.position.y = 0.005;
  scene.add(grid);
  const bayMeshes = [];
  const outlines = new Map();
  for (const bay of bays) {
    const pad = new THREE.Mesh(new THREE.PlaneGeometry(3.5, 6.1), new THREE.MeshBasicMaterial({ color: '#253650', transparent: true, opacity: 0.75, side: THREE.DoubleSide }));
    pad.rotation.x = -Math.PI / 2;
    pad.position.set(bay.x, 0.012, bay.z);
    pad.userData.bay = bay.id;
    scene.add(pad); bayMeshes.push(pad);
    const points = [[-1.75, -3.05], [-1.75, 3.05], [1.75, 3.05], [1.75, -3.05], [-1.75, -3.05]].map(([x, z]) => new THREE.Vector3(x + bay.x, 0.025, z + bay.z));
    const outline = new THREE.Line(new THREE.BufferGeometry().setFromPoints(points), new THREE.LineBasicMaterial({ color: '#52627b' }));
    scene.add(outline); outlines.set(bay.id, outline);
    const canvas = document.createElement('canvas'); canvas.width = 256; canvas.height = 96;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = '#b9c8da'; ctx.font = '600 48px sans-serif'; ctx.textAlign = 'center'; ctx.fillText(bay.code || String(bay.id), 128, 62);
    const label = new THREE.Mesh(new THREE.PlaneGeometry(1.4, 0.55), new THREE.MeshBasicMaterial({ map: new THREE.CanvasTexture(canvas), transparent: true, depthWrite: false }));
    label.rotation.x = -Math.PI / 2; label.position.set(bay.x, 0.03, bay.z + 2.5); scene.add(label);
  }
  const vehicle = new THREE.Group(); scene.add(vehicle);
  let disposed = false, frame, loadedModel, extras, selectedBay = bays.find(bay => bay.id === options.initialBayId) || bays[1] || bays[0], activeViewId = 'perspective';
  if (options.readOnly) vehicle.position.set(selectedBay.x, 0, selectedBay.z);
  const materials = new Set(), originalColors = new Map();
  const originalMaterials = new Map(), rimMaterials = new Set(), caliperMaterials = new Set();
  const bodyMeshes = [], rimMeshes = [], tyreMeshes = [], shellMeshes = [], glassMaterials = new Set();
  const finishUniforms = { customInverse: { value: new THREE.Matrix4() }, customRoof: { value: 0 }, customMirrors: { value: 0 } };
  scene.onBeforeRender = () => { finishUniforms.customInverse.value.copy(vehicle.matrixWorld).invert(); };
  function setView(viewId = 'perspective') {
    activeViewId = viewId;
    const view = VIEWS.find(item => item.id === viewId) || VIEWS[0];
    const target = viewId === 'garage' ? new THREE.Vector3(0, 0.5, 0) : new THREE.Vector3(selectedBay.x, 0.85, selectedBay.z);
    controls.target.copy(target);
    camera.position.copy(target).add(new THREE.Vector3(...view.position).multiplyScalar(Math.max(1, 1.2 / Math.max(camera.aspect, 0.4))));
    controls.update();
  }
  function setBay(id) {
    if (options.readOnly && !bays.some(bay => bay.id === id)) return;
    selectedBay = bays.find(bay => bay.id === id) || bays[0];
    if (!options.readOnly) vehicle.position.set(selectedBay.x, 0, selectedBay.z);
    outlines.forEach((outline, bayId) => outline.material.color.set(bayId === selectedBay.id ? '#5eead4' : '#52627b'));
    setView();
  }
  setBay(selectedBay.id);
  function disposeObject(object) {
    const textures = new Set(), mats = new Set();
    object.traverse(child => {
      child.geometry?.dispose();
      for (const mat of child.material ? (Array.isArray(child.material) ? child.material : [child.material]) : []) mats.add(mat);
    });
    mats.forEach(mat => { Object.values(mat).forEach(value => { if (value?.isTexture) textures.add(value); }); mat.dispose(); });
    textures.forEach(texture => { texture.dispose(); texture.source?.data?.close?.(); });
  }
  const draco = new DRACOLoader().setDecoderPath('/models/draco/');
  new GLTFLoader().setDRACOLoader(draco).load(MODEL_URL, gltf => {
    if (disposed) { disposeObject(gltf.scene); return; }
    loadedModel = gltf.scene;
    let box = new THREE.Box3().setFromObject(loadedModel), size = box.getSize(new THREE.Vector3());
    loadedModel.scale.setScalar(4.95 / Math.max(size.x, size.y, size.z));
    loadedModel.updateMatrixWorld(true);
    box.setFromObject(loadedModel); size = box.getSize(new THREE.Vector3());
    if (size.x > size.z) loadedModel.rotation.y += Math.PI / 2;
    loadedModel.updateMatrixWorld(true); box.setFromObject(loadedModel);
    const center = box.getCenter(new THREE.Vector3());
    loadedModel.position.sub(new THREE.Vector3(center.x, box.min.y, center.z));
    loadedModel.traverse(child => {
      if (child.isMesh) shellMeshes.push(child);
      for (const material of child.material ? (Array.isArray(child.material) ? child.material : [child.material]) : []) {
        originalMaterials.set(material, { color: material.color.clone(), roughness: material.roughness, metalness: material.metalness, map: material.map, opacity: material.opacity, transparent: material.transparent });
        if (/^rim_ao/i.test(material.name)) { rimMaterials.add(material); if (child.isMesh) rimMeshes.push(child); }
        if (/^EXT_TYRE/i.test(material.name) && child.isMesh) tyreMeshes.push(child);
        if (/^caliper/i.test(material.name)) caliperMaterials.add(material);
        if (/^ext_glass/i.test(material.name)) glassMaterials.add(material);
        if (/^EXT_Body/i.test(material.name)) { materials.add(material); originalColors.set(material, material.color.clone()); if (child.isMesh) bodyMeshes.push(child); }
      }
    });
    vehicle.add(loadedModel);
    if (!options.readOnly) {
      materials.forEach(material => installBodyFinish(material, finishUniforms));
      extras = createCustomExtras(vehicle, bodyMeshes, rimMeshes, tyreMeshes, shellMeshes);
    } else {
      const plateLabel = (group, text) => {
        const canvas = document.createElement('canvas'); canvas.width = 512; canvas.height = 96;
        const ctx = canvas.getContext('2d'); ctx.fillStyle = '#102636'; ctx.fillRect(0, 0, 512, 96); ctx.fillStyle = '#a3f2e8'; ctx.font = 'bold 40px sans-serif'; ctx.textAlign = 'center'; ctx.fillText(text, 256, 62, 480);
        const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(canvas), depthTest: false }));
        sprite.position.set(0, 1.9, 0); sprite.scale.set(2.8, 0.525, 1); group.add(sprite);
      };
      const parked = options.parkedVehicles || [];
      const first = parked.filter(item => item.bayId === selectedBay.id);
      if (first.length) plateLabel(vehicle, first.map(item => item.licensePlate).join(' · '));
      const shownBays = new Set([selectedBay.id]);
      for (const item of parked) {
        if (shownBays.has(item.bayId)) continue;
        const bay = bays.find(bay => bay.id === item.bayId); if (!bay) continue;
        shownBays.add(item.bayId);
        const copy = new THREE.Group(); copy.position.set(bay.x, 0, bay.z); scene.add(copy);
        copy.add(loadedModel.clone(true)); plateLabel(copy, parked.filter(other => other.bayId === item.bayId).map(other => other.licensePlate).join(' · '));
      }
    }
    callbacks.onReady();
  }, event => { if (!disposed) callbacks.onProgress(event.total ? Math.round(event.loaded / event.total * 100) : null); }, () => { if (!disposed) callbacks.onError('Không tải được model 3D. Vui lòng thử lại.'); });
  const observer = new ResizeObserver(() => {
    const { width, height } = host.getBoundingClientRect();
    renderer.setSize(width, height); camera.aspect = width / Math.max(height, 1); camera.updateProjectionMatrix(); setView(activeViewId);
  });
  observer.observe(host);
  const raycaster = new THREE.Raycaster(), pointer = new THREE.Vector2();
  let down = null;
  const pointerDown = event => { down = [event.clientX, event.clientY]; };
  const pointerUp = event => {
    if (options.readOnly) return;
    if (!down || Math.hypot(event.clientX - down[0], event.clientY - down[1]) > 5) return;
    const rect = renderer.domElement.getBoundingClientRect();
    pointer.set((event.clientX - rect.left) / rect.width * 2 - 1, -(event.clientY - rect.top) / rect.height * 2 + 1);
    raycaster.setFromCamera(pointer, camera);
    const hit = raycaster.intersectObjects(bayMeshes)[0];
    if (hit) callbacks.onSelectBay(hit.object.userData.bay);
  };
  const contextLost = event => { event.preventDefault(); callbacks.onError('Trình duyệt đã tạm ngắt đồ họa 3D. Vui lòng tải lại khung xem.'); };
  renderer.domElement.addEventListener('pointerdown', pointerDown);
  renderer.domElement.addEventListener('pointerup', pointerUp);
  renderer.domElement.addEventListener('webglcontextlost', contextLost);
  function animate() { if (disposed) return; frame = requestAnimationFrame(animate); if (!document.hidden) { controls.update(); renderer.render(scene, camera); } }
  animate();
  return {
    setBay, setView,
    projectVehiclePoint(vehicleId, position) {
      const parked = options.parkedVehicles?.find(item => item.vehicleId === vehicleId);
      const bay = bays.find(item => item.id === parked?.bayId);
      if (!bay || !loadedModel) return null;
      const point = new THREE.Vector3(...position).add(new THREE.Vector3(bay.x, 0, bay.z)).project(camera);
      return { x: (point.x + 1) * host.clientWidth / 2, y: (1 - point.y) * host.clientHeight / 2,
        visible: point.z >= -1 && point.z <= 1 && Math.abs(point.x) < 1 && Math.abs(point.y) < 1 };
    },
    setRotate(value) { controls.autoRotate = value; },
    setPaint(value) { materials.forEach(material => material.color.copy(value ? new THREE.Color(value) : originalColors.get(material))); },
    setCustomization(config) {
      const applyColor = (targets, groupKey) => {
        const option = CUSTOM_GROUPS.find(group => group.key === groupKey)?.options.find(item => item.id === config[groupKey]);
        targets.forEach(material => {
          const original = originalMaterials.get(material);
          material.color.copy(option?.color ? new THREE.Color(option.color) : original.color);
          material.map = option?.color ? null : original.map;
          material.metalness = option?.color ? 0.75 : original.metalness;
          material.roughness = option?.color ? 0.3 : original.roughness;
          material.needsUpdate = true;
        });
      };
      materials.forEach(material => {
        const original = originalMaterials.get(material);
        material.color.copy(config.paint ? new THREE.Color(config.paint) : original.color);
        material.roughness = config.finish === 'matte' ? 0.8 : config.finish === 'metallic' ? 0.28 : original.roughness;
        material.metalness = config.finish === 'matte' ? 0.15 : config.finish === 'metallic' ? 0.85 : original.metalness;
      });
      applyColor(rimMaterials, 'rims');
      applyColor(caliperMaterials, 'calipers');
      finishUniforms.customRoof.value = config.roof === 'black' ? 1 : config.roof === 'carbon' ? 2 : 0;
      finishUniforms.customMirrors.value = config.mirrors === 'black' ? 1 : config.mirrors === 'carbon' ? 2 : 0;
      glassMaterials.forEach(material => {
        const original = originalMaterials.get(material);
        const tinted = config.tint && config.tint !== 'stock';
        material.color.copy(tinted ? new THREE.Color(config.tint === 'medium' ? '#172331' : '#647485') : original.color);
        material.opacity = tinted ? config.tint === 'medium' ? 0.85 : 0.55 : original.opacity;
        material.transparent = tinted ? true : original.transparent; material.needsUpdate = true;
      });
      const rimColor = CUSTOM_GROUPS.find(group => group.key === 'rims').options.find(option => option.id === config.rims)?.color;
      extras?.set(config, rimColor);
    },
    zoom(factor) { camera.position.sub(controls.target).multiplyScalar(factor).clampLength(controls.minDistance, controls.maxDistance).add(controls.target); controls.update(); },
    dispose() {
      disposed = true; cancelAnimationFrame(frame); observer.disconnect(); controls.dispose(); draco.dispose();
      renderer.domElement.removeEventListener('pointerdown', pointerDown); renderer.domElement.removeEventListener('pointerup', pointerUp); renderer.domElement.removeEventListener('webglcontextlost', contextLost);
      // Restore detached maps so the shared model resources are disposed exactly once.
      originalMaterials.forEach((original, material) => { material.map = original.map; });
      extras?.dispose();
      disposeObject(scene);
      environmentMap.dispose(); renderer.dispose(); renderer.domElement.remove();
    },
  };
}
