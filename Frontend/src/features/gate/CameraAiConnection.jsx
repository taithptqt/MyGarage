import React, { useCallback, useEffect, useState } from "react";
import { request } from "@app/apiClient";
import { gateLabels as L, directions } from "./gateLabels";

const aiUrl = (import.meta.env.VITE_CAMERA_AI_URL || "http://127.0.0.1:7861").replace(/\/$/, "");
// The local proxy reaches the HTTPS backend with its development certificate.
const garageUrl = import.meta.env.VITE_CAMERA_GARAGE_URL || "http://127.0.0.1:5173";

export default function CameraAiConnection({ cameras, manager }) {
  const [camera, setCamera] = useState(""), [status, setStatus] = useState(null);
  const [busy, setBusy] = useState(false), [error, setError] = useState("");
  const refresh = useCallback(async () => {
    const response = await fetch(`${aiUrl}/garage/status`);
    if (!response.ok) throw new Error(L.aiUnavailable);
    const result = await response.json(); setStatus(result); return result;
  }, []);
  useEffect(() => {
    let disposed = false;
    const check = async () => { try { const response = await fetch(`${aiUrl}/garage/status`); if (!response.ok) throw new Error(); const value = await response.json(); if (!disposed) setStatus(value); } catch { if (!disposed) setStatus(null); } };
    check(); const timer = setInterval(check, 5000);
    return () => { disposed = true; clearInterval(timer); };
  }, []);
  const connect = async () => {
    setBusy(true); setError("");
    try {
      await refresh();
      const { credential } = await request({ url: `/api/v1/gate/cameras/${camera}/rotate-credential`, method: "POST" });
      const response = await fetch(`${aiUrl}/garage/connect`, { method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ garageUrl, cameraId: Number(camera), credential }) });
      if (!response.ok) throw new Error(L.aiPairingError);
      setStatus(await response.json());
    } catch (e) { setError(e?.response?.data?.message || e.message || L.aiPairingError); }
    finally { setBusy(false); }
  };
  // Only display pairing information for cameras visible in this user's branch.
  const active = cameras.find(item => item.id === status?.activeCameraId);
  const delivery = status?.cameras?.find(item => item.cameraId === active?.id)?.delivery;
  return <section className="gate-panel" aria-label={L.aiStandalone}>
    <h3>{L.aiStandalone}</h3><p>{L.aiStandaloneHelp}</p>
    <p className={status ? "gate-online" : "gate-offline"}>{status ? L.aiOnline : L.aiUnavailable}</p>
    {status && !active && <p>{L.aiNoCamera}</p>}
    {active && <p>{L.camera}: {active.name} · {directions[active.direction]} · {delivery === "RETRYING" ? L.aiRetrying : L.aiConnected}</p>}
    {manager && <div className="gate-toolbar">
      <label>{L.camera}<select value={camera} onChange={e => setCamera(e.target.value)}><option value="">{L.camera}</option>
        {cameras.filter(item => item.enabled).map(item => <option key={item.id} value={item.id}>{item.name} · {directions[item.direction]}</option>)}
      </select></label>
      <button disabled={busy || !camera || !status} onClick={connect}>{L.aiConnect}</button>
    </div>}
    {error && <p className="gate-error" role="alert">{error}</p>}
    <a href={aiUrl} target="_blank" rel="noreferrer">{L.aiOpen}</a>
    <p>{L.aiPairingHelp}</p>
  </section>;
}
