import { useEffect, useRef, useState } from 'react';
import { useAuth } from '@shared/core/context/AuthContext';
import { BAYS, PAINTS } from './garageConfig';
import { CUSTOM_COPY, CUSTOM_GROUPS, CUSTOM_TABS, CUSTOM_ACCESSORIES, CUSTOM_PRESETS, customPreset, STICKER_SURFACES, CUSTOM_STORAGE_KEY, DEFAULT_CUSTOM, customLines, normalizeCustom, hasCustomChanges } from './customConfig';
import { submitCustomRequest } from './customRequestsApi';
import { getCustomerVehicles } from '@features/customerPortal/api/customerPortalApi';
import { getAllBranches } from '@features/appointments/api/appointmentReferenceApi';
import StickerEditor from './StickerEditor';

export function GarageBayControls({ bay, onChange }) {
  return <section>
    <p className="garage3d-eyebrow garage3d-bay-title">CHỌN KHOANG XEM TRƯỚC</p>
    <div className="garage3d-bays">{BAYS.map(item => <button key={item.id} className={bay === item.id ? 'active' : ''} onClick={() => onChange(item.id)} aria-pressed={bay === item.id}><span>Khoang {item.id}</span><span>{bay === item.id ? 'Đang xem' : 'Xem trước'}</span></button>)}</div>
    <p className="garage3d-custom-disclaimer">Bố trí trải nghiệm minh họa. Không phải dữ liệu chỗ đỗ trống hoặc thao tác đặt chỗ thực tế.</p>
  </section>;
}

