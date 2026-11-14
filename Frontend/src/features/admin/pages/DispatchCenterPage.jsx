import { useCallback, useEffect, useMemo, useState } from "react";
import {
  autoMarkNoShows,
  getAllBranches,
  getBranchBookingHealth,
  getDispatchDashboard,
  lockOvernightSlots,
  runDispatchCycle,
} from "@features/appointments/api/appointmentApi";
import "./DispatchCenterPage.css";

function fmtDateTime(value) {
  if (!value) return "-";
  const dt = new Date(value);
  if (Number.isNaN(dt.getTime())) return "-";
  return dt.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "Asia/Ho_Chi_Minh",
  });
}

function statusTone(value) {
  const key = String(value || "").toUpperCase();
  if (key.includes("BUSY") || key.includes("WORK")) return "is-busy";
  if (key.includes("OVERFLOW")) return "is-overflow";
  if (key.includes("IDLE") || key.includes("READY")) return "is-idle";
  return "is-default";
}

export default function DispatchCenterPage() {
  const [branches, setBranches] = useState([]);
  const [selectedBranchId, setSelectedBranchId] = useState("");
  const [health, setHealth] = useState(null);
  const [dashboard, setDashboard] = useState(null);
  const [cycleResult, setCycleResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  const loadSnapshot = useCallback(async (branchId) => {
    if (!branchId) return;
    const [healthData, dashboardData] = await Promise.all([
      getBranchBookingHealth(Number(branchId)),
      getDispatchDashboard(Number(branchId)),
    ]);
    setHealth(healthData || null);
    setDashboard(dashboardData || null);
  }, []);

  useEffect(() => {
    let ignore = false;
    async function bootstrap() {
      setLoading(true);
      try {
        const branchData = await getAllBranches();
        if (ignore) return;
        const active = Array.isArray(branchData)
          ? branchData.filter((item) => String(item?.status || "").toLowerCase() === "active")
          : [];
        setBranches(active);
        const first = active[0]?.id || active[0]?.Id;
        if (first) {
          setSelectedBranchId(String(first));
          await loadSnapshot(first);
        }
      } catch (bootstrapError) {
        if (!ignore) setError(bootstrapError?.message || "Không thể tải dữ liệu điều phối.");
      } finally {
        if (!ignore) setLoading(false);
      }
    }

    bootstrap();
    return () => {
      ignore = true;
    };
  }, [loadSnapshot]);

  useEffect(() => {
    if (!selectedBranchId) return;
    const timer = setInterval(() => {
      loadSnapshot(selectedBranchId).catch(() => {});
    }, 30000);
    return () => clearInterval(timer);
  }, [loadSnapshot, selectedBranchId]);

  const sortedBays = useMemo(() => {
    const bays = dashboard?.bays || dashboard?.Bays || [];
    return [...bays].sort((a, b) => {
      const aOrder = Number(a?.sequenceOrder ?? a?.SequenceOrder ?? 0);
      const bOrder = Number(b?.sequenceOrder ?? b?.SequenceOrder ?? 0);
      if (aOrder !== bOrder) {
        return aOrder - bOrder;
      }

      return String(a.bayCode || a.BayCode || "").localeCompare(String(b.bayCode || b.BayCode || ""));
    });
  }, [dashboard]);

  async function handleRunCycle() {
    if (!selectedBranchId) return;
    setRunning(true);
    setError("");
    setMessage("");
    try {
      const [cycle, noShow] = await Promise.all([
        runDispatchCycle(Number(selectedBranchId)),
        autoMarkNoShows(Number(selectedBranchId)),
      ]);
      setCycleResult(cycle || null);
      setMessage(`Đã chạy điều phối. Auto no-show: ${noShow?.updated ?? 0} lịch.`);
      await loadSnapshot(selectedBranchId);
    } catch (cycleError) {
      setError(cycleError?.message || "Không thể chạy chu kỳ điều phối.");
    } finally {
      setRunning(false);
    }
  }

  async function handleOvernightLock() {
    if (!selectedBranchId) return;
    setRunning(true);
    setError("");
    setMessage("");
    try {
      const result = await lockOvernightSlots(Number(selectedBranchId));
      setMessage(`Đã khóa qua đêm: ${result?.lockedSlotCount ?? 0} slot.`);
      await loadSnapshot(selectedBranchId);
    } catch (lockError) {
      setError(lockError?.message || "Không thể khóa slot qua đêm.");
    } finally {
      setRunning(false);
    }
  }

  if (loading) {
    return <div className="dispatch-page loading">Đang tải dashboard điều phối...</div>;
  }

  return (
    <div className="dispatch-page">
      <header className="dispatch-header">
        <div>
          <h1>Dispatch Center</h1>
          <p>Giám sát khoang theo thời gian thực và điều phối xe check-in linh hoạt.</p>
        </div>
        <div className="dispatch-toolbar">
          <select
            value={selectedBranchId}
            onChange={(event) => {
              const value = event.target.value;
              setSelectedBranchId(value);
              setCycleResult(null);
              if (value) {
                loadSnapshot(value).catch(() => {});
              }
            }}
          >
            {branches.map((branch) => {
              const id = branch?.id || branch?.Id;
              return (
                <option key={id} value={id}>
                  {branch?.name || branch?.Name || `Branch #${id}`}
                </option>
              );
            })}
          </select>
          <button type="button" onClick={handleRunCycle} disabled={running || !selectedBranchId}>
            {running ? "Đang chạy..." : "Chạy Dispatch Cycle"}
          </button>
          <button type="button" className="secondary" onClick={handleOvernightLock} disabled={running || !selectedBranchId}>
            Lock Qua Đêm
          </button>
        </div>
      </header>

      {error ? <div className="dispatch-alert error">{error}</div> : null}
      {message ? <div className="dispatch-alert success">{message}</div> : null}

      <section className="dispatch-stats">
        <article>
          <span>Khoang hoạt động</span>
          <strong>{dashboard?.busyBays ?? dashboard?.BusyBays ?? 0}/{dashboard?.totalBays ?? dashboard?.TotalBays ?? 20}</strong>
        </article>
        <article>
          <span>Khoang trống</span>
          <strong>{dashboard?.idleBays ?? dashboard?.IdleBays ?? 0}</strong>
        </article>
        <article>
          <span>Khoang tràn</span>
          <strong>{dashboard?.overflowBays ?? dashboard?.OverflowBays ?? 0}</strong>
        </article>
        <article>
          <span>Hàng đợi bãi xe</span>
          <strong>{dashboard?.waitingQueueCount ?? dashboard?.WaitingQueueCount ?? 0}</strong>
        </article>
      </section>

      <section className="dispatch-health">
        <h2>Sức khỏe booking</h2>
        <div className="health-grid">
          <div><span>Booking lock</span><strong>{(health?.isBookingLocked ?? health?.IsBookingLocked) ? "Đang khóa" : "Mở"}</strong></div>
          <div><span>Khóa đến</span><strong>{fmtDateTime(health?.lockUntil ?? health?.LockUntil)}</strong></div>
          <div><span>Tổng phút trễ</span><strong>{health?.totalDelayMinutes ?? health?.TotalDelayMinutes ?? 0}</strong></div>
          <div><span>Ngưỡng trễ</span><strong>{health?.delayThresholdMinutes ?? health?.DelayThresholdMinutes ?? 0}</strong></div>
        </div>
        {(health?.message || health?.Message) ? <p>{health?.message || health?.Message}</p> : null}
      </section>

      {cycleResult ? (
        <section className="dispatch-cycle-result">
          <h2>Kết quả chu kỳ gần nhất</h2>
          <div className="health-grid">
            <div><span>Queue trước</span><strong>{cycleResult?.queueBefore ?? cycleResult?.QueueBefore ?? 0}</strong></div>
            <div><span>Queue sau</span><strong>{cycleResult?.queueAfter ?? cycleResult?.QueueAfter ?? 0}</strong></div>
            <div><span>Số xe điều phối</span><strong>{cycleResult?.dispatchedCount ?? cycleResult?.DispatchedCount ?? 0}</strong></div>
            <div><span>Thông báo trễ</span><strong>{cycleResult?.delayNotifications ?? cycleResult?.DelayNotifications ?? 0}</strong></div>
          </div>
        </section>
      ) : null}

      <section className="dispatch-bays">
        <h2>Trạng thái khoang</h2>
        <div className="dispatch-table-wrap">
          <table>
            <thead>
              <tr>
                <th>STT</th>
                <th>Khoang</th>
                <th>Loại</th>
                <th>Trạng thái</th>
                <th>Tràn từ DV nhanh</th>
                <th>Dự kiến trống</th>
              </tr>
            </thead>
            <tbody>
              {sortedBays.length === 0 ? (
                <tr>
                  <td colSpan={6}>Chưa có dữ liệu khoang.</td>
                </tr>
              ) : (
                sortedBays.map((bay) => {
                  const status = bay?.status || bay?.Status || "-";
                  return (
                    <tr key={bay?.bayId || bay?.BayId}>
                      <td>{bay?.sequenceOrder ?? bay?.SequenceOrder ?? "-"}</td>
                      <td>{bay?.bayCode || bay?.BayCode || bay?.bayName || bay?.BayName || "-"}</td>
                      <td>{bay?.serviceArea || bay?.ServiceArea || "-"}</td>
                      <td><span className={`pill ${statusTone(status)}`}>{status}</span></td>
                      <td>{(bay?.isOverflowFromQuick ?? bay?.IsOverflowFromQuick) ? "Có" : "Không"}</td>
                      <td>{fmtDateTime(bay?.plannedReleaseAt || bay?.PlannedReleaseAt || bay?.actualReleaseAt || bay?.ActualReleaseAt)}</td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}




