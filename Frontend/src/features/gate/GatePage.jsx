import React, { useCallback, useEffect, useRef, useState } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { gateLabels as L, statuses, directions } from "./gateLabels";
import "./gate.css";
import CameraAiConnection from "./CameraAiConnection";

const base = "/api/v1/gate";
const dateText = value => value ? new Date(value.endsWith("Z") ? value : `${value}Z`).toLocaleString("vi-VN") : "—";
const blankCamera = { name: "", lane: "", direction: "ENTRY", streamUrl: "localvideo://sample", minConfidence: .85, retentionDays: 30, enabled: true };
const api = (url, options = {}) => request({ url: `${base}${url}`, ...options });
const errorMessage = e => e?.response?.data?.message || e?.message || "Không thể xử lý yêu cầu.";

export default function GatePage() {
  const { user } = useAuth();
  const { branches, selectedBranchId } = useBranch();
  const { pathname } = useLocation();
  const pageBase = pathname.startsWith("/admin/") ? "/admin/gate" : "/workshop/gate";
  const tab = pathname.endsWith("/cameras") ? "cameras" : pathname.endsWith("/history") ? "history" : "dashboard";
  const role = String(user?.roleCode || "").toUpperCase();
  const manager = ["ADMIN", "MANAGER"].includes(role);
  const canConfirm = manager || role === "GATE_SECURITY";
  const [branch, setBranch] = useState(role === "ADMIN" ? Number(selectedBranchId) || "" : Number(user?.branchId) || "");
  const [filters, setFilters] = useState({ plate: "", cameraId: "", direction: "", status: "", from: "", to: "" });
  const [page, setPage] = useState(1);
  const [events, setEvents] = useState({ items: [], total: 0 });
  const [cameras, setCameras] = useState([]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [live, setLive] = useState(false);
  const [selected, setSelected] = useState(null);
  const [detail, setDetail] = useState(null);
  const [review, setReview] = useState({ plate: "", reason: "", gatePassId: "", override: false });
  const [cameraForm, setCameraForm] = useState(null);
  const [credential, setCredential] = useState("");
  const generation = useRef(0);
  const params = useCallback(() => ({ ...filters, branchId: branch || undefined, page, pageSize: 25,
    cameraId: filters.cameraId || undefined,
    from: filters.from ? new Date(filters.from).toISOString() : undefined,
    to: filters.to ? new Date(filters.to).toISOString() : undefined }), [branch, filters, page]);
  const load = useCallback(async () => {
    const token = ++generation.current;
    try {
      const [list, devices] = await Promise.all([api("/events", { params: params() }), api("/cameras", { params: { branchId: branch || undefined } })]);
      if (token === generation.current) { setEvents(list); setCameras(devices); }
    } catch (e) { if (token === generation.current) setError(errorMessage(e)); }
  }, [branch, params]);
  const loadRef = useRef(load);
  loadRef.current = load;
  useEffect(() => { setEvents({ items: [], total: 0 }); setCameras([]); setSelected(null); setError(""); load(); return () => { generation.current++; }; }, [load]);
  useEffect(() => {
    let disposed = false;
    const hub = new HubConnectionBuilder().withUrl("/hubs/gate", { withCredentials: true }).withAutomaticReconnect().configureLogging(LogLevel.Warning).build();
    hub.on("GateChanged", () => loadRef.current());
    hub.onreconnecting(() => setLive(false)); hub.onreconnected(() => { setLive(true); loadRef.current(); }); hub.onclose(() => setLive(false));
    hub.start().then(() => { if (!disposed) { setLive(true); loadRef.current(); } }).catch(() => { if (!disposed) setLive(false); });
    const timer = setInterval(() => loadRef.current(), 30000);
    return () => { disposed = true; clearInterval(timer); hub.stop().catch(() => {}); };
  }, []);
  const action = async operation => {
    setBusy(true); setError("");
    try { await operation(); await load(); } catch (e) { setError(errorMessage(e)); }
    finally { setBusy(false); }
  };
  const select = row => action(async () => {
    setSelected(row); setDetail(null); setReview({ plate: row.plate, reason: "", gatePassId: "", override: false });
    setDetail(await api(`/events/${row.id}`));
  });
  const submitReview = kind => action(async () => {
    await api(`/events/${selected.id}/review`, { method: "POST", data: { ...review, action: kind, gatePassId: review.gatePassId ? Number(review.gatePassId) : null } });
    setSelected(null); setDetail(null);
  });
  const updateFilter = (key, value) => { setFilters(current => ({ ...current, [key]: value })); setPage(1); };
  const exportCsv = () => action(async () => {
    const query = new URLSearchParams(Object.entries(params()).filter(([, v]) => v !== undefined && v !== ""));
    const response = await fetch(`${base}/events/export?${query}`, { credentials: "include" });
    if (!response.ok) throw Error((await response.json()).message || "Không thể xuất lịch sử.");
    const url = URL.createObjectURL(await response.blob()); const a = document.createElement("a"); a.href = url; a.download = "gate-events.csv"; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
  });
  const saveCamera = e => { e.preventDefault(); action(async () => {
    const { id, ...data } = cameraForm;
    data.branchId = Number(data.branchId); data.minConfidence = Number(data.minConfidence); data.retentionDays = Number(data.retentionDays);
    data.streamUrl = data.streamUrl || null;
    const result = await api(id ? `/cameras/${id}` : "/cameras", { method: id ? "PUT" : "POST", data });
    if (result.credential) setCredential(`Camera ${result.id}: ${result.credential}`);
    setCameraForm(null);
  }); };
  return <div className="gate-page">
    <div className="gate-heading"><div><h2>{L.title}</h2><p>{L.policy}</p></div><span className={live ? "gate-online" : ""}>{live ? L.realtime : L.reconnect}</span></div>
    <nav className="gate-tabs" aria-label={L.title}>
      <NavLink end to={pageBase}>{L.dashboard}</NavLink><NavLink to={`${pageBase}/history`}>{L.history}</NavLink>
      {manager && <NavLink to={`${pageBase}/cameras`}>{L.cameras}</NavLink>}
    </nav>
    {error && <div className="gate-error" role="alert">{error}</div>}
    {credential && <div className="gate-secret"><p>{L.credential}</p><code>{credential}</code><button onClick={() => setCredential("")}>{L.close}</button></div>}
    <div className="gate-toolbar">
      <label>{L.branch}<select value={branch} disabled={role !== "ADMIN"} onChange={e => { setBranch(e.target.value); setPage(1); }}>
        {role === "ADMIN" && <option value="">{L.all}</option>}{branches.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
      </select></label><button disabled={busy} onClick={() => action(load)}>{L.refresh}</button>
      {tab === "history" && manager && <button onClick={exportCsv} disabled={busy}>{L.export}</button>}
    </div>
    {tab === "cameras" && manager ? <>
      <button onClick={() => setCameraForm({ ...blankCamera, branchId: branch || user?.branchId || "" })}>{L.add}</button>
      {cameraForm && <form className="gate-panel gate-form" onSubmit={saveCamera}>
        <label>{L.branch}<select required disabled={!!cameraForm.id || role !== "ADMIN"} value={cameraForm.branchId} onChange={e => setCameraForm({ ...cameraForm, branchId: e.target.value })}><option value="">{L.branch}</option>{branches.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select></label>
        {[['name', L.name], ['lane', L.lane]].map(([key, text]) => <label key={key}>{text}<input required maxLength={key === "name" ? 120 : 40} value={cameraForm[key]} onChange={e => setCameraForm({ ...cameraForm, [key]: e.target.value })} /></label>)}
        <label>{L.direction}<select value={cameraForm.direction} onChange={e => setCameraForm({ ...cameraForm, direction: e.target.value })}>{Object.entries(directions).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>
        <label>{L.rtsp}<input type="password" autoComplete="new-password" required={!cameraForm.id} placeholder={cameraForm.id ? L.keepRtsp : "rtsp://…"} value={cameraForm.streamUrl} onChange={e => setCameraForm({ ...cameraForm, streamUrl: e.target.value })} /></label>
        <label>{L.threshold}<input type="number" min="0.1" max="1" step="0.01" value={cameraForm.minConfidence} onChange={e => setCameraForm({ ...cameraForm, minConfidence: e.target.value })} /></label>
        <label>{L.retention}<input type="number" min="1" max="365" value={cameraForm.retentionDays} onChange={e => setCameraForm({ ...cameraForm, retentionDays: e.target.value })} /></label>
        <label><input type="checkbox" checked={cameraForm.enabled} onChange={e => setCameraForm({ ...cameraForm, enabled: e.target.checked })} /> {L.enabled}</label>
        <button disabled={busy}>{L.save}</button><button type="button" onClick={() => setCameraForm(null)}>{L.close}</button>
      </form>}
      <div className="gate-devices">{cameras.map(c => <article className="gate-panel" key={c.id}><h3>{c.name}</h3><p>#{c.id} · {directions[c.direction]} · {c.lane}</p><p className={c.health === "ONLINE" ? "gate-online" : "gate-offline"}>{c.health}</p><p>{L.heartbeat}: {dateText(c.lastHeartbeatAt)}</p><p>{L.rtsp}: {c.streamUrl}</p>
        <button onClick={() => setCameraForm({ ...c, streamUrl: "" })}>{L.edit}</button><button disabled={busy} onClick={() => action(async () => { const r = await api(`/cameras/${c.id}/rotate-credential`, { method: "POST" }); setCredential(`Camera ${c.id}: ${r.credential}`); })}>{L.rotate}</button>
      </article>)}</div>
    </> : <>
      {tab === "dashboard" && <div className="gate-devices">{cameras.map(c => <div className="gate-panel" key={c.id}><strong>{c.name} · {directions[c.direction]}</strong><p className={c.health === "ONLINE" ? "gate-online" : "gate-offline"}>{c.health} · {dateText(c.lastHeartbeatAt)}</p></div>)}</div>}
      {tab === "dashboard" && <CameraAiConnection key={branch} cameras={cameras} manager={manager} />}
      <div className="gate-toolbar">
        <label>{L.plate}<input value={filters.plate} onChange={e => updateFilter("plate", e.target.value)} /></label>
        <label>{L.camera}<select value={filters.cameraId} onChange={e => updateFilter("cameraId", e.target.value)}><option value="">{L.all}</option>{cameras.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
        <label>{L.direction}<select value={filters.direction} onChange={e => updateFilter("direction", e.target.value)}><option value="">{L.all}</option>{Object.entries(directions).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>
        <label>{L.status}<select value={filters.status} onChange={e => updateFilter("status", e.target.value)}><option value="">{L.all}</option>{Object.entries(statuses).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>
        {[["from", L.from], ["to", L.to]].map(([k, v]) => <label key={k}>{v}<input type="datetime-local" value={filters[k]} onChange={e => updateFilter(k, e.target.value)} /></label>)}
      </div>
      <div className="gate-table-wrap"><table><thead><tr>{[L.observed, L.plate, L.direction, L.confidence, L.matched, L.status, L.reviewer, ""].map((v, i) => <th key={i}>{v}</th>)}</tr></thead>
        <tbody>{events.items.map(row => <tr key={row.id}><td>{dateText(row.observedAt)}</td><td><strong>{row.plate}</strong><small>{L.original}: {row.rawPlate}</small></td><td>{directions[row.direction]}<small>#{row.cameraId}</small></td><td>{(row.confidence * 100).toFixed(1)}%</td><td>{row.vehicleId ? `#${row.vehicleId}` : "—"}<small>{row.appointmentId ? `Lịch hẹn #${row.appointmentId}` : ""}</small></td><td>{statuses[row.status] || row.status}</td><td>{row.reviewedByName || (row.reviewedBy ? `#${row.reviewedBy}` : "—")}</td><td><button onClick={() => select(row)}>{L.detail}</button></td></tr>)}</tbody></table>{!events.items.length && <p>{L.empty}</p>}</div>
      <div className="gate-pagination"><button disabled={page <= 1} onClick={() => setPage(page - 1)}>{L.previous}</button><span>{page} / {Math.max(1, Math.ceil(events.total / 25))} · {events.total}</span><button disabled={page * 25 >= events.total} onClick={() => setPage(page + 1)}>{L.next}</button></div>
    </>}
    {selected && <section className="gate-panel gate-review" aria-label={L.detail}>
      <div className="gate-heading"><h3>{selected.plate} · {directions[selected.direction]}</h3><button onClick={() => setSelected(null)}>{L.close}</button></div>
      <div className="gate-evidence">{[["snapshot", L.snapshot, selected.hasSnapshot], ["crop", L.crop, selected.hasCrop]].map(([kind, label, exists]) => <figure key={kind}><figcaption>{label}</figcaption>{exists ? <img src={`${base}/events/${selected.id}/evidence/${kind}`} alt={label} /> : <p>{L.noImage}</p>}</figure>)}</div>
      {selected.direction === "EXIT" && <div className={detail && !detail.reason ? "gate-online" : "gate-error"}>{!detail ? L.loading : detail.reason ? `${L.blocked}: ${detail.reason}` : L.allowed}</div>}
      <div className="gate-form">
        <label>{L.plate}<input maxLength={40} value={review.plate} onChange={e => setReview({ ...review, plate: e.target.value })} /></label>
        <label>{L.notes}<textarea maxLength={1000} value={review.reason} onChange={e => setReview({ ...review, reason: e.target.value })} /></label>
        {selected.direction === "EXIT" && <label>{L.gatePass}<select value={review.gatePassId} onChange={e => { const gatePassId = e.target.value; setReview({ ...review, gatePassId }); action(async () => setDetail(await api(`/events/${selected.id}`, { params: { gatePassId: gatePassId || undefined } }))); }}><option value="">{L.choosePass}</option>{detail?.passes?.map(p => <option key={p.id} value={p.id}>#{p.id} · SO #{p.serviceOrderId}</option>)}</select></label>}
        {manager && selected.direction === "EXIT" && <label><input type="checkbox" checked={review.override} onChange={e => setReview({ ...review, override: e.target.checked })} />{L.override}</label>}
        {canConfirm && selected.direction === "EXIT" && <label><input type="checkbox" checked={review.oldPartsChecked || false} onChange={e => setReview({ ...review, oldPartsChecked: e.target.checked })} />{L.partsChecked}</label>}
      </div>
      {!["CONFIRMED", "REJECTED"].includes(selected.status) && <div className="gate-toolbar"><button disabled={busy} onClick={() => submitReview("CORRECT")}>{L.correct}</button><button disabled={busy} onClick={() => submitReview("NOTE")}>{L.note}</button>{canConfirm && <><button disabled={busy} onClick={() => submitReview("CONFIRM")}>{L.confirm}</button><button disabled={busy} onClick={() => submitReview("REJECT")}>{L.reject}</button></>}</div>}
      <h4>{L.audit}</h4>{detail?.audits?.map((a, i) => <p key={i}>{dateText(a.at)} · {a.actorName || `#${a.actorId}`} · {a.action} · {a.before} → {a.after} · {a.reason}</p>)}
    </section>}
  </div>;
}
