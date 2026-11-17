import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, ArrowUpRight, Box, ChevronRight, Maximize2, Minus, Plus, RotateCcw, RotateCw } from 'lucide-react';
import { createGarageScene } from './createGarageScene';
import { PAINTS, VIEWS } from './garageConfig';
import CustomizerPanel, { GarageBayControls } from './CustomizerPanel';
import { DEFAULT_CUSTOM, CUSTOM_COPY } from './customConfig';
import './Garage3DPage.css';

export default function Garage3DPage() {
  const host = useRef(null), scene = useRef(null), stage = useRef(null);
  const [bay, setBay] = useState('A02'), [view, setView] = useState('perspective'), [paint, setPaint] = useState(null), [rotate, setRotate] = useState(false);
  const [custom, setCustom] = useState({ ...DEFAULT_CUSTOM }), [comparing, setComparing] = useState(false);
  const changeCustom = value => { setCustom(value); setPaint(value.paint); setComparing(false); };
  const [progress, setProgress] = useState(0), [ready, setReady] = useState(false), [error, setError] = useState(''), [attempt, setAttempt] = useState(0);
  useEffect(() => {
    setReady(false); setError(''); setProgress(0);
    try { scene.current = createGarageScene(host.current, { onProgress: setProgress, onReady: () => setReady(true), onError: setError, onSelectBay: id => { setBay(id); setView('perspective'); } }); }
    catch { setError('Thiết bị hoặc trình duyệt chưa hỗ trợ WebGL. Hãy thử Chrome/Edge và bật tăng tốc phần cứng.'); }
    return () => { scene.current?.dispose(); scene.current = null; };
  }, [attempt]);
  useEffect(() => { scene.current?.setBay(bay); }, [bay, attempt]);
  useEffect(() => { scene.current?.setView(view); }, [view, bay, attempt]);
  useEffect(() => {
    if (ready) scene.current?.setCustomization(comparing ? DEFAULT_CUSTOM : { ...custom, paint });
  }, [custom, paint, comparing, ready]);
  useEffect(() => { scene.current?.setRotate(rotate); }, [rotate, attempt]);
  const reset = () => { setView('perspective'); setRotate(false); scene.current?.setView(); };
  const fullscreen = () => { if (document.fullscreenElement) document.exitFullscreen?.(); else stage.current?.requestFullscreen?.().catch(() => {}); };
  return <main className="garage3d-page">
    <header className="garage3d-topbar"><Link className="garage3d-brand" to="/home"><span><Box size={22}/></span>MY GARAGE<small>KHÔNG GIAN 3D</small></Link><div className="garage3d-breadcrumb"><Link to="/storage">Storage</Link><ChevronRight size={14}/><span>Khám phá garage 3D</span></div><Link className="garage3d-back" to="/storage"><ArrowLeft size={16}/> Quay lại Storage</Link></header>
    <div className="garage3d-workspace">
      <CustomizerPanel custom={{ ...custom, paint }} onChange={changeCustom} ready={ready} comparing={comparing} onCompare={() => setComparing(value => !value)} onFocusSurface={id => { setView(id); setRotate(false); }}/>
      <section className="garage3d-center"><div className="garage3d-summary"><div><span>MODEL TRẢI NGHIỆM</span><strong>VinFast Lux A2.0</strong></div><div><span>VỊ TRÍ XEM TRƯỚC</span><strong>Khu A <em>/ {bay}</em></strong></div><div><span>GÓC NHÌN</span><strong>{VIEWS.find(item => item.id === view).label}</strong></div></div>
        <div className="garage3d-stage" ref={stage}><div className="garage3d-custom-mode" role="status">{comparing ? CUSTOM_COPY.originalMode : CUSTOM_COPY.customMode}</div><div className="garage3d-canvas" ref={host}/><div className="garage3d-stage-label"><span className="garage3d-dot"/>{ready ? 'KHÔNG GIAN TƯƠNG TÁC' : 'ĐANG CHUẨN BỊ KHÔNG GIAN'}</div><button className="garage3d-fullscreen" onClick={fullscreen} aria-label="Toàn màn hình"><Maximize2 size={18}/></button>{!ready && !error && <div className="garage3d-loading" role="status"><Box size={34}/><h2>Đang đưa xe vào garage</h2><p>{progress == null ? 'Đang tải dữ liệu…' : `${progress}% · Đang tải và xử lý model 3D`}</p><div className="garage3d-progress"><span style={{ width: `${progress || 3}%` }}/></div><small>Lần đầu tải khoảng 9,3 MB.</small></div>}{error && <div className="garage3d-loading" role="alert"><h2>Chưa thể mở khung 3D</h2><p>{error}</p><button onClick={() => setAttempt(value => value + 1)}>Tải lại khung xem</button><Link to="/storage">Về Storage</Link></div>}<div className="garage3d-stage-bottom"><span>VINFAST <b>LUX A2.0</b><small>MODEL 3D · KHOANG {bay}</small></span><div><button aria-label="Thu nhỏ" disabled={!ready} onClick={() => scene.current?.zoom(1.2)}><Minus size={18}/></button><button aria-label="Phóng to" disabled={!ready} onClick={() => scene.current?.zoom(0.8)}><Plus size={18}/></button><button aria-label="Đặt lại góc nhìn" disabled={!ready} onClick={reset}><RotateCcw size={18}/></button></div></div></div>
        <p className="garage3d-credit">Model <a href="https://sketchfab.com/3d-models/vinfast-lux-a20-9089d4d7f1ea4de5a4cf924ed6e01410" target="_blank" rel="noreferrer">VinFast LUX A2.0</a> bởi <a href="https://sketchfab.com/Tyler_Dave" target="_blank" rel="noreferrer">Dave Love SketchFab</a> · <a href="https://creativecommons.org/licenses/by/4.0/" target="_blank" rel="noreferrer">CC BY 4.0</a> · Đã nén Draco cho web; màu sơn có thể thay đổi trong bản xem trước.</p>
      </section>
      <aside className="garage3d-inspector"><p className="garage3d-eyebrow">ĐIỀU CHỈNH TRẢI NGHIỆM</p><h2>Góc nhìn của bạn.</h2><p className="garage3d-muted">Khám phá từng đường nét.</p><label className="garage3d-control-label">GÓC CAMERA</label><div className="garage3d-views">{VIEWS.map(item => <button key={item.id} className={view === item.id ? 'active' : ''} aria-pressed={view === item.id} disabled={!ready} onClick={() => setView(item.id)}>{item.label}</button>)}</div><button className={`garage3d-rotate ${rotate ? 'active' : ''}`} aria-pressed={rotate} disabled={!ready} onClick={() => setRotate(value => !value)}><RotateCw size={16}/>{rotate ? 'Dừng xoay tự động' : 'Xoay tự động'}</button><GarageBayControls bay={bay} onChange={id => { setBay(id); setView('perspective'); }}/><div className="garage3d-details"><p className="garage3d-eyebrow">THÔNG TIN TRẢI NGHIỆM</p><dl><div><dt>Thương hiệu</dt><dd>VinFast</dd></div><div><dt>Dòng xe</dt><dd>Lux A2.0</dd></div><div><dt>Khu vực</dt><dd>Khu A · {bay}</dd></div><div><dt>Chế độ</dt><dd>Khám phá 3D</dd></div></dl></div><div className="garage3d-booking"><span>Không gian xứng tầm<br/>chiếc xe của bạn.</span><Link to="/contact">Tư vấn lưu trữ xe <ArrowUpRight size={17}/></Link></div></aside>
    </div>
  </main>;
}
