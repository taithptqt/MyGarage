import { useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { getMyGarageLocations } from '../api/vehicleLocationApi';
import { createGarageScene } from './createGarageScene';
import { LOCATION_COPY, layoutGarageBays } from './garageLocationConfig';
import { VIEWS } from './garageConfig';
import RepairCallouts from './RepairCallouts';
import { REPAIR_COPY } from './repairConfig';
import './Garage3DPage.css';

const formatTime = value => value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : '—';

export default function CustomerGarage3DPage() {
  const [data, setData] = useState({ branches: [], vehicles: [] }), [branchId, setBranchId] = useState(''), [vehicleId, setVehicleId] = useState('');
  const [loading, setLoading] = useState(true), [error, setError] = useState(''), [version, setVersion] = useState(0), [sceneError, setSceneError] = useState(''), [ready, setReady] = useState(false);
  const host = useRef(null), scene = useRef(null);
  useEffect(() => {
    let active = true, busy = false;
    const load = async () => {
      if (busy) return; busy = true;
      try {
        const result = await getMyGarageLocations();
        if (active) { setData(result); setError(''); }
      } catch { if (active) setError(LOCATION_COPY.error); }
      finally { busy = false; if (active) setLoading(false); }
    };
    void load();
    const refresh = () => { if (!document.hidden) void load(); };
    const timer = setInterval(refresh, 15000); window.addEventListener('focus', refresh);
    return () => { active = false; clearInterval(timer); window.removeEventListener('focus', refresh); };
  }, [version]);
  const branch = data.branches.find(item => String(item.id) === branchId) || data.branches[0];
  const branchVehicles = data.vehicles.filter(vehicle => vehicle.branchId === branch?.id);
  const parked = branchVehicles.filter(vehicle => vehicle.bayId != null && branch?.bays.some(bay => bay.id === vehicle.bayId));
  const selected = branchVehicles.find(vehicle => String(vehicle.vehicleId) === vehicleId) || parked[0] || branchVehicles[0];
  const bays = useMemo(() => layoutGarageBays(branch?.bays || []), [branch]);
  // Changes in positions recreate the scene; camera-only selections never move a parked vehicle.
  const signature = JSON.stringify({ bays, parked: parked.map(vehicle => ({ vehicleId: vehicle.vehicleId, bayId: vehicle.bayId, licensePlate: vehicle.licensePlate })) });
  useEffect(() => {
    if (!host.current || !parked.length || !bays.length) { setReady(false); return; }
    setReady(false); setSceneError('');
    try { scene.current = createGarageScene(host.current, { onProgress: () => {}, onReady: () => setReady(true), onError: setSceneError, onSelectBay: () => {} }, { bays, initialBayId: parked[0].bayId, readOnly: true, parkedVehicles: parked }); }
    catch { setSceneError('Chưa mở được sơ đồ 3D trên thiết bị này. Vị trí xe vẫn có trong danh sách.'); }
    return () => { scene.current?.dispose(); scene.current = null; };
  }, [signature]);
  useEffect(() => { if (ready && selected?.bayId != null) scene.current?.setBay(selected.bayId); }, [ready, selected?.bayId]);
  return <main className="garage3d-page garage3d-owned-page">
    <header className="garage3d-topbar"><h1>{LOCATION_COPY.title}</h1><Link to="/my-garage-dashboard">Theo dõi sửa chữa</Link><Link className="garage3d-back" to="/storage/garage-3d?mode=custom">Custom xe · minh họa</Link></header>
    {loading && <p className="garage3d-location-message" role="status">{LOCATION_COPY.loading}</p>}
    {error && <p className="garage3d-location-message" role="alert">{error}</p>}
    <div className="garage3d-owned-workspace">
      <aside className="garage3d-sidebar">
        <label className="garage3d-input-label">Chi nhánh có xe của bạn<select value={branch?.id || ''} onChange={event => { setBranchId(event.target.value); setVehicleId(''); }}>{data.branches.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
        {!loading && !branch && <p className="garage3d-muted">{LOCATION_COPY.noBranch}</p>}
        <div className="garage3d-bays">{branchVehicles.map(vehicle => <button key={vehicle.vehicleId} aria-pressed={selected?.vehicleId === vehicle.vehicleId} className={selected?.vehicleId === vehicle.vehicleId ? 'active' : ''} onClick={() => setVehicleId(String(vehicle.vehicleId))}><span>{vehicle.licensePlate}</span><span>{vehicle.bayCode || LOCATION_COPY.unknown}</span></button>)}</div>
        {selected && <dl className="garage3d-location-details">
          <dt>Xe</dt><dd>{selected.modelLabel} · {selected.licensePlate}</dd><dt>Chi nhánh</dt><dd>{selected.branchName}</dd>
          <dt>Kho / bãi / khoang</dt><dd>{selected.bayCode ? [selected.serviceArea, selected.bayCode, selected.bayName].filter(Boolean).join(' · ') : LOCATION_COPY.unknown}</dd>
          <dt>Người ghi nhận</dt><dd>{selected.reportedBy || '—'}</dd><dt>Cập nhật vị trí</dt><dd>{formatTime(selected.reportedAt)}</dd>
          {selected.note && <><dt>Ghi chú vị trí</dt><dd>{selected.note}</dd></>}
        </dl>}
        <p className="garage3d-muted">{LOCATION_COPY.refresh}</p><button className="garage3d-rotate" onClick={() => setVersion(value => value + 1)}>Tải lại vị trí</button>
        {data.vehicles.filter(vehicle => !vehicle.branchId).map(vehicle => <p key={vehicle.vehicleId} className="garage3d-muted">{vehicle.licensePlate} · {LOCATION_COPY.noBranch}</p>)}
      </aside>
      <section className="garage3d-center">
        <h2>{branch?.name || LOCATION_COPY.title}</h2>
        {parked.length ? <div className="garage3d-stage"><div className="garage3d-canvas" ref={host}/>{ready && !sceneError && selected?.bayId != null && <RepairCallouts key={selected.vehicleId} scene={scene} vehicle={selected}/>}<p className="garage3d-repair-summary" role="status">{selected?.repairs?.length ? `${REPAIR_COPY.active}: ${selected.repairs.length}` : REPAIR_COPY.empty}</p>{!ready && !sceneError && <div className="garage3d-loading" role="status">Đang mở sơ đồ xe…</div>}{sceneError && <div className="garage3d-loading" role="alert">{sceneError}</div>}</div> : <div className="garage3d-stage garage3d-location-empty">{LOCATION_COPY.noParked}</div>}
        <div className="garage3d-views garage3d-location-views">{VIEWS.map(view => <button key={view.id} disabled={!ready || !selected?.bayId} onClick={() => scene.current?.setView(view.id)}>{view.label}</button>)}</div>
        <p className="garage3d-credit">{LOCATION_COPY.diagram}</p>
        <p className="garage3d-credit">Model <a href="https://sketchfab.com/3d-models/vinfast-lux-a20-9089d4d7f1ea4de5a4cf924ed6e01410" target="_blank" rel="noreferrer">VinFast LUX A2.0</a> bởi Dave Love SketchFab · <a href="https://creativecommons.org/licenses/by/4.0/" target="_blank" rel="noreferrer">CC BY 4.0</a>.</p>
      </section>
    </div>
  </main>;
}
