import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { FileText, ImagePlus, Paperclip, SendHorizontal, ShoppingBag, X } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import {
  getReceptionConversation,
  getReceptionConversationOrders,
  getReceptionConversations,
  getReceptionMessages,
  markReceptionRead,
  sendReceptionMessage,
  uploadReceptionChatAttachments,
} from "@features/chat/api/chatApi";
import {
  formatChatDividerTime,
  formatChatTimestamp,
  shouldShowChatTimeDivider,
} from "@features/chat/utils/chatTime";
import {
  CHAT_FILE_ACCEPT,
  CHAT_IMAGE_ACCEPT,
  CHAT_MAX_ATTACHMENT_COUNT,
  CHAT_MAX_ATTACHMENT_SIZE_BYTES,
  formatAttachmentSize,
  getAttachmentDisplayName,
  getExtensionFromPath,
  isImageAttachment,
  isImageFile,
  isSupportedAttachmentFile,
} from "@features/chat/utils/chatAttachment";
import {
  buildOrderCardMessagePayload,
  formatOrderAmount,
  getOrderCardFromMessage,
  getOrderTypeLabel,
} from "@features/chat/utils/chatOrderCard";
import { createChatHubConnection } from "@features/chat/realtime/chatHubConnection";
import "./ChatPages.css";

function getItems(response) {
  if (!response) return [];
  if (Array.isArray(response)) return response;
  if (Array.isArray(response.items)) return response.items;
  if (Array.isArray(response.Items)) return response.Items;
  return [];
}

function getId(item) {
  return item?.id ?? item?.Id ?? null;
}

function getCustomerDisplayName(item) {
  const name =
    item?.customerFullName ??
    item?.CustomerFullName ??
    item?.customerName ??
    item?.CustomerName ??
    "";

  if (String(name).trim()) {
    return String(name).trim();
  }

  const customerId = item?.customerId ?? item?.CustomerId ?? null;
  return customerId ? `User #${customerId}` : "User";
}

function getShortName(name) {
  const value = String(name || "").trim();
  if (!value) return "U";

  const parts = value.split(/\s+/).slice(0, 2);
  return parts.map((part) => part[0]).join("").toUpperCase();
}

function getAttachmentItems(message) {
  const raw = message?.attachments ?? message?.Attachments;
  if (Array.isArray(raw) && raw.length > 0) {
    return raw
      .map((item, index) => ({
        id: item?.id ?? item?.Id ?? `${getId(message) ?? "msg"}-att-${index}`,
        attachmentUrl: item?.attachmentUrl ?? item?.AttachmentUrl ?? "",
        contentType: item?.contentType ?? item?.ContentType ?? "",
        fileSize: item?.fileSize ?? item?.FileSize ?? null,
      }))
      .filter((item) => item.attachmentUrl);
  }

  const legacyUrl = message?.attachmentUrl ?? message?.AttachmentUrl ?? "";
  if (String(legacyUrl).trim()) {
    return [
      {
        id: `${getId(message) ?? "msg"}-legacy`,
        attachmentUrl: String(legacyUrl).trim(),
        contentType: "",
        fileSize: null,
      },
    ];
  }

  return [];
}

function getAttachmentMetaText(attachment) {
  const extension = getExtensionFromPath(attachment?.attachmentUrl).replace(".", "").toUpperCase();
  const sizeText = formatAttachmentSize(attachment?.fileSize);
  if (extension && sizeText) {
    return `${extension} | ${sizeText}`;
  }

  if (extension) {
    return extension;
  }

  if (sizeText) {
    return sizeText;
  }

  return "Tap de tai xuong";
}

