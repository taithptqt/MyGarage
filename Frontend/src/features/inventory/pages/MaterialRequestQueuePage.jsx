import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./MaterialRequestQueue.css";

/* -- helpers -- */
function fmt(v) {
  if (!v) return "--";
  return new Date(v).toLocaleString("vi-VN", {
    day: "2-digit", month: "2-digit",
    hour: "2-digit", minute: "2-digit",
    timeZone: "Asia/Ho_Chi_Minh",
  });
}
function fmtNum(v) {
  return Number(v ?? 0).toLocaleString("vi-VN");
}
function fmtCurrency(v) {
  return Number(v ?? 0).toLocaleString("vi-VN") + " ₫";
}

const STATUS_MAP = {
  PENDING:   { label: "Chờ duyệt",   bg: "#fef9c3", color: "#713f12", dot: "#f59e0b" },
  APPROVED:  { label: "Đã duyệt",    bg: "#d1fae5", color: "#065f46", dot: "#10b981" },
  ISSUED:    { label: "Đã xuất",     bg: "#dbeafe", color: "#1e40af", dot: "#3b82f6" },
  REJECTED:  { label: "Từ chối",     bg: "#fee2e2", color: "#991b1b", dot: "#ef4444" },
  PARTIAL:   { label: "Xuất 1 phần", bg: "#ede9fe", color: "#4c1d95", dot: "#7c3aed" },
  CANCELLED: { label: "Đã hủy",      bg: "#f1f5f9", color: "#64748b", dot: "#94a3b8" },
};

const TABS = [
  { key: "PENDING",  label: "⏳ Chờ duyệt" },
  { key: "ISSUED",   label: "📦 Đã xuất" },
  { key: "REJECTED", label: "🚫 Từ chối" },
  { key: "ALL",      label: "📋 Tất cả" },
];

/* -- sub-components -- */
function StatusBadge({ status }) {
  const m = STATUS_MAP[String(status).toUpperCase()] || { label: status, bg: "#f1f5f9", color: "#64748b", dot: "#94a3b8" };
  return (
    <span className="mrq-badge" style={{ background: m.bg, color: m.color }}>
      <span className="mrq-badge-dot" style={{ background: m.dot }} />
      {m.label}
    </span>
  );
}

function ItemRow({ item }) {
  const stockLevel = Number(item.currentStock ?? item.CurrentStock ?? 0);
  const requested  = Number(item.quantity ?? item.Quantity ?? 0);
  const canFulfill = stockLevel >= requested;

  return (
    <div className="mrq-item-row">
      <div className="mrq-item-info">
        <span className="mrq-item-name">{item.productName ?? item.ProductName ?? item.name ?? `SP-${item.productId}`}</span>
        <span className="mrq-item-code">{item.productCode ?? item.ProductCode ?? ""}</span>
      </div>
      <div className="mrq-item-numbers">
        <span className="mrq-qty-req"><strong>{fmtNum(requested)}</strong> {item.unit ?? item.Unit ?? "cái"}</span>
        <span className={`mrq-qty-stock ${canFulfill ? "mrq-stock-ok" : "mrq-stock-low"}`}>
          Tồn: {fmtNum(stockLevel)}
        </span>
      </div>
      {item.unitCost && (
        <span className="mrq-item-cost">{fmtCurrency(requested * Number(item.unitCost))}</span>
      )}
    </div>
  );
}

