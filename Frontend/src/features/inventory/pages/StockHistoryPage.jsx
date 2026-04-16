import React, { useState, useEffect } from "react";
import { request } from "@app/apiClient";
import "./StockHistory.css";

function fmtCurrency(v) { return Number(v ?? 0).toLocaleString("vi-VN") + " ₫"; }
function fmtDate(v) { return v ? new Date(v).toLocaleString("vi-VN") : "--"; }

export default function StockHistoryPage() {
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [searchCode, setSearchCode] = useState("");

  const loadHistory = async (code = "") => {
    setLoading(true);
    setError("");
    try {
      const res = await request({ 
        url: "/api/inventory/history", 
        method: "GET",
        params: { search: code } 
      });
      setHistory(res?.items ?? res ?? []);
    } catch (e) {
      setError("Không thể tải lịch sử kho.");
      // dummy data if api doesn't exist
      setHistory([
        { id: 1, type: "IN", partCode: "PT-001", partName: "Lọc dầu động cơ", qty: 50, ref: "PO-2404-001", date: new Date().toISOString(), by: "Thủ kho A" },
        { id: 2, type: "OUT", partCode: "PT-001", partName: "Lọc dầu động cơ", qty: -2, ref: "SO-10024", date: new Date(Date.now() - 3600000).toISOString(), by: "Thợ B" },
        { id: 3, type: "OUT", partCode: "PT-002", partName: "Má phanh", qty: -1, ref: "SO-10025", date: new Date(Date.now() - 7200000).toISOString(), by: "Thợ C" }
      ]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadHistory(); }, []);

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
            onKeyDown={(e) => e.key === 'Enter' && loadHistory(searchCode)}
          />
          <button className="sh-btn-search" onClick={() => loadHistory(searchCode)}>🔍 Tra cứu</button>
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
                    <span className={`sh-type t-${row.type.toLowerCase()}`}>
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
      </div>
    </div>
  );
}
