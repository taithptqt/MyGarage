import React, { useState, useEffect, useCallback, useMemo } from "react";
import { request } from "@app/apiClient";
import { useAuth } from "@shared/core/context/AuthContext";
import "./Timesheet.css";

/* ── helpers ── */
function pad(n) { return n < 10 ? "0" + n : n; }
function formatTime(secs) {
  const h = Math.floor(secs / 3600);
  const m = Math.floor((secs % 3600) / 60);
  const s = secs % 60;
  return `${pad(h)}:${pad(m)}:${pad(s)}`;
}

export default function TimesheetPage() {
  const { user } = useAuth();
  
  const [activeEntry, setActiveEntry] = useState(null);
  const [todayEntries, setTodayEntries] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [now, setNow] = useState(new Date());

  const fetchTimesheet = useCallback(async () => {
    setLoading(true);
    try {
      // get today's entries
      const res = await request({ 
        url: "/api/timesheets/today", 
        method: "GET", 
        params: { staffId: user?.id ?? user?.staffId } 
      });
      const data = res?.items ?? res ?? [];
      setTodayEntries(data);
      // find active (without checkOutTime)
      const active = data.find(e => !e.checkOutTime && !e.CheckOutTime);
      setActiveEntry(active || null);
    } catch (err) {
      setError("Không thể tải dữ liệu chấm công hôm nay.");
    } finally {
      setLoading(false);
    }
  }, [user]);

  useEffect(() => {
    fetchTimesheet();
    const t = setInterval(() => setNow(new Date()), 1000); // tick for real-time timer
    return () => clearInterval(t);
  }, [fetchTimesheet]);

  // Compute stats
  const totalSecondsToday = useMemo(() => {
    let secs = 0;
    todayEntries.forEach(e => {
      const start = new Date(e.checkInTime || e.CheckInTime);
      const end = (e.checkOutTime || e.CheckOutTime) ? new Date(e.checkOutTime || e.CheckOutTime) : now;
      if (end >= start) {
        secs += Math.floor((end - start) / 1000);
      }
    });
    return secs;
  }, [todayEntries, now]);

  const handleCheckIn = async () => {
    setError("");
    try {
      await request({
        url: "/api/timesheets/check-in",
        method: "POST",
        data: { staffId: user?.id ?? user?.staffId }
      });
      await fetchTimesheet();
    } catch (err) {
      setError(err?.response?.data?.message || "Lỗi khi Check-in.");
    }
  };

  const handleCheckOut = async () => {
    if (!activeEntry) return;
    setError("");
    try {
      const id = activeEntry.id || activeEntry.Id;
      await request({
        url: `/api/timesheets/${id}/check-out`,
        method: "PUT",
        data: { staffId: user?.id ?? user?.staffId }
      });
      await fetchTimesheet();
    } catch (err) {
      setError(err?.response?.data?.message || "Lỗi khi Check-out.");
    }
  };

  return (
    <div className="time-page">
      <div className="time-card">
        {error && <div className="time-alert">{error}</div>}

        <div className="time-header">
          <h2 className="time-title">🕒 Chấm công (Timesheet)</h2>
          <p className="time-subtitle">Ngày {now.toLocaleDateString("vi-VN")}</p>
        </div>
        
        {loading ? (
          <div className="time-loading">Đang tải biểu mẫu...</div>
        ) : (
          <div className="time-body">
            
            <div className="time-clock-display">
               <div className="time-now">{now.toLocaleTimeString("vi-VN", { hour12: false })}</div>
               <div className="time-total">Đã làm hôm nay: <strong>{formatTime(totalSecondsToday)}</strong></div>
            </div>

            <div className="time-actions">
              {activeEntry ? (
                <button className="time-btn time-btn-out" onClick={handleCheckOut}>
                  🔴 Kết thúc ca (Check-out)
                </button>
              ) : (
                <button className="time-btn time-btn-in" onClick={handleCheckIn}>
                  🟢 Bắt đầu ca (Check-in)
                </button>
              )}
            </div>

            {/* List entries today */}
            <div className="time-history">
              <h4 className="time-h-title">Lịch sử hôm nay</h4>
              {todayEntries.length === 0 ? (
                <div className="time-empty">Chưa có bản ghi nào.</div>
              ) : (
                <ul className="time-list">
                  {todayEntries.map(e => {
                     const tIn = e.checkInTime || e.CheckInTime;
                     const tOut = e.checkOutTime || e.CheckOutTime;
                     return (
                       <li key={e.id || e.Id}>
                         <span>🔵 In: {new Date(tIn).toLocaleTimeString("vi-VN")}</span>
                         <span>{tOut ? `🔴 Out: ${new Date(tOut).toLocaleTimeString("vi-VN")}` : "⏳ Đang làm..."}</span>
                       </li>
                     )
                  })}
                </ul>
              )}
            </div>
            
          </div>
        )}
      </div>
    </div>
  );
}
