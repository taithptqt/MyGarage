import React, { useState, useEffect, useRef, useCallback } from "react";
import { request } from "@app/apiClient";
import "./StockHistory.css";

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ₫"; }
function fmtDate(v) { return v ? new Date(v).toLocaleString("vi-VN") : "--"; }
const PAGE_SIZE = 50;
const PAGE_LABELS = { previous: "Trước", next: "Sau", page: "Trang" };

export default function StockHistoryPage() {
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [searchCode, setSearchCode] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [revision, setRevision] = useState(0);
  const sequence = useRef(0);

  const loadHistory = useCallback(async () => {
    const requestId = ++sequence.current;
    setLoading(true);
    setError("");
    try {
      const res = await request({ 
        url: "/api/v1/inventory/history", 
        method: "GET",
        params: { search, page, pageSize: PAGE_SIZE }
      });
      if (requestId !== sequence.current) return;
      const count = Number(res?.total) || 0;
      const lastPage = Math.max(1, Math.ceil(count / PAGE_SIZE));
      if (page > lastPage) { setPage(lastPage); return; }
      setHistory(Array.isArray(res?.items) ? res.items : []);
      setTotal(count);
    } catch (e) {
      if (requestId !== sequence.current) return;
      setError("Không thể tải lịch sử kho.");
      setHistory([]);
      setTotal(0);
    } finally {
      if (requestId === sequence.current) setLoading(false);
    }
  }, [page, search]);

  useEffect(() => {
    loadHistory();
    return () => { sequence.current += 1; };
  }, [loadHistory, revision]);

  const applySearch = () => {
    setPage(1);
    setSearch(searchCode.trim());
    if (page === 1 && searchCode.trim() === search) setRevision((value) => value + 1);
  };

  return (
    <div className="sh-page">
      <div className="sh-header">
        <div>
          <h1 className="sh-title">📦 Lịch sử xuất/nhập kho</h1>
          <p className="sh-subtitle">Tra cứu chi tiết mọi biến động số lượng của từng mã phụ tùng.</p>
        </div>
        <div className="sh-search">
          <input 
            type="text" 
            placeholder="Tìm theo mã PT hoặc chứng từ..." 
            value={searchCode}
            onChange={(e) => setSearchCode(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && applySearch()}
          />
          <button className="sh-btn-search" onClick={applySearch}>🔍 Tra cứu</button>
        </div>
      </div>

      <div className="sh-board">
        {loading ? <div className="text-center p-8 text-slate-500">Đang tải dữ liệu...</div> :
         error && history.length === 0 ? <div className="text-center p-8 text-red-500">{error}</div> :
         history.length === 0 ? <div className="text-center p-8 text-slate-500">Không tìm thấy lịch sử nào.</div> : (
          <table className="sh-table">
            <thead>
              <tr>
                <th>Thời gian</th>
                <th>Mã PT</th>
                <th>Tên phụ tùng</th>
                <th>Loại GD</th>
                <th className="t-right">S.Lượng</th>
                <th>Mã Chứng Từ</th>
                <th>Thực hiện bởi</th>
              </tr>
            </thead>
            <tbody>
              {history.map(row => (
                <tr key={row.id}>
                  <td>{fmtDate(row.date)}</td>
                  <td className="font-semibold text-slate-700">{row.partCode}</td>
                  <td>{row.partName}</td>
                  <td>
                    <span className={`sh-type t-${String(row.type || "").toLowerCase()}`}>
                      {row.type === "IN" ? "Nhập kho" : row.type === "OUT" ? "Xuất kho" : row.type}
                    </span>
                  </td>
                  <td className={`t-right font-bold ${row.qty > 0 ? 'text-emerald-600' : 'text-red-500'}`}>
                    {row.qty > 0 ? `+${row.qty}` : row.qty}
                  </td>
                  <td><span className="sh-ref">{row.ref}</span></td>
                  <td>{row.by}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {!error && total > PAGE_SIZE && (
          <nav aria-label={PAGE_LABELS.page}>
            <button type="button" className="sh-btn-search" disabled={loading || page <= 1} onClick={() => setPage((value) => value - 1)}>{PAGE_LABELS.previous}</button>
            <span> {PAGE_LABELS.page} {page} / {Math.ceil(total / PAGE_SIZE)} </span>
            <button type="button" className="sh-btn-search" disabled={loading || page * PAGE_SIZE >= total} onClick={() => setPage((value) => value + 1)}>{PAGE_LABELS.next}</button>
          </nav>
        )}
      </div>
    </div>
  );
}


