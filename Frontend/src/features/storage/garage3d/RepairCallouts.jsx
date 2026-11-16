import { useEffect, useRef, useState } from 'react';
import { REPAIR_COPY, REPAIR_PARTS } from './repairConfig';

export default function RepairCallouts({ scene, vehicle }) {
  const [activeId, setActiveId] = useState('');
  const overlay = useRef(null), card = useRef(null), wire = useRef(null), markers = useRef(new Map());
  const repairs = (vehicle.repairs || []).filter(repair => REPAIR_PARTS.some(part => part.id === repair.partId));
  const active = repairs.find(repair => repair.partId === activeId) || repairs[0];
  const part = REPAIR_PARTS.find(item => item.id === active?.partId);
  useEffect(() => {
    let frame;
    const update = () => {
      const root = overlay.current;
      if (!root) return;
      for (const repair of repairs) {
        const position = REPAIR_PARTS.find(item => item.id === repair.partId).position;
        const point = scene.current?.projectVehiclePoint(vehicle.vehicleId, position);
        const marker = markers.current.get(repair.partId);
        if (marker) {
          marker.hidden = !point?.visible;
          if (point?.visible) marker.style.transform = `translate(${point.x}px, ${point.y}px) translate(-50%, -50%)`;
        }
        if (repair.partId !== active?.partId || !card.current || !wire.current) continue;
        card.current.hidden = !point?.visible; wire.current.setAttribute('visibility', point?.visible ? 'visible' : 'hidden');
        if (point?.visible) {
          const width = card.current.offsetWidth, height = card.current.offsetHeight;
          const left = Math.max(12, Math.min(root.clientWidth - width - 12, point.x + (point.x < root.clientWidth / 2 ? 55 : -width - 55)));
          const top = Math.max(12, Math.min(root.clientHeight - height - 12, point.y - height - 45));
          card.current.style.transform = `translate(${left}px, ${top}px)`;
          const endX = Math.max(left + 16, Math.min(left + width - 16, point.x));
          wire.current.setAttribute('d', `M ${point.x} ${point.y} L ${point.x} ${top + height + 20} L ${endX} ${top + height}`);
        }
      }
      frame = requestAnimationFrame(update);
    };
    update();
    return () => cancelAnimationFrame(frame);
  }, [scene, vehicle.vehicleId, vehicle.bayId, JSON.stringify(repairs), active?.partId]);

  if (!active) return null;
  return <div className="garage3d-repair-overlay" ref={overlay}>
    <svg className="garage3d-repair-wires" aria-hidden="true"><path ref={wire}/></svg>
    {repairs.map(repair => <button key={repair.partId} ref={element => { if (element) markers.current.set(repair.partId, element); else markers.current.delete(repair.partId); }} className={`garage3d-repair-marker${repair.partId === active.partId ? ' active' : ''}`} aria-label={`${REPAIR_COPY.active}: ${REPAIR_PARTS.find(item => item.id === repair.partId).label}`} aria-pressed={repair.partId === active.partId} onClick={() => setActiveId(repair.partId)}/>)}
    <article className="garage3d-repair-card" ref={card} aria-label={REPAIR_COPY.active}>
      <span className="garage3d-repair-status">{REPAIR_COPY.status}</span>
      <h3>{part.label}</h3><p className="garage3d-repair-intro">{REPAIR_COPY.message}</p>
      <p className="garage3d-repair-note">{active.note}</p><footer>{active.technician}</footer>
      {repairs.length > 1 && <div className="garage3d-repair-switcher">{repairs.map((repair, index) => <button key={repair.partId} aria-label={REPAIR_PARTS.find(item => item.id === repair.partId).label} aria-pressed={repair.partId === active.partId} onClick={() => setActiveId(repair.partId)}>{index + 1}</button>)}</div>}
    </article>
  </div>;
}