function RequestCard({ req, onIssue, onReject, issuing, rejecting }) {
  const [expanded, setExpanded] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [showRejectInput, setShowRejectInput] = useState(false);
  const status = String(req.status ?? req.Status ?? "PENDING").toUpperCase();
  const isPending = status === "PENDING";
  const items = req.items ?? req.Items ?? req.materialRequestItems ?? [];
  const totalItems = items.length;

  const handleReject = () => {
    if (!rejectReason.trim()) { alert("Vui lòng nhập lý do từ chối."); return; }
    onReject(req.id, rejectReason);
    setShowRejectInput(false);
  };

  return (
    <div className={`mrq-card ${!isPending ? "mrq-card-closed" : ""}`}>
      {/* card header */}
      <div className="mrq-card-header" onClick={() => setExpanded(e => !e)} role="button" tabIndex={0} onKeyDown={e => e.key === "Enter" && setExpanded(x => !x)}>
        <div className="mrq-card-left">
          <div className="mrq-card-id">
            <span className="mrq-code">#{String(req.id).padStart(5, "0")}</span>
            <StatusBadge status={status} />
          </div>
          <div className="mrq-card-meta">
            <span>📋 {req.serviceOrderCode ?? req.serviceOrderId ?? "--"}</span>
            <span>🔧 {req.requesterName ?? req.RequestedByName ?? "--"}</span>
            <span>🕐 {fmt(req.createdAt ?? req.CreatedAt)}</span>
          </div>
        </div>
        <div className="mrq-card-right">
          <span className="mrq-item-count">{totalItems} vật tư</span>
          <span className="mrq-chevron">{expanded ? "🔼" : "🔽"}</span>
        </div>
      </div>

      {/* expanded items */}
      {expanded && (
        <div className="mrq-card-body">
          <div className="mrq-items-list">
            {items.length === 0
              ? <div className="mrq-empty-items">Không có vật tư nào</div>
              : items.map((it, i) => <ItemRow key={it.id ?? i} item={it} />)
            }
          </div>

          {req.notes && (
            <div className="mrq-card-notes">
              <span>💬</span> <span>{req.notes}</span>
            </div>
          )}

          {/* actions */}
          {isPending && (
            <div className="mrq-card-actions">
              {showRejectInput ? (
                <div className="mrq-reject-input">
                  <input
                    type="text"
                    className="mrq-reason-input"
                    placeholder="Lý do từ chối..."
                    value={rejectReason}
                    onChange={e => setRejectReason(e.target.value)}
                    onKeyDown={e => e.key === "Enter" && handleReject()}
                    autoFocus
                  />
                  <button className="mrq-btn mrq-btn-red" onClick={handleReject} disabled={rejecting}>
                    {rejecting ? "⌛" : "Xác nhận từ chối"}
                  </button>
                  <button className="mrq-btn mrq-btn-ghost" onClick={() => setShowRejectInput(false)}>Hủy</button>
                </div>
              ) : (
                <>
                  <button
                    className="mrq-btn mrq-btn-ghost"
                    onClick={() => setShowRejectInput(true)}
                    disabled={issuing}
                  >
                    🚫 Từ chối
                  </button>
                  <button
                    className="mrq-btn mrq-btn-green"
                    onClick={() => onIssue(req.id)}
                    disabled={issuing}
                  >
                    {issuing ? "⌛ Đang xuất..." : "✅ Duyệt & Xuất kho"}
                  </button>
                </>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/* -- main page -- */
export default function MaterialRequestQueuePage() {
  const { user }  = useAuth();
  const navigate  = useNavigate();

  const [requests, setRequests]   = useState([]);
  const [loading, setLoading]     = useState(true);
  const [error, setError]         = useState("");
  const [activeTab, setActiveTab] = useState("PENDING");
  const [search, setSearch]       = useState("");
  const [issuingId, setIssuingId] = useState(null);
  const [rejectingId, setRejectingId] = useState(null);
  const [lastRefresh, setLastRefresh] = useState(new Date());

  /* -- fetch -- */
  const fetchData = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    setError("");
    try {
      const res = await request({
        url: "/api/material-requests",
        method: "GET",
        params: { branchId: user?.branchId ?? user?.BranchId ?? "" },
      });
      const raw = Array.isArray(res) ? res : (res?.items ?? res?.data ?? []);
      setRequests(raw);
      setLastRefresh(new Date());
    } catch (err) {
      setError("Không thể tải danh sách yêu cầu vật tư.");
      console.error(err);
    } finally {
      if (!silent) setLoading(false);
    }
  }, [user]);

  useEffect(() => {
    fetchData();
    const id = setInterval(() => fetchData(true), 30000);
    return () => clearInterval(id);
  }, [fetchData]);

  /* -- issue -- */
  const handleIssue = async (reqId) => {
    setIssuingId(reqId);
    try {
      await request({ url: `/api/material-requests/${reqId}/issue`, method: "POST",
        data: { issuedBy: user?.id ?? user?.staffId } });
      setRequests(prev => prev.map(r =>
        (r.id === reqId || r.Id === reqId) ? { ...r, status: "ISSUED" } : r
      ));
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể xuất vật tư. Kiểm tra tồn kho.");
    } finally {
      setIssuingId(null);
    }
  };

  /* -- reject -- */
  const handleReject = async (reqId, reason) => {
    setRejectingId(reqId);
    try {
      await request({ url: `/api/material-requests/${reqId}/reject`, method: "POST",
        data: { reason, rejectedBy: user?.id ?? user?.staffId } });
      setRequests(prev => prev.map(r =>
        (r.id === reqId || r.Id === reqId) ? { ...r, status: "REJECTED", rejectionReason: reason } : r
      ));
    } catch (err) {
      setError(err?.response?.data?.message || "Không thể từ chối yêu cầu.");
    } finally {
      setRejectingId(null);
    }
  };

  /* -- filter -- */
  const filtered = useMemo(() => {
    let list = [...requests];
    if (activeTab !== "ALL") {
      list = list.filter(r => String(r.status ?? r.Status ?? "").toUpperCase() === activeTab);
    }
    if (search.trim()) {
      const q = search.toLowerCase();
      list = list.filter(r =>
        String(r.id).includes(q) ||
        String(r.serviceOrderCode ?? r.serviceOrderId ?? "").toLowerCase().includes(q) ||
        String(r.requesterName ?? "").toLowerCase().includes(q)
      );
    }
    // newest first
    return list.sort((a, b) => new Date(b.createdAt ?? b.CreatedAt ?? 0) - new Date(a.createdAt ?? a.CreatedAt ?? 0));
  }, [requests, activeTab, search]);

  /* -- tab counts -- */
  const counts = useMemo(() => {
    const c = { PENDING: 0, ISSUED: 0, REJECTED: 0, ALL: requests.length };
    requests.forEach(r => {
      const s = String(r.status ?? r.Status ?? "").toUpperCase();
      if (s in c) c[s]++;
    });
    return c;
  }, [requests]);

  return (
    <div className="mrq-page">
      {/* header */}
      <div className="mrq-header">
        <div>
          <h1 className="mrq-title">📦 Hàng đợi xuất vật tư</h1>
          <p className="mrq-subtitle">
            Cập nhật lúc {fmt(lastRefresh)} • Tự động làm mới sau 30s
          </p>
        </div>
        <div className="mrq-header-actions">
          <button className="mrq-btn mrq-btn-outline" onClick={() => fetchData()}>
            🔄 Làm mới
          </button>
          <button className="mrq-btn mrq-btn-outline" onClick={() => navigate("/workshop/inventory/history")}>
            📜 Lịch sử kho
          </button>
        </div>
      </div>

      {error && <div className="mrq-alert mrq-alert-error">⚠️ {error}</div>}

      {/* stat bar */}
      <div className="mrq-stat-bar">
        <div className="mrq-stat" style={{ borderTopColor: "#f59e0b" }}>
          <span className="mrq-stat-num" style={{ color: "#b45309" }}>{counts.PENDING}</span>
          <span className="mrq-stat-lbl">⌛ Chờ duyệt</span>
        </div>
        <div className="mrq-stat" style={{ borderTopColor: "#3b82f6" }}>
          <span className="mrq-stat-num" style={{ color: "#1d4ed8" }}>{counts.ISSUED}</span>
          <span className="mrq-stat-lbl">📦 Đã xuất</span>
        </div>
        <div className="mrq-stat" style={{ borderTopColor: "#ef4444" }}>
          <span className="mrq-stat-num" style={{ color: "#b91c1c" }}>{counts.REJECTED}</span>
          <span className="mrq-stat-lbl">🚫 Từ chối</span>
        </div>
        <div className="mrq-stat" style={{ borderTopColor: "#6366f1" }}>
          <span className="mrq-stat-num" style={{ color: "#4338ca" }}>{counts.ALL}</span>
          <span className="mrq-stat-lbl">📋 Tất cả</span>
        </div>
      </div>

      {/* filter bar */}
      <div className="mrq-filter-bar">
        <div className="mrq-tabs">
          {TABS.map(t => (
            <button
              key={t.key}
              className={`mrq-tab ${activeTab === t.key ? "mrq-tab-active" : ""}`}
              onClick={() => setActiveTab(t.key)}
            >
              {t.label}
              {counts[t.key] > 0 && (
                <span className="mrq-tab-badge">{counts[t.key]}</span>
              )}
            </button>
          ))}
        </div>
        <div className="mrq-search-wrap">
          <span className="mrq-search-icon">🔍</span>
          <input
            type="text"
            className="mrq-search"
            placeholder="Tìm theo mã phiếu, lệnh sửa, thợ..."
            value={search}
            onChange={e => setSearch(e.target.value)}
          />
        </div>
      </div>

      {/* list */}
      {loading ? (
        <div className="mrq-loading"><div className="mrq-spinner" /><span>Đang tải...</span></div>
      ) : filtered.length === 0 ? (
        <div className="mrq-empty">
          <span>📦</span>
          <p>{search ? "Không tìm thấy phiếu phù hợp." : "Không có phiếu yêu cầu nào."}</p>
        </div>
      ) : (
        <div className="mrq-list">
          {filtered.map(req => (
            <RequestCard
              key={req.id ?? req.Id}
              req={req}
              onIssue={handleIssue}
              onReject={handleReject}
              issuing={issuingId === (req.id ?? req.Id)}
              rejecting={rejectingId === (req.id ?? req.Id)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
