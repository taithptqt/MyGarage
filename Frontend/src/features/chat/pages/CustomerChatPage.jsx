import { useCallback, useEffect, useMemo, useState } from "react";
import {
  createCustomerConversation,
  getCustomerConversations,
  getCustomerMessages,
  markCustomerRead,
  sendCustomerMessage,
} from "@features/chat/api/chatApi";
import "./ChatPages.css";

const CUSTOMER_STATUS_OPTIONS = [
  { value: "", label: "Tat ca" },
  { value: "OPEN", label: "OPEN" },
  { value: "WAITING_FOR_STAFF", label: "WAITING_FOR_STAFF" },
  { value: "WAITING_FOR_CUSTOMER", label: "WAITING_FOR_CUSTOMER" },
  { value: "CLOSED", label: "CLOSED" },
];

function formatTime(value) {
  if (!value) return "--";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "--";
  return date.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
}

function getItems(response) {
  if (!response) return [];
  if (Array.isArray(response)) return response;
  if (Array.isArray(response.items)) return response.items;
  if (Array.isArray(response.Items)) return response.Items;
  return [];
}

function toOptionalLong(value) {
  const trimmed = String(value || "").trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export default function CustomerChatPage() {
  const [statusFilter, setStatusFilter] = useState("");
  const [conversations, setConversations] = useState([]);
  const [selectedConversationId, setSelectedConversationId] = useState(null);
  const [messages, setMessages] = useState([]);
  const [composeText, setComposeText] = useState("");
  const [loadingConversations, setLoadingConversations] = useState(false);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [sending, setSending] = useState(false);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState("");
  const [createForm, setCreateForm] = useState({
    branchId: "",
    appointmentId: "",
    serviceOrderId: "",
    vehicleId: "",
    initialMessage: "",
  });

  const selectedConversation = useMemo(
    () => conversations.find((item) => item.id === selectedConversationId || item.Id === selectedConversationId) || null,
    [conversations, selectedConversationId]
  );

  const loadConversations = useCallback(
    async (preferredConversationId = null) => {
      setLoadingConversations(true);
      setError("");
      try {
        const data = await getCustomerConversations({
          status: statusFilter || undefined,
          page: 1,
          pageSize: 100,
        });
        const items = getItems(data);
        setConversations(items);

        if (items.length === 0) {
          setSelectedConversationId(null);
          setMessages([]);
          return;
        }

        const wantedId =
          preferredConversationId ??
          selectedConversationId ??
          items[0]?.id ??
          items[0]?.Id ??
          null;
        setSelectedConversationId(wantedId);
      } catch (err) {
        setError(err.message || "Không tải được danh sách hội thoại.");
      } finally {
        setLoadingConversations(false);
      }
    },
    [selectedConversationId, statusFilter]
  );

  const loadMessages = useCallback(async (conversationId) => {
    if (!conversationId) return;
    setLoadingMessages(true);
    setError("");
    try {
      const data = await getCustomerMessages(conversationId, { take: 200 });
      const nextMessages = getItems(data);
      setMessages(nextMessages);
      const lastMessage = nextMessages[nextMessages.length - 1];
      const lastMessageId = lastMessage?.id ?? lastMessage?.Id ?? null;
      if (lastMessageId) {
        markCustomerRead(conversationId, { lastReadMessageId: lastMessageId }).catch(() => {});
      }
    } catch (err) {
      setError(err.message || "Không tải được tin nhắn.");
    } finally {
      setLoadingMessages(false);
    }
  }, []);

  useEffect(() => {
    loadConversations();
  }, [loadConversations]);

  useEffect(() => {
    if (!selectedConversationId) return;
    loadMessages(selectedConversationId);
  }, [loadMessages, selectedConversationId]);

  async function handleCreateConversation(event) {
    event.preventDefault();
    const branchId = toOptionalLong(createForm.branchId);
    if (!branchId) {
      setError("BranchId phai lon hon 0.");
      return;
    }

    setCreating(true);
    setError("");
    try {
      const created = await createCustomerConversation({
        branchId,
        appointmentId: toOptionalLong(createForm.appointmentId),
        serviceOrderId: toOptionalLong(createForm.serviceOrderId),
        vehicleId: toOptionalLong(createForm.vehicleId),
        initialMessage: String(createForm.initialMessage || "").trim() || null,
      });
      const createdId = created?.id ?? created?.Id ?? null;
      setCreateForm({
        branchId: "",
        appointmentId: "",
        serviceOrderId: "",
        vehicleId: "",
        initialMessage: "",
      });
      await loadConversations(createdId);
    } catch (err) {
      setError(err.message || "Không tạo được hội thoại.");
    } finally {
      setCreating(false);
    }
  }

  async function handleSendMessage(event) {
    event.preventDefault();
    if (!selectedConversationId) return;
    const content = composeText.trim();
    if (!content) return;

    setSending(true);
    setError("");
    try {
      await sendCustomerMessage(selectedConversationId, {
        content,
        messageType: "TEXT",
      });
      setComposeText("");
      await loadMessages(selectedConversationId);
      await loadConversations(selectedConversationId);
    } catch (err) {
      setError(err.message || "Gửi tin nhắn thất bại.");
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="chat-page-shell">
      <div className="chat-page-grid">
        <aside className="chat-sidebar">
          <div className="chat-panel-header">
            <h2>Tin nhắn của tôi</h2>
            <button
              type="button"
              className="chat-btn ghost"
              onClick={() => loadConversations(selectedConversationId)}
              disabled={loadingConversations}
            >
              Tải lại
            </button>
          </div>

          <div className="chat-filter-row">
            <label htmlFor="customer-chat-status">Trạng thái</label>
            <select
              id="customer-chat-status"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
            >
              {CUSTOMER_STATUS_OPTIONS.map((option) => (
                <option key={option.value || "ALL"} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          <div className="chat-conversation-list">
            {loadingConversations ? <p className="chat-empty">Đang tải hội thoại...</p> : null}
            {!loadingConversations && conversations.length === 0 ? (
              <p className="chat-empty">Chưa có hội thoại. Tạo mới bên dưới.</p>
            ) : null}

            {conversations.map((item) => {
              const id = item.id ?? item.Id;
              const active = id === selectedConversationId;
              return (
                <button
                  key={id}
                  type="button"
                  className={`chat-conversation-item ${active ? "active" : ""}`}
                  onClick={() => setSelectedConversationId(id)}
                >
                  <div className="chat-conversation-top">
                    <span>Hội thoại #{id}</span>
                    <span className="chat-badge">{item.unreadCount ?? item.UnreadCount ?? 0}</span>
                  </div>
                  <div className="chat-conversation-bottom">
                    <span className="chat-status">{item.status ?? item.Status ?? "--"}</span>
                    <small>{formatTime(item.lastMessageAt ?? item.LastMessageAt)}</small>
                  </div>
                  <p>{item.lastMessage ?? item.LastMessage ?? "(Không có nội dung)"}</p>
                </button>
              );
            })}
          </div>

          <form className="chat-create-form" onSubmit={handleCreateConversation}>
            <h3>Tạo hội thoại mới</h3>
            <div className="chat-form-grid">
              <input
                type="number"
                min="1"
                placeholder="BranchId *"
                value={createForm.branchId}
                onChange={(e) => setCreateForm((prev) => ({ ...prev, branchId: e.target.value }))}
              />
              <input
                type="number"
                min="1"
                placeholder="AppointmentId"
                value={createForm.appointmentId}
                onChange={(e) => setCreateForm((prev) => ({ ...prev, appointmentId: e.target.value }))}
              />
              <input
                type="number"
                min="1"
                placeholder="ServiceOrderId"
                value={createForm.serviceOrderId}
                onChange={(e) => setCreateForm((prev) => ({ ...prev, serviceOrderId: e.target.value }))}
              />
              <input
                type="number"
                min="1"
                placeholder="VehicleId"
                value={createForm.vehicleId}
                onChange={(e) => setCreateForm((prev) => ({ ...prev, vehicleId: e.target.value }))}
              />
            </div>
            <textarea
              rows={3}
              placeholder="Tin nhắn mở đầu (tùy chọn)"
              value={createForm.initialMessage}
              onChange={(e) => setCreateForm((prev) => ({ ...prev, initialMessage: e.target.value }))}
            />
            <button type="submit" className="chat-btn primary" disabled={creating}>
              {creating ? "Đang tạo..." : "Tạo hội thoại"}
            </button>
          </form>
        </aside>

        <section className="chat-thread">
          <div className="chat-panel-header">
            <h2>
              {selectedConversation
                  ? `Hội thoại #${selectedConversation.id ?? selectedConversation.Id}`
                  : "Chọn hội thoại"}
            </h2>
            {selectedConversation ? (
              <span className="chat-status-chip">{selectedConversation.status ?? selectedConversation.Status}</span>
            ) : null}
          </div>

          {error ? <p className="chat-error">{error}</p> : null}

          <div className="chat-message-list">
            {!selectedConversation ? <p className="chat-empty">Vui lòng chọn hội thoại để nhắn tin.</p> : null}
            {selectedConversation && loadingMessages ? <p className="chat-empty">Đang tải tin nhắn...</p> : null}
            {selectedConversation && !loadingMessages && messages.length === 0 ? (
              <p className="chat-empty">Chưa có tin nhắn.</p>
            ) : null}

            {messages.map((message) => {
              const senderType = String(message.senderType ?? message.SenderType ?? "").toUpperCase();
              const mine = senderType === "CUSTOMER";
              const id = message.id ?? message.Id;
              const content = message.content ?? message.Content;
              return (
                <article key={id} className={`chat-message-item ${mine ? "mine" : "other"}`}>
                  <header>
                    <strong>{senderType || "UNKNOWN"}</strong>
                    <small>{formatTime(message.createdAt ?? message.CreatedAt)}</small>
                  </header>
                  <p>{content || "(Attachment)"}</p>
                </article>
              );
            })}
          </div>

          <form className="chat-compose-form" onSubmit={handleSendMessage}>
            <textarea
              rows={3}
              placeholder="Nhập nội dung tin nhắn..."
              value={composeText}
              onChange={(e) => setComposeText(e.target.value)}
              disabled={!selectedConversation || sending}
            />
            <button type="submit" className="chat-btn primary" disabled={!selectedConversation || sending || !composeText.trim()}>
              {sending ? "Đang gửi..." : "Gửi tin nhắn"}
            </button>
          </form>
        </section>
      </div>
    </div>
  );
}