function formatOrderCardDate(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleString("vi-VN", {
    hour12: false,
    hour: "2-digit",
    minute: "2-digit",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

function ChatOrderCard({ orderCard, onClick }) {
  if (!orderCard) return null;

  const amountText = formatOrderAmount(orderCard.totalAmount);
  const orderTime = formatOrderCardDate(orderCard.createdAt);
  const typeLabel = getOrderTypeLabel(orderCard.orderType);
  const fallbackThumbSrc = "/assets/booking/showcase-2.jpg";
  const [thumbSrc, setThumbSrc] = useState(orderCard.imageUrl || fallbackThumbSrc);
  const [usedFallback, setUsedFallback] = useState(!orderCard.imageUrl);

  useEffect(() => {
    const nextSrc = orderCard.imageUrl || fallbackThumbSrc;
    setThumbSrc(nextSrc);
    setUsedFallback(!orderCard.imageUrl);
  }, [orderCard.imageUrl]);

  return (
    <button
      type="button"
      className="chat-order-card"
      onClick={() => onClick(orderCard)}
      title="Mo chi tiet don hang"
    >
      <div className="chat-order-card-head">Ban dang hoi thong tin ve don hang sau:</div>
      <div className="chat-order-card-body">
        <div className="chat-order-card-thumb">
          {thumbSrc ? (
            <img
              src={thumbSrc}
              alt={orderCard.title}
              loading="lazy"
              onError={() => {
                if (!usedFallback) {
                  setThumbSrc(fallbackThumbSrc);
                  setUsedFallback(true);
                  return;
                }
                setThumbSrc("");
              }}
            />
          ) : (
            <span>{typeLabel}</span>
          )}
        </div>
        <div className="chat-order-card-main">
          <strong>{orderCard.title}</strong>
          <p>{orderCard.subtitle}</p>
          <div className="chat-order-card-line">
            <span className="chat-order-card-status">{orderCard.statusLabel || "Dang xu ly"}</span>
            {amountText ? <span className="chat-order-card-amount">{amountText}</span> : null}
          </div>
        </div>
      </div>
      <div className="chat-order-card-meta">
        <span>Ma don: {orderCard.orderCode}</span>
        {orderTime ? <span>{orderTime}</span> : null}
      </div>
    </button>
  );
}

export default function ReceptionChatPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const roleCode = String(user?.roleCode || user?.RoleCode || "").toUpperCase();
  const isReceptionist = roleCode === "RECEPTIONIST";
  const branchId = Number(user?.branchId ?? user?.BranchId ?? 0) || null;

  const [conversations, setConversations] = useState([]);
  const [selectedConversationId, setSelectedConversationId] = useState(null);
  const [selectedConversationDetail, setSelectedConversationDetail] = useState(null);
  const [messages, setMessages] = useState([]);
  const [composeText, setComposeText] = useState("");
  const [selectedFiles, setSelectedFiles] = useState([]);
  const [loadingConversations, setLoadingConversations] = useState(false);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");
  const [orderPickerOpen, setOrderPickerOpen] = useState(false);
  const [loadingOrders, setLoadingOrders] = useState(false);
  const [orderPickerError, setOrderPickerError] = useState("");
  const [conversationOrders, setConversationOrders] = useState([]);
  const [orderPreview, setOrderPreview] = useState(null);
  const realtimeConnectionRef = useRef(null);
  const selectedConversationIdRef = useRef(null);
  const lastMarkedReadByConversationRef = useRef(new Map());
  const messageListRef = useRef(null);
  const imageInputRef = useRef(null);
  const fileInputRef = useRef(null);

  const filePreviews = useMemo(
    () =>
      selectedFiles.map((file) => {
        const image = isImageFile(file);
        return {
          id: `${file.name}-${file.lastModified}-${file.size}`,
          file,
          isImage: image,
          url: image ? URL.createObjectURL(file) : "",
        };
      }),
    [selectedFiles]
  );

  useEffect(() => {
    return () => {
      filePreviews.forEach((item) => {
        if (item.url) {
          URL.revokeObjectURL(item.url);
        }
      });
    };
  }, [filePreviews]);

  useEffect(() => {
    selectedConversationIdRef.current = selectedConversationId;
  }, [selectedConversationId]);

  useEffect(() => {
    setSelectedFiles([]);
    setOrderPickerOpen(false);
    setOrderPickerError("");
    setConversationOrders([]);
    if (imageInputRef.current) {
      imageInputRef.current.value = "";
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  }, [selectedConversationId]);

  useEffect(() => {
    const list = messageListRef.current;
    if (!list || !selectedConversationId) {
      return;
    }

    window.requestAnimationFrame(() => {
      list.scrollTop = list.scrollHeight;
    });
  }, [messages.length, selectedConversationId]);

  const selectedConversation = useMemo(() => {
    const fromList =
      conversations.find((item) => item.id === selectedConversationId || item.Id === selectedConversationId) || null;
    return selectedConversationDetail || fromList;
  }, [conversations, selectedConversationDetail, selectedConversationId]);

  const loadConversations = useCallback(
    async (preferredConversationId = null, { silent = false } = {}) => {
      if (!silent) {
        setLoadingConversations(true);
        setError("");
      }

      try {
        const data = await getReceptionConversations({
          page: 1,
          pageSize: 100,
        });
        const items = getItems(data);
        setConversations(items);

        if (items.length === 0) {
          setSelectedConversationId(null);
          setSelectedConversationDetail(null);
          setMessages([]);
          return;
        }

        const wantedId =
          preferredConversationId ??
          selectedConversationIdRef.current ??
          getId(items[0]) ??
          null;
        setSelectedConversationId(wantedId);
      } catch (err) {
        if (!silent) {
          setError(err.message || "Khong tai duoc danh sach hoi thoai.");
        }
      } finally {
        if (!silent) {
          setLoadingConversations(false);
        }
      }
    },
    []
  );

  const markConversationReadIfNeeded = useCallback((conversationId, lastMessageId) => {
    if (!conversationId || !lastMessageId) {
      return;
    }

    const normalizedConversationId = Number(conversationId);
    const normalizedMessageId = Number(lastMessageId);
    if (!Number.isFinite(normalizedConversationId) || !Number.isFinite(normalizedMessageId)) {
      return;
    }

    const key = String(normalizedConversationId);
    const previous = Number(lastMarkedReadByConversationRef.current.get(key) || 0);
    if (normalizedMessageId <= previous) {
      return;
    }

    lastMarkedReadByConversationRef.current.set(key, normalizedMessageId);
    markReceptionRead(normalizedConversationId, { lastReadMessageId: normalizedMessageId }).catch(() => {});
  }, []);

  const loadConversationDetailAndMessages = useCallback(async (conversationId, { silent = false, markRead = true } = {}) => {
    if (!conversationId) return;
    if (!silent) {
      setLoadingMessages(true);
      setError("");
    }

    try {
      const detail = await getReceptionConversation(conversationId);
      setSelectedConversationDetail(detail || null);

      const detailMessages = getItems(detail?.messages ?? detail?.Messages);
      let nextMessages = detailMessages;
      if (nextMessages.length > 0) {
        setMessages(nextMessages);
      } else {
        const fetched = await getReceptionMessages(conversationId, { take: 200 });
        nextMessages = getItems(fetched);
        setMessages(nextMessages);
      }

      if (markRead) {
        const lastMessage = nextMessages.length > 0
          ? nextMessages[nextMessages.length - 1]
          : null;
        const lastMessageId = lastMessage?.id ?? lastMessage?.Id ?? null;
        markConversationReadIfNeeded(conversationId, lastMessageId);
      }
    } catch (err) {
      if (!silent) {
        setError(err.message || "Khong tai duoc chi tiet hoi thoai.");
      }
    } finally {
      if (!silent) {
        setLoadingMessages(false);
      }
    }
  }, [markConversationReadIfNeeded]);

  const loadConversationOrders = useCallback(async () => {
    if (!selectedConversationId) return;

    setLoadingOrders(true);
    setOrderPickerError("");
    try {
      const data = await getReceptionConversationOrders(selectedConversationId);
      const items = getItems(data).map((item) => getOrderCardFromMessage({ orderCard: item })).filter(Boolean);
      setConversationOrders(items);
    } catch (err) {
      setOrderPickerError(err.message || "Khong tai duoc danh sach don hang.");
      setConversationOrders([]);
    } finally {
      setLoadingOrders(false);
    }
  }, [selectedConversationId]);

  useEffect(() => {
    loadConversations();
  }, [loadConversations]);

  useEffect(() => {
    if (!selectedConversationId) return;
    loadConversationDetailAndMessages(selectedConversationId, { silent: false, markRead: true });
  }, [loadConversationDetailAndMessages, selectedConversationId]);

  useEffect(() => {
    if (!isReceptionist) {
      return undefined;
    }

    let disposed = false;
    const connection = createChatHubConnection();
    realtimeConnectionRef.current = connection;

    connection.on("ReceiveMessage", (message) => {
      const messageConversationId = message?.conversationId ?? message?.ConversationId ?? null;
      const activeConversationId = selectedConversationIdRef.current;

      if (!messageConversationId) {
        return;
      }

      if (activeConversationId && Number(messageConversationId) === Number(activeConversationId)) {
        const messageId = getId(message);
        setMessages((prev) => {
          if (messageId && prev.some((item) => getId(item) === messageId)) {
            return prev;
          }

          return [...prev, message];
        });

        markConversationReadIfNeeded(activeConversationId, messageId);
      }

      loadConversations(activeConversationId ?? Number(messageConversationId), { silent: true });
    });

    connection.on("ConversationUpdated", (payload) => {
      const updatedConversationId = payload?.conversationId ?? payload?.ConversationId ?? null;
      const activeConversationId = selectedConversationIdRef.current;

      if (updatedConversationId && activeConversationId && Number(updatedConversationId) === Number(activeConversationId)) {
        loadConversationDetailAndMessages(Number(activeConversationId), { silent: true, markRead: false });
      }

      loadConversations(activeConversationId ?? (updatedConversationId ? Number(updatedConversationId) : null), { silent: true });
    });

    connection.onreconnected(async () => {
      try {
        if (branchId) {
          await connection.invoke("JoinBranchInboxGroup", Number(branchId));
        }

        const activeConversationId = selectedConversationIdRef.current;
        if (activeConversationId) {
          await connection.invoke("JoinConversationGroup", Number(activeConversationId));
        }
      } catch (err) {
        console.error("ChatHub rejoin failed:", err);
      }
    });

    const startRealtime = async () => {
      try {
        await connection.start();
        if (disposed) {
          return;
        }

        if (branchId) {
          await connection.invoke("JoinBranchInboxGroup", Number(branchId));
        }

        const activeConversationId = selectedConversationIdRef.current;
        if (activeConversationId) {
          await connection.invoke("JoinConversationGroup", Number(activeConversationId));
        }
      } catch (err) {
        console.error("ChatHub reception connection failed:", err);
      }
    };

    startRealtime();

    return () => {
      disposed = true;
      connection.off("ReceiveMessage");
      connection.off("ConversationUpdated");
      realtimeConnectionRef.current = null;
      connection.stop().catch(() => {});
    };
  }, [branchId, isReceptionist, loadConversationDetailAndMessages, loadConversations, markConversationReadIfNeeded]);

  useEffect(() => {
    const connection = realtimeConnectionRef.current;
    if (!connection || !selectedConversationId || connection.state !== "Connected") {
      return undefined;
    }

    const id = Number(selectedConversationId);
    connection.invoke("JoinConversationGroup", id).catch((err) => {
      console.error("JoinConversationGroup failed:", err);
    });

    return () => {
      if (connection.state === "Connected") {
        connection.invoke("LeaveConversationGroup", id).catch(() => {});
      }
    };
  }, [selectedConversationId]);

  async function handleSendMessage(event) {
    event.preventDefault();
    if (!selectedConversationId) return;
    const content = composeText.trim();
    if (!content && selectedFiles.length === 0) return;
    setSending(true);
    setError("");
    try {
      const hasOnlyImageAttachments =
        selectedFiles.length > 0 && selectedFiles.every((file) => isImageFile(file));
      let attachmentUrls = [];
      if (selectedFiles.length > 0) {
        const uploaded = await uploadReceptionChatAttachments(selectedConversationId, selectedFiles);
        const uploadedItems = getItems(uploaded);
        attachmentUrls = uploadedItems
          .map((item) => item?.attachmentUrl ?? item?.AttachmentUrl ?? "")
          .filter((url) => String(url).trim());
      }

      await sendReceptionMessage(selectedConversationId, {
        content,
        messageType: attachmentUrls.length > 0 && hasOnlyImageAttachments ? "IMAGE" : "TEXT",
        attachmentUrls,
      });
      setComposeText("");
      setSelectedFiles([]);
      setOrderPickerOpen(false);
      if (imageInputRef.current) {
        imageInputRef.current.value = "";
      }
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
      await loadConversationDetailAndMessages(selectedConversationId, { silent: true, markRead: false });
      await loadConversations(selectedConversationId, { silent: true });
    } catch (err) {
      setError(err.message || "Gui tin nhan that bai.");
    } finally {
      setSending(false);
    }
  }

  async function handleSendOrderCard(orderCard) {
    if (!selectedConversationId) return;

    const payload = buildOrderCardMessagePayload(orderCard);
    if (!payload) {
      setOrderPickerError("Khong tao duoc noi dung don hang.");
      return;
    }

    setSending(true);
    setOrderPickerError("");
    setError("");
    try {
      await sendReceptionMessage(selectedConversationId, payload);
      setOrderPickerOpen(false);
      await loadConversationDetailAndMessages(selectedConversationId, { silent: true, markRead: false });
      await loadConversations(selectedConversationId, { silent: true });
    } catch (err) {
      setOrderPickerError(err.message || "Gui the don hang that bai.");
    } finally {
      setSending(false);
    }
  }

  function handleOpenOrderCard(orderCard) {
    if (!orderCard) return;
    if (orderCard.detailUrl) {
      navigate(orderCard.detailUrl);
      return;
    }

    setOrderPreview(orderCard);
  }

  async function toggleOrderPicker() {
    if (!selectedConversationId || sending) return;

    const nextOpen = !orderPickerOpen;
    setOrderPickerOpen(nextOpen);
    setOrderPickerError("");

    if (nextOpen) {
      await loadConversationOrders();
    }
  }

  function handleFileSelect(event, pickerType = "all") {
    const picked = Array.from(event.target.files || []);
    if (picked.length === 0) return;

    setError("");
    const next = [...selectedFiles];
    const onlyImages = pickerType === "image";
    const onlyFiles = pickerType === "file";
    for (const file of picked) {
      if (!isSupportedAttachmentFile(file)) {
        setError(`Tep ${file.name} khong duoc ho tro.`);
        continue;
      }

      const fileIsImage = isImageFile(file);
      if (onlyImages && !fileIsImage) {
        setError("Nut anh chi nhan tep anh.");
        continue;
      }

      if (onlyFiles && fileIsImage) {
        setError("Nut file chi nhan tai lieu.");
        continue;
      }

      if (file.size > CHAT_MAX_ATTACHMENT_SIZE_BYTES) {
        setError(`Tep ${file.name} vuot qua ${CHAT_MAX_ATTACHMENT_SIZE_BYTES / (1024 * 1024)}MB.`);
        continue;
      }

      if (next.length >= CHAT_MAX_ATTACHMENT_COUNT) {
        setError(`Toi da ${CHAT_MAX_ATTACHMENT_COUNT} tep moi lan gui.`);
        break;
      }

      const duplicated = next.some(
        (item) =>
          item.name === file.name &&
          item.size === file.size &&
          item.lastModified === file.lastModified
      );
      if (duplicated) {
        continue;
      }

      next.push(file);
    }

    setSelectedFiles(next);
    event.target.value = "";
  }

  function removeSelectedFile(targetId) {
    setSelectedFiles((prev) =>
      prev.filter((file) => `${file.name}-${file.lastModified}-${file.size}` !== targetId)
    );
  }

  return (
    <div className="chat-page-shell">
      <div className="chat-page-grid">
        <aside className="chat-sidebar">
          <div className="chat-panel-header">
            <div className="chat-panel-title-wrap">
              <h2>Danh sach user</h2>
              <small>{conversations.length} cuoc tro chuyen</small>
            </div>
            <button
              type="button"
              className="chat-btn ghost"
              onClick={() => loadConversations(selectedConversationId)}
              disabled={loadingConversations}
            >
              Tai lai
            </button>
          </div>

          <div className="chat-conversation-list">
            {loadingConversations ? <p className="chat-empty">Dang tai hoi thoai...</p> : null}
            {!loadingConversations && conversations.length === 0 ? (
              <p className="chat-empty">Chua co user nao nhan tin.</p>
            ) : null}

            {conversations.map((item) => {
              const id = item.id ?? item.Id;
              const active = id === selectedConversationId;
              const customerName = getCustomerDisplayName(item);
              const unreadCount = item.unreadCount ?? item.UnreadCount ?? 0;
              return (
                <button
                  key={id}
                  type="button"
                  className={`chat-conversation-item ${active ? "active" : ""}`}
                  onClick={() => setSelectedConversationId(id)}
                >
                  <div className="chat-conversation-top">
                    <div className="chat-user-brief">
                      <span className="chat-user-avatar">{getShortName(customerName)}</span>
                      <div className="chat-user-info">
                        <span className="chat-user-name">{customerName}</span>
                        <small>Khach hang</small>
                      </div>
                    </div>
                    <div className="chat-conversation-metrics">
                      <small>{formatChatTimestamp(item.lastMessageAt ?? item.LastMessageAt) || "--"}</small>
                      {unreadCount > 0 ? <span className="chat-badge">{unreadCount}</span> : null}
                    </div>
                  </div>
                  <p>{item.lastMessage ?? item.LastMessage ?? "(Khong co noi dung)"}</p>
                </button>
              );
            })}
          </div>
        </aside>

        <section className="chat-thread">
          <div className="chat-panel-header chat-thread-head">
            <div className="chat-panel-title-wrap">
              <h2>
                {selectedConversation
                  ? getCustomerDisplayName(selectedConversation)
                  : "Chon user de nhan tin"}
              </h2>
              <small>Nhan tin truc tiep voi khach hang</small>
            </div>
            {selectedConversation ? <span className="chat-live-chip">Dang online</span> : null}
          </div>

          {error ? <p className="chat-error">{error}</p> : null}

          <div ref={messageListRef} className="chat-message-list">
            {!selectedConversation ? <p className="chat-empty">Vui long chon hoi thoai de nhan tin.</p> : null}
            {selectedConversation && loadingMessages ? <p className="chat-empty">Dang tai tin nhan...</p> : null}
            {selectedConversation && !loadingMessages && messages.length === 0 ? (
              <p className="chat-empty">Chua co tin nhan.</p>
            ) : null}

            {messages.map((message, index) => {
              const senderType = String(message.senderType ?? message.SenderType ?? "").toUpperCase();
              const mine = senderType === "STAFF";
              const id = message.id ?? message.Id;
              const content = message.content ?? message.Content;
              const orderCard = getOrderCardFromMessage(message);
              const attachments = getAttachmentItems(message);
              const imageAttachments = attachments.filter((attachment) => isImageAttachment(attachment));
              const fileAttachments = attachments.filter((attachment) => !isImageAttachment(attachment));
              const senderLabel = mine ? "Le tan" : getCustomerDisplayName(selectedConversation);
              const senderAvatar = getShortName(senderLabel);
              const createdAt = message.createdAt ?? message.CreatedAt ?? null;
              const previous = index > 0 ? messages[index - 1] : null;
              const previousCreatedAt = previous?.createdAt ?? previous?.CreatedAt ?? null;
              const showDivider = shouldShowChatTimeDivider(previousCreatedAt, createdAt, 120);
              const dividerText = formatChatDividerTime(createdAt);

              return (
                <div key={id ?? `message-${index}`} className="chat-message-block">
                  {showDivider && dividerText ? <div className="chat-time-divider">{dividerText}</div> : null}
                  <article className={`chat-message-row ${mine ? "mine" : "other"}`}>
                    {!mine ? <span className="chat-message-avatar">{senderAvatar}</span> : null}
                    <div className="chat-message-content">
                      <div className={`chat-message-item ${orderCard ? "order-card-only" : ""}`}>
                        {orderCard ? (
                          <ChatOrderCard orderCard={orderCard} onClick={handleOpenOrderCard} />
                        ) : (
                          <>
                            {imageAttachments.length > 0 ? (
                              <div className={`chat-attachment-grid count-${Math.min(imageAttachments.length, 4)}`}>
                                {imageAttachments.map((attachment) => (
                                  <a
                                    key={attachment.id}
                                    href={attachment.attachmentUrl}
                                    target="_blank"
                                    rel="noreferrer"
                                    className="chat-attachment-item"
                                  >
                                    <img src={attachment.attachmentUrl} alt="attachment" loading="lazy" />
                                  </a>
                                ))}
                              </div>
                            ) : null}
                            {fileAttachments.length > 0 ? (
                              <div className="chat-file-list">
                                {fileAttachments.map((attachment, fileIndex) => (
                                  <a
                                    key={attachment.id}
                                    href={attachment.attachmentUrl}
                                    target="_blank"
                                    rel="noreferrer"
                                    className="chat-file-item"
                                  >
                                    <span className="chat-file-icon">
                                      <FileText size={14} />
                                    </span>
                                    <span className="chat-file-meta">
                                      <span className="chat-file-name">
                                        {getAttachmentDisplayName(
                                          attachment.attachmentUrl,
                                          `tep-dinh-kem-${fileIndex + 1}`
                                        )}
                                      </span>
                                      <small>{getAttachmentMetaText(attachment)}</small>
                                    </span>
                                  </a>
                                ))}
                              </div>
                            ) : null}
                            {content ? <p>{content}</p> : null}
                            {!content && attachments.length === 0 ? <p>(Attachment)</p> : null}
                          </>
                        )}
                      </div>
                    </div>
                  </article>
                </div>
              );
            })}
          </div>

          {filePreviews.length > 0 ? (
            <div className="chat-compose-preview">
              {filePreviews.map((item) => (
                <div
                  key={item.id}
                  className={`chat-compose-preview-item ${item.isImage ? "image" : "file"}`}
                >
                  {item.isImage ? (
                    <img src={item.url} alt={item.file.name} />
                  ) : (
                    <div className="chat-compose-preview-file">
                      <FileText size={16} />
                      <div>
                        <strong>{item.file.name}</strong>
                        <small>{formatAttachmentSize(item.file.size) || "tep"}</small>
                      </div>
                    </div>
                  )}
                  <button
                    type="button"
                    className="chat-preview-remove-btn"
                    onClick={() => removeSelectedFile(item.id)}
                    aria-label="Xoa anh"
                  >
                    <X size={12} />
                  </button>
                </div>
              ))}
            </div>
          ) : null}

          {orderPickerOpen ? (
            <div className="chat-order-picker">
              <div className="chat-order-picker-head">
                <strong>Chon don hang de gui</strong>
                <button
                  type="button"
                  className="chat-attach-btn"
                  onClick={() => setOrderPickerOpen(false)}
                  aria-label="Dong danh sach don hang"
                >
                  <X size={12} />
                </button>
              </div>
              {orderPickerError ? <p className="chat-error">{orderPickerError}</p> : null}
              {loadingOrders ? <p className="chat-empty">Dang tai don hang...</p> : null}
              {!loadingOrders && conversationOrders.length === 0 ? (
                <p className="chat-empty">Khong co don hang nao phu hop de gui.</p>
              ) : null}
              {!loadingOrders && conversationOrders.length > 0 ? (
                <div className="chat-order-picker-list">
                  {conversationOrders.map((order) => (
                    <button
                      key={`${order.orderType}-${order.orderId}`}
                      type="button"
                      className="chat-order-picker-item"
                      onClick={() => handleSendOrderCard(order)}
                      disabled={sending}
                    >
                      <div className="chat-order-picker-row">
                        <span>{order.title}</span>
                        <span>{formatOrderAmount(order.totalAmount)}</span>
                      </div>
                      <small>
                        {order.subtitle} - {order.statusLabel}
                      </small>
                    </button>
                  ))}
                </div>
              ) : null}
            </div>
          ) : null}

          <form className="chat-compose-form" onSubmit={handleSendMessage}>
            <input
              ref={imageInputRef}
              type="file"
              accept={CHAT_IMAGE_ACCEPT}
              multiple
              className="chat-file-input"
              onChange={(event) => handleFileSelect(event, "image")}
              disabled={!selectedConversation || sending}
            />
            <input
              ref={fileInputRef}
              type="file"
              accept={CHAT_FILE_ACCEPT}
              multiple
              className="chat-file-input"
              onChange={(event) => handleFileSelect(event, "file")}
              disabled={!selectedConversation || sending}
            />
            <button
              type="button"
              className="chat-attach-btn image"
              onClick={() => imageInputRef.current?.click()}
              disabled={!selectedConversation || sending}
              aria-label="Chon anh"
            >
              <ImagePlus size={16} />
            </button>
            <button
              type="button"
              className="chat-attach-btn file"
              onClick={() => fileInputRef.current?.click()}
              disabled={!selectedConversation || sending}
              aria-label="Chon tep dinh kem"
            >
              <Paperclip size={16} />
            </button>
            <button
              type="button"
              className="chat-attach-btn order"
              onClick={toggleOrderPicker}
              disabled={!selectedConversation || sending}
              aria-label="Gui the don hang"
            >
              <ShoppingBag size={16} />
            </button>
            <input
              type="text"
              placeholder="Nhap noi dung tin nhan..."
              value={composeText}
              onChange={(e) => setComposeText(e.target.value)}
              disabled={!selectedConversation || sending}
            />
            <button
              type="submit"
              className="chat-send-btn"
              disabled={!selectedConversation || sending || (!composeText.trim() && selectedFiles.length === 0)}
              aria-label="Gui tin nhan"
            >
              <SendHorizontal size={16} />
            </button>
          </form>
        </section>
      </div>

      {orderPreview ? (
        <div className="chat-order-modal-backdrop" onClick={() => setOrderPreview(null)}>
          <div className="chat-order-modal" onClick={(event) => event.stopPropagation()}>
            <header>
              <strong>{orderPreview.title}</strong>
              <button
                type="button"
                className="chat-attach-btn"
                onClick={() => setOrderPreview(null)}
                aria-label="Dong chi tiet don hang"
              >
                <X size={12} />
              </button>
            </header>
            <p>{orderPreview.subtitle}</p>
            <div className="chat-order-modal-grid">
              <span>Loai</span>
              <strong>{getOrderTypeLabel(orderPreview.orderType)}</strong>
              <span>Ma don</span>
              <strong>{orderPreview.orderCode}</strong>
              <span>Trang thai</span>
              <strong>{orderPreview.statusLabel}</strong>
              <span>Tong tien</span>
              <strong>{formatOrderAmount(orderPreview.totalAmount) || "--"}</strong>
              <span>Chi nhanh</span>
              <strong>{orderPreview.branchName || "--"}</strong>
            </div>
            {orderPreview.detailUrl ? (
              <button
                type="button"
                className="chat-order-modal-link"
                onClick={() => {
                  const target = orderPreview.detailUrl;
                  setOrderPreview(null);
                  navigate(target);
                }}
              >
                Mo trang chi tiet
              </button>
            ) : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}
