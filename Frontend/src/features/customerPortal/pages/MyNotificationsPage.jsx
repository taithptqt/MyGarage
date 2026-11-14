import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import Footer from "@shared/components/Footer";
import Header from "@shared/components/Header";
import {
  getCustomerNotifications,
  getServiceOrderLinkContext,
  markAllNotificationsRead,
  markNotificationRead,
} from "@features/customerPortal/api/customerPortalApi";
import {
  extractLegacySurveyServiceOrderId,
  isLegacySurveyNotificationLink,
  notificationCtaLabel,
  notificationTone,
  notificationTypeLabel,
  resolvePortalNotificationLink,
} from "@features/customerPortal/utils/notificationLinks";
import "./CustomerPortalShell.css";
import "./MyNotificationsPage.css";

const PAGE_SIZE = 25;
const PAGINATION_LABELS = { previous: "Trước", next: "Sau", page: "Trang" };

function fmtDt(v) {
  if (!v) return "—";
  try {
    return new Date(v).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" });
  } catch {
    return String(v);
  }
}

export default function MyNotificationsPage() {
  const navigate = useNavigate();
  const [filterUnread, setFilterUnread] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const [items, setItems] = useState([]);
  const [resolvedLinks, setResolvedLinks] = useState({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busyId, setBusyId] = useState(null);
  const [markingAll, setMarkingAll] = useState(false);
  const [page, setPage] = useState(1);
  const [matchingCount, setMatchingCount] = useState(0);
  const loadSequence = useRef(0);

  const load = useCallback(async () => {
    const sequence = ++loadSequence.current;
    setLoading(true);
    setError("");
    try {
      const res = await getCustomerNotifications({ unreadOnly: filterUnread, page, pageSize: PAGE_SIZE });
      if (sequence !== loadSequence.current) return;
      const total = Number(res?.totalCount) || 0;
      const lastPage = Math.max(1, Math.ceil(total / PAGE_SIZE));
      if (page > lastPage) {
        setPage(lastPage);
        return;
      }
      const nextItems = Array.isArray(res?.items) ? res.items : [];
      setMatchingCount(total);
      setUnreadCount(Number(res?.unreadCount) || 0);
      setItems(nextItems);

      const legacySurveyItems = nextItems.filter((item) =>
        isLegacySurveyNotificationLink(item.linkAction)
      );
      if (legacySurveyItems.length === 0) {
        setResolvedLinks({});
        return;
      }

      const entries = await Promise.all(
        legacySurveyItems.map(async (item) => {
          const serviceOrderId = extractLegacySurveyServiceOrderId(item.linkAction);
          if (!serviceOrderId) return [item.id, null];

          try {
            const context = await getServiceOrderLinkContext(serviceOrderId);
            return [item.id, context?.targetPath || null];
          } catch {
            return [item.id, null];
          }
        })
      );

      if (sequence !== loadSequence.current) return;
      setResolvedLinks(
        Object.fromEntries(entries.filter(([, path]) => typeof path === "string" && path.length > 0))
      );
    } catch (e) {
      if (sequence === loadSequence.current) setError(e?.message || "Không tải được thông báo.");
    } finally {
      if (sequence === loadSequence.current) setLoading(false);
    }
  }, [filterUnread, page]);

  useEffect(() => {
    load();
    return () => { loadSequence.current += 1; };
  }, [load]);

  const totalCount = items.length;
  const actionableCount = useMemo(
    () =>
      items.filter((item) => {
        const path = resolvedLinks[item.id] || resolvePortalNotificationLink(item.linkAction);
        return Boolean(path);
      }).length,
    [items, resolvedLinks]
  );

  const onOpenLink = (item) => {
    const path = resolvedLinks[item.id] || resolvePortalNotificationLink(item.linkAction);
    if (path) navigate(path);
  };

  const onMarkOne = async (id) => {
    setBusyId(id);
    try {
      await markNotificationRead(id);
      await load();
    } catch (e) {
      setError(e?.message || "Không cập nhật được.");
    } finally {
      setBusyId(null);
    }
  };

  const onMarkAll = async () => {
    setMarkingAll(true);
    try {
      await markAllNotificationsRead();
      await load();
    } catch (e) {
      setError(e?.message || "Không cập nhật được.");
    } finally {
      setMarkingAll(false);
    }
  };

  return (
    <div className="cp-page">
      <Header />
      <div className="cp-shell">
        <div className="cp-inner">
          <section className="cp-card cp-card--highlight cp-notif-hero">
            <div className="cp-notif-hero-copy">
              <p className="cp-eyebrow">Garage của tôi</p>
              <h1 className="cp-title">Thông báo</h1>
              <p className="cp-desc">
                Nhắc bảo dưỡng, khảo sát sau dịch vụ, bảo hiểm và cập nhật tiến độ từ xưởng
                đều được gom về một inbox dễ theo dõi.
              </p>
            </div>

            <div className="cp-notif-stats">
              <div className="cp-stat-card">
                <span className="cp-stat-label">Chưa đọc</span>
                <strong className="cp-stat-value">{unreadCount}</strong>
              </div>
              <div className="cp-stat-card">
                <span className="cp-stat-label">Đang hiển thị</span>
                <strong className="cp-stat-value">{totalCount}</strong>
              </div>
              <div className="cp-stat-card">
                <span className="cp-stat-label">Có CTA nội bộ</span>
                <strong className="cp-stat-value">{actionableCount}</strong>
              </div>
            </div>
          </section>

          <section className="cp-card cp-card--secondary cp-notif-toolbar-shell">
            <div className="cp-notif-toolbar">
              <div className="cp-notif-filters">
                <button
                  type="button"
                  className={`cp-tab ${!filterUnread ? "cp-tab--active" : ""}`}
                  onClick={() => { setPage(1); setFilterUnread(false); }}
                >
                  Tất cả
                </button>
                <button
                  type="button"
                  className={`cp-tab ${filterUnread ? "cp-tab--active" : ""}`}
                  onClick={() => { setPage(1); setFilterUnread(true); }}
                >
                  Chưa đọc
                </button>
              </div>
              <button
                type="button"
                className="cp-btn cp-btn-ghost"
                onClick={onMarkAll}
                disabled={markingAll || unreadCount === 0}
              >
            {markingAll ? "Đang xử lý..." : "Đánh dấu đã đọc tất cả"}
              </button>
            </div>
          </section>

          {error ? <div className="cp-error">{error}</div> : null}
          {loading ? <p className="cp-muted">Đang tải…</p> : null}

          {!loading && items.length === 0 ? (
            <section className="cp-card cp-empty-state">
              <h2 className="cp-empty-title">Inbox đang trống</h2>
              <p className="cp-muted">
                Khi có nhắc bảo dưỡng, bảo hiểm hay cập nhật từ xưởng, bạn sẽ thấy chúng ở đây.
              </p>
            </section>
          ) : null}

          <ul className="cp-notif-list">
            {!loading &&
              items.map((item) => {
                const resolvedPath = resolvedLinks[item.id] || resolvePortalNotificationLink(item.linkAction);
                const tone = notificationTone(item.derivedType);

                return (
                  <li
                    key={item.id}
                    className={`cp-card cp-notif-item cp-notif-item--${tone} ${
                      item.isRead ? "" : "cp-notif-item--unread"
                    }`}
                  >
                    <div className="cp-notif-item-head">
                      <div className="cp-notif-meta">
                        <span className={`cp-chip cp-chip--${tone}`}>
                          {notificationTypeLabel(item.derivedType)}
                        </span>
                        {!item.isRead ? <span className="cp-notif-unread-dot" /> : null}
                      </div>
                      <span className="cp-muted">{fmtDt(item.sentAt)}</span>
                    </div>

                    <div className="cp-notif-content">
                      <h2 className="cp-notif-title">{item.title}</h2>
                      <p className="cp-notif-body">{item.content}</p>
                    </div>

                    <div className="cp-notif-actions">
                      {resolvedPath ? (
                        <button
                          type="button"
                          className="cp-btn"
                          onClick={() => onOpenLink(item)}
                        >
                          {notificationCtaLabel(item.derivedType)}
                        </button>
                      ) : (
                        <span className="cp-muted cp-notif-hint">
                          Thông báo này hiện chưa có màn hình điều hướng trực tiếp.
                        </span>
                      )}

                      {!item.isRead ? (
                        <button
                          type="button"
                          className="cp-btn cp-btn-ghost"
                          disabled={busyId === item.id}
                          onClick={() => onMarkOne(item.id)}
                        >
                        {busyId === item.id ? "Đang cập nhật..." : "Đánh dấu đã đọc"}
                        </button>
                      ) : (
                        <span className="cp-notif-read-pill">Đã đọc</span>
                      )}
                    </div>
                  </li>
                );
              })}
          </ul>
          {matchingCount > PAGE_SIZE && (
            <nav className="cp-notif-toolbar" aria-label={PAGINATION_LABELS.page}>
              <button type="button" className="cp-btn cp-btn-ghost" disabled={loading || page <= 1}
                onClick={() => setPage((value) => value - 1)}>{PAGINATION_LABELS.previous}</button>
              <span>{PAGINATION_LABELS.page} {page} / {Math.ceil(matchingCount / PAGE_SIZE)}</span>
              <button type="button" className="cp-btn cp-btn-ghost" disabled={loading || page * PAGE_SIZE >= matchingCount}
                onClick={() => setPage((value) => value + 1)}>{PAGINATION_LABELS.next}</button>
            </nav>
          )}
        </div>
      </div>
      <Footer />
    </div>
  );
}