export default function CustomizerPanel({ custom, onChange, ready, comparing, onCompare, onFocusSurface }) {
  const [notice, setNotice] = useState('');
  const { isAuthenticated, user } = useAuth();
  const [sending, setSending] = useState(false), [sentSnapshot, setSentSnapshot] = useState('');
  const pending = useRef(null), sendingRef = useRef(false);
  const snapshot = JSON.stringify(custom);
  const hasChanges = hasCustomChanges(custom);
  const isCustomer = isAuthenticated && String(user?.roleCode || user?.RoleCode).toUpperCase() === 'CUSTOMER';
  const lines = customLines(custom, PAINTS);
  const [tab, setTab] = useState('paint'), [selectedSticker, setSelectedSticker] = useState('');
  const [vehicles, setVehicles] = useState([]), [branches, setBranches] = useState([]), [lookupError, setLookupError] = useState('');
  useEffect(() => {
    let active = true;
    getAllBranches().then(result => { if (active) setBranches((Array.isArray(result) ? result : result.items || []).filter(item => !item.isDeleted && String(item.status || '').trim().toLowerCase() === 'active')); })
      .catch(() => { if (active) setLookupError('Chưa tải được chi nhánh.'); });
    return () => { active = false; };
  }, []);
  useEffect(() => {
    let active = true;
    if (isCustomer) getCustomerVehicles().then(result => { if (active) setVehicles((Array.isArray(result) ? result : []).filter(item => item.isCurrentOwner)); })
      .catch(() => { if (active) setLookupError('Chưa tải được xe đã lưu. Bạn có thể nhập thông tin xe bên dưới.'); });
    return () => { active = false; };
  }, [isCustomer]);
  const submit = async () => {
    if (sendingRef.current || sentSnapshot === snapshot) return;
    if (!isCustomer) { setNotice(CUSTOM_COPY.login); return; }
    if (!hasChanges) { setNotice(CUSTOM_COPY.empty); return; }
    if (custom.stickers.some(item => ['text', 'number'].includes(item.design) && !item.text.trim())) { setNotice(CUSTOM_COPY.missingStickerText); setTab('decal'); return; }
    if (!custom.vehicleId && !custom.vehicleDescription.trim()) { setNotice(CUSTOM_COPY.missingVehicle); setTab('request'); return; }
    if (!custom.branchId) { setNotice(CUSTOM_COPY.missingBranch); setTab('request'); return; }
    if (pending.current?.snapshot !== snapshot) pending.current = { snapshot, id: crypto.randomUUID() };
    sendingRef.current = true; setSending(true); setNotice('');
    try {
      await submitCustomRequest(custom, pending.current.id);
      setSentSnapshot(snapshot); setNotice(CUSTOM_COPY.sent); pending.current = null;
    } catch (error) {
      const status = error?.status || error?.response?.status;
      if (status === 409) pending.current = null;
      setNotice(status === 401 || status === 403 ? CUSTOM_COPY.login : status === 400 || status === 409 ? error.message || CUSTOM_COPY.sendError : CUSTOM_COPY.sendError);
    } finally { sendingRef.current = false; setSending(false); }
  };
  const update = (key, value) => { onChange({ ...custom, [key]: value }); setNotice(''); };
  const save = () => {
    try { localStorage.setItem(CUSTOM_STORAGE_KEY, JSON.stringify(custom)); setNotice('Đã lưu cấu hình trên trình duyệt này.'); }
    catch { setNotice(CUSTOM_COPY.storageError); }
  };
  const restore = () => {
    try {
      const saved = localStorage.getItem(CUSTOM_STORAGE_KEY);
      if (!saved) { setNotice('Chưa có cấu hình đã lưu.'); return; }
      onChange(normalizeCustom(JSON.parse(saved), PAINTS)); setNotice('Đã mở cấu hình đã lưu.');
    } catch { setNotice(CUSTOM_COPY.storageError); }
  };
  return <aside className="garage3d-sidebar garage3d-customizer">
    <p className="garage3d-eyebrow">CUSTOM STUDIO / 01</p>
    <h1>Dấu ấn<br/>của riêng bạn.</h1>
    <p className="garage3d-muted">{CUSTOM_COPY.intro}</p>
    <div className="garage3d-presets"><span>Thử phong cách mẫu</span>{CUSTOM_PRESETS.map(preset => <button key={preset.id} disabled={!ready || sending} onClick={() => { onChange({ ...customPreset(preset.id), vehicleId: custom.vehicleId, vehicleDescription: custom.vehicleDescription, branchId: custom.branchId, notes: custom.notes }); setNotice(''); onFocusSurface?.('perspective'); }}>{preset.label}</button>)}</div>
    <nav className="garage3d-custom-tabs" aria-label="Hạng mục custom">{CUSTOM_TABS.map(item => <button key={item.id} className={tab === item.id ? 'active' : ''} aria-pressed={tab === item.id} onClick={() => setTab(item.id)}>{item.label}{item.id === 'decal' && custom.stickers.length > 0 ? ` (${custom.stickers.length})` : ''}</button>)}</nav>
    {tab === 'paint' && <fieldset disabled={!ready || sending}>
      <legend>Màu thân xe</legend>
      <div className="garage3d-paints">{PAINTS.map(item => <button type="button" key={item.label} title={item.label} aria-label={`Màu thân xe: ${item.label}`} aria-pressed={custom.paint === item.value} className={custom.paint === item.value ? 'active' : ''} style={{ '--paint': item.value || 'linear-gradient(135deg,#c5a059,#22334c)' }} onClick={() => update('paint', item.value)}/>)}</div>
      <p className="garage3d-selected-paint">{PAINTS.find(item => item.value === custom.paint)?.label}</p>
    </fieldset>}
    {CUSTOM_GROUPS.filter(group => group.tab === tab).map(group => <fieldset disabled={!ready || sending} key={group.key}>
      <legend>{group.label}</legend>
      <div className="garage3d-custom-options">{group.options.map(option => <button type="button" key={option.id} aria-pressed={custom[group.key] === option.id} className={custom[group.key] === option.id ? 'active' : ''} onClick={() => update(group.key, option.id)}>
        <span>{option.color && <i style={{ background: option.color }}/>} {option.label}</span>
      </button>)}</div>
    </fieldset>)}
    {tab === 'decal' && <StickerEditor stickers={custom.stickers} selectedId={selectedSticker} onSelect={setSelectedSticker} onChange={value => update('stickers', value)} onFocus={surface => onFocusSurface?.(STICKER_SURFACES.find(item => item.id === surface)?.view)} disabled={!ready || sending}/>}
    {tab === 'comfort' && <fieldset disabled={sending}><legend>Hạng mục cần tư vấn</legend><p className="garage3d-muted">{CUSTOM_COPY.accessoriesHelp}</p><div className="garage3d-accessories">{CUSTOM_ACCESSORIES.map(option => <label key={option.id}><input type="checkbox" checked={custom.accessories.includes(option.id)} onChange={event => update('accessories', event.target.checked ? [...custom.accessories, option.id] : custom.accessories.filter(id => id !== option.id))}/>{option.label}</label>)}</div></fieldset>}
    {tab === 'request' && <fieldset disabled={sending} className="garage3d-request-fields">
      <legend>Chiếc xe & yêu cầu thực tế</legend>
      <p className="garage3d-muted">{CUSTOM_COPY.requestHelp}</p>
      {vehicles.length > 0 && <label className="garage3d-input-label">Xe trong garage của bạn<select value={custom.vehicleId} onChange={event => update('vehicleId', event.target.value)}><option value="">Nhập xe khác bên dưới</option>{vehicles.map(vehicle => <option key={vehicle.vehicleId} value={vehicle.vehicleId}>{vehicle.licensePlate} · {vehicle.modelName || vehicle.brandName}</option>)}</select></label>}
      {!custom.vehicleId && <label className="garage3d-input-label">Thông tin xe cần custom *<input maxLength={160} value={custom.vehicleDescription} placeholder="Hãng, dòng xe, đời xe, biển số (nếu có)" onChange={event => update('vehicleDescription', event.target.value)}/></label>}
      <label className="garage3d-input-label">Chi nhánh tiếp nhận *<select value={custom.branchId} onChange={event => update('branchId', event.target.value)}><option value="">Chọn chi nhánh</option>{branches.map(branch => <option key={branch.id} value={branch.id}>{branch.name}</option>)}</select></label>
      {lookupError && <p role="alert" className="garage3d-custom-disclaimer">{lookupError}</p>}
      <label className="garage3d-input-label">Ghi chú cho quản lý<textarea rows={4} maxLength={1000} value={custom.notes} placeholder="Phong cách mong muốn, vị trí cần lưu ý, vật liệu hoặc hạng mục cần tư vấn thêm…" onChange={event => update('notes', event.target.value)}/><small>{custom.notes.length}/1000</small></label>
      <p className="garage3d-custom-disclaimer">Mô hình Lux A2.0 chỉ minh họa ý tưởng. Gửi yêu cầu chưa phải xác nhận thi công hoặc đặt lịch.</p>
    </fieldset>}
    <button className={`garage3d-rotate ${comparing ? 'active' : ''}`} disabled={!ready || sending} aria-pressed={comparing} onClick={onCompare}>{comparing ? 'Trở lại bản custom' : 'So sánh với xe nguyên bản'}</button>
    <section className="garage3d-custom-selection" aria-label="Danh sách lựa chọn custom xe">
      <p className="garage3d-eyebrow">CẤU HÌNH CỦA BẠN</p>
      {!lines.length && <p className="garage3d-muted">Chưa chọn thay đổi nào.</p>}
      <ul>{lines.map(line => <li key={line.key}><span>{line.label}</span><b>{line.value}</b></li>)}</ul>
      <button className="garage3d-custom-send" disabled={!ready || sending || !hasChanges || sentSnapshot === snapshot} onClick={submit}>{sending ? CUSTOM_COPY.sending : sentSnapshot === snapshot ? CUSTOM_COPY.sent : CUSTOM_COPY.send}</button>
      <p className="garage3d-custom-disclaimer">{CUSTOM_COPY.sendHelp}</p>
      <div className="garage3d-custom-actions"><button disabled={!ready || sending} onClick={save}>Lưu cấu hình</button><button disabled={!ready || sending} onClick={restore}>Mở bản đã lưu</button></div>
      <button className="garage3d-custom-reset" disabled={!ready || sending} onClick={() => { onChange({ ...DEFAULT_CUSTOM, stickers: [] }); setNotice('Đã đưa xe về nguyên bản.'); }}>Đặt lại toàn bộ</button>
      <p role="status" className="garage3d-custom-status">{notice}</p>
    </section>
    <p className="garage3d-custom-disclaimer">{CUSTOM_COPY.disclaimer}</p>
  </aside>;
}
