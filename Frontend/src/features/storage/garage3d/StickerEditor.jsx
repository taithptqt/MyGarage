import { MAX_STICKERS, STICKER_DESIGNS, STICKER_SURFACES, newSticker, CUSTOM_COPY } from './customConfig';

const ranges = [
  { key: 'size', label: 'Kích thước', min: 0.25, max: 1.2, step: 0.05, unit: ' m' },
  { key: 'rotation', label: 'Góc xoay', min: -180, max: 180, step: 5, unit: '°' },
  { key: 'x', label: 'Dịch ngang / dọc thân', min: -1, max: 1, step: 0.05 },
  { key: 'y', label: 'Dịch cao / thấp', min: -1, max: 1, step: 0.05 },
];

export default function StickerEditor({ stickers, selectedId, onSelect, onChange, onFocus, disabled }) {
  const selected = stickers.find(item => item.id === selectedId) || stickers[0];
  const update = patch => onChange(stickers.map(item => item.id === selected.id ? { ...item, ...patch } : item));
  const add = () => { const item = newSticker(); onChange([...stickers, item]); onSelect(item.id); onFocus('right'); };
  return <div className="garage3d-sticker-editor">
    <p className="garage3d-muted">{CUSTOM_COPY.decalHelp}</p>
    <div className="garage3d-sticker-layers">{stickers.map((item, index) => <button disabled={disabled} key={item.id} className={selected?.id === item.id ? 'active' : ''} aria-pressed={selected?.id === item.id} onClick={() => { onSelect(item.id); onFocus(item.surface); }}>Lớp {index + 1}</button>)}</div>
    <button className="garage3d-rotate" disabled={disabled || stickers.length >= MAX_STICKERS} onClick={add}>+ Thêm sticker ({stickers.length}/{MAX_STICKERS})</button>
    {selected && <fieldset disabled={disabled}>
      <legend>Chỉnh lớp {stickers.indexOf(selected) + 1}</legend>
      <div className="garage3d-custom-options">{STICKER_DESIGNS.map(design => <button key={design.id} className={selected.design === design.id ? 'active' : ''} aria-pressed={selected.design === design.id} onClick={() => update({ design: design.id, text: design.id === 'number' ? '07' : design.id === 'text' && selected.design === 'number' ? 'MY GARAGE' : selected.text })}><strong className="garage3d-decal-symbol">{design.symbol}</strong>{design.label}</button>)}</div>
      <label className="garage3d-input-label">Vị trí dán<select value={selected.surface} onChange={event => { update({ surface: event.target.value, x: 0, y: 0 }); onFocus(event.target.value); }}>{STICKER_SURFACES.map(surface => <option key={surface.id} value={surface.id}>{surface.label}</option>)}</select></label>
      <label className="garage3d-input-label">Màu sticker<input type="color" value={selected.color} onChange={event => update({ color: event.target.value })}/></label>
      {['text', 'number'].includes(selected.design) && <label className="garage3d-input-label">{selected.design === 'number' ? 'Số / ký hiệu thi đấu' : 'Nội dung chữ'}<input maxLength={20} value={selected.text} onChange={event => update({ text: event.target.value })}/></label>}
      {ranges.map(range => <label key={range.key} className="garage3d-input-label">{range.label}<output>{selected[range.key]}{range.unit}</output><input type="range" min={range.min} max={range.max} step={range.step} value={selected[range.key]} onChange={event => update({ [range.key]: Number(event.target.value) })}/></label>)}
      <div className="garage3d-custom-actions"><button disabled={stickers.length >= MAX_STICKERS} onClick={() => { const copy = { ...selected, id: crypto.randomUUID(), surface: selected.surface === 'right' ? 'left' : selected.surface === 'left' ? 'right' : selected.surface }; onChange([...stickers, copy]); onSelect(copy.id); onFocus(copy.surface); }}>Nhân bản lớp</button><button onClick={() => { onChange(stickers.filter(item => item.id !== selected.id)); onSelect(''); }}>Xóa lớp này</button></div>
    </fieldset>}
  </div>;
}
