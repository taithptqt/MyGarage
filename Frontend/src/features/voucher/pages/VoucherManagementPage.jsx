import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Modal } from "@features/admin/components/AdminModals";
import { voucherApi } from "@features/voucher/api/voucherApi";
import { getVoucherPage, getRedemptionPage, getApprovalPage, getVoucherReportPage, getCampaignPage } from "@features/voucher/api/voucherPagingApi";
import TablePagination from "@shared/components/TablePagination";
import "../../admin/pages/AdminPage.css";
import "./VoucherManagementPage.css";

const TITLES = {
  dashboard: ["Bảng điều khiển voucher", "Tổng quan hiệu quả voucher"],
  list: ["Danh sách voucher", "Tạo, sửa và quản lý vòng đời voucher"],
  campaigns: ["Chiến dịch", "Tổng hợp voucher theo chiến dịch"],
  conflicts: ["Kiểm tra xung đột", "Kiểm tra xung đột điều kiện áp dụng"],
  redemptions: ["Lịch sử sử dụng", "Theo dõi áp dụng và hoàn voucher"],
  approvals: ["Hàng chờ phê duyệt", "Xử lý yêu cầu ngoại lệ"],
  reports: ["Báo cáo hiệu quả", "Báo cáo hiệu quả và rủi ro"],
  settings: ["Thiết lập chính sách", "Chính sách vận hành voucher"]
};

const STATUS_BADGE = {
  DRAFT: "badge-info",
  PENDING_APPROVAL: "badge-suspended",
  SCHEDULED: "badge-info",
  ACTIVE: "badge-active",
  PAUSED: "badge-suspended",
  ENDED: "badge-inactive",
  EXHAUSTED: "badge-warning",
  EXPIRED: "badge-inactive",
  REVOKED: "badge-inactive",
  ARCHIVED: "badge-inactive",
  PENDING: "badge-suspended",
  APPROVED: "badge-active",
  REJECTED: "badge-inactive",
  REDEEMED: "badge-active",
  REVERSED: "badge-warning"
};

const STATUS_LABEL = {
  DRAFT: "Nháp",
  PENDING_APPROVAL: "Chờ duyệt",
  SCHEDULED: "Đã lên lịch",
  ACTIVE: "Đang hoạt động",
  PAUSED: "Tạm dừng",
  ENDED: "Đã kết thúc",
  EXHAUSTED: "Hết lượt",
  EXPIRED: "Hết hạn",
  REVOKED: "Đã thu hồi",
  ARCHIVED: "Lưu trữ",
  PENDING: "Chờ duyệt",
  APPROVED: "Đã duyệt",
  REJECTED: "Từ chối",
  REDEEMED: "Đã sử dụng",
  REVERSED: "Đã hoàn"
};

const SECTION_NEEDS = {
  vouchers: new Set([]),
  campaigns: new Set([]),
  redemptions: new Set([]),
  approvals: new Set([])
};

const up = (v) => String(v || "").trim().toUpperCase();
const num = (v, d = 0) => (Number.isFinite(Number(v)) ? Number(v) : d);
const tnull = (v) => {
  const s = String(v ?? "").trim();
  return s || null;
};
const nnull = (v) => {
  const s = String(v ?? "").trim();
  if (!s) return null;
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
};
const arr = (v) => (Array.isArray(v) ? v : Array.isArray(v?.items) ? v.items : []);
const rows = (v) => arr(v).filter((x) => x && typeof x === "object");
const statusText = (s) => STATUS_LABEL[up(s)] || up(s) || "-";
const pct = (a, b) => (!b || b <= 0 ? 0 : Math.max(0, Math.min(100, Math.round((a / b) * 100))));
const dt = (v) => {
  if (!v) return "-";
  const d = new Date(v);
  return Number.isNaN(d.getTime())
    ? "-"
    : d.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
};
const money = (v) =>
  Number.isFinite(Number(v))
    ? Number(v).toLocaleString("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 })
    : "-";
const budgetAlertText = (campaign) => {
  if (campaign?.isBudgetExceeded) return "Vượt ngân sách";
  if (campaign?.isNearBudgetLimit) return "Sắp chạm ngân sách";
  if (num(campaign?.budgetLimit, 0) <= 0) return "Không giới hạn";
  return "Bình thường";
};
const budgetAlertClass = (campaign) => {
  if (campaign?.isBudgetExceeded) return "badge-inactive";
  if (campaign?.isNearBudgetLimit) return "badge-warning";
  if (num(campaign?.budgetLimit, 0) <= 0) return "badge-info";
  return "badge-active";
};
const toIso = (v) => {
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? new Date().toISOString() : d.toISOString();
};
const toInput = (v) => {
  const d = new Date(v || Date.now());
  const o = d.getTimezoneOffset() * 60000;
  return new Date(d.getTime() - o).toISOString().slice(0, 16);
};
const withTimeout = (promise, timeoutMs, message) =>
  Promise.race([promise, new Promise((_, reject) => setTimeout(() => reject(new Error(message)), timeoutMs))]);
const uid = () => {
  try {
    const u = JSON.parse(localStorage.getItem("mygarage_user") || "{}");
    return Number(u?.id || u?.staffId || u?.staff?.id) || null;
  } catch {
    return null;
  }
};

function EmptyState({ message }) {
  return (
    <div className="empty-state">
      <span className="empty-icon">*</span>
      <span>{message}</span>
    </div>
  );
}

function formDefault() {
  const now = new Date();
  const end = new Date(now);
  end.setDate(end.getDate() + 30);
  return {
    id: null,
    code: "",
    voucherName: "",
    type: "FIXED",
    value: "",
    minOrderValue: "",
    maxDiscountAmount: "",
    usageLimit: "",
    budgetLimit: "",
    campaignId: "",
    startDate: toInput(now),
    endDate: toInput(end),
    allowedChannels: "",
    allowedPaymentMethods: "",
    isStackable: false,
    approvalRequired: false,
    isActive: true,
    description: ""
  };
}

const mapForm = (v) => ({
  id: v.id,
  code: v.code || "",
  voucherName: v.voucherName || "",
  type: v.type || "FIXED",
  value: v.value ?? "",
  minOrderValue: v.minOrderValue ?? "",
  maxDiscountAmount: v.maxDiscountAmount ?? "",
  usageLimit: v.usageLimit ?? "",
  budgetLimit: v.budgetLimit ?? "",
  campaignId: v.campaignId ?? "",
  startDate: toInput(v.startDate),
  endDate: toInput(v.endDate),
  allowedChannels: v.allowedChannels || "",
  allowedPaymentMethods: v.allowedPaymentMethods || "",
  isStackable: !!v.isStackable,
  approvalRequired: !!v.approvalRequired,
  isActive: v.isActive ?? true,
  description: v.description || ""
});

function campaignDefault() {
  const now = new Date();
  const end = new Date(now);
  end.setDate(end.getDate() + 30);
  return {
    id: null,
    campaignCode: "",
    name: "",
    objective: "",
    description: "",
    startDate: toInput(now),
    endDate: toInput(end),
    budgetLimit: "",
    status: "",
    isActive: true,
    ownerStaffId: "",
    voucherIds: [],
    replaceVoucherScope: true
  };
}

const mapCampaignForm = (item) => ({
  id: item.id ?? null,
  campaignCode: item.campaignCode || "",
  name: item.name || "",
  objective: item.objective || "",
  description: item.description || "",
  startDate: toInput(item.startDate),
  endDate: toInput(item.endDate),
  budgetLimit: item.budgetLimit ?? "",
  status: item.status || "",
  isActive: item.isActive ?? true,
  ownerStaffId: item.ownerStaffId ?? "",
  voucherIds: Array.isArray(item.voucherIds)
    ? item.voucherIds.map((x) => Number(x)).filter((x) => Number.isFinite(x))
    : [],
  replaceVoucherScope: true
});

export default function VoucherManagementPage({ section = "dashboard" }) {
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [q, setQ] = useState("");
  const [status, setStatus] = useState("ALL");

  const [vouchers, setVouchers] = useState([]);
  const [voucherRows, setVoucherRows] = useState([]);
  const [voucherTotal, setVoucherTotal] = useState(0);
  const [voucherStatuses, setVoucherStatuses] = useState([]);
  const [voucherLoading, setVoucherLoading] = useState(false);
  const [voucherError, setVoucherError] = useState("");
  const [voucherSearch, setVoucherSearch] = useState("");
  const voucherLoadSequence = useRef(0);
  const [redemptionRows, setRedemptionRows] = useState([]);
  const [redemptionTotal, setRedemptionTotal] = useState(0);
  const [redemptionLoading, setRedemptionLoading] = useState(false);
  const [redemptionError, setRedemptionError] = useState("");
  const redemptionSequence = useRef(0);
  const [approvalRows, setApprovalRows] = useState([]);
  const [approvalTotal, setApprovalTotal] = useState(0);
  const [approvalLoading, setApprovalLoading] = useState(false);
  const [approvalError, setApprovalError] = useState("");
  const approvalSequence = useRef(0);
  const [reportVouchers, setReportVouchers] = useState([]);
  const [reportVoucherTotal, setReportVoucherTotal] = useState(0);
  const [reportRedemptions, setReportRedemptions] = useState([]);
  const [reportRedemptionTotal, setReportRedemptionTotal] = useState(0);
  const [reportStats, setReportStats] = useState({ issued: 0, redeemed: 0, budgetLimit: 0, budgetUsed: 0, discount: 0 });
  const [reportLoading, setReportLoading] = useState(false);
  const [reportError, setReportError] = useState("");
  const reportSequence = useRef(0);
  const [campaignPageRows, setCampaignPageRows] = useState([]);
  const [campaignTotal, setCampaignTotal] = useState(0);
  const [campaignStatuses, setCampaignStatuses] = useState([]);
  const [campaignStats, setCampaignStats] = useState({ totalCampaigns: 0, activeCampaigns: 0, totalRedeemed: 0, budgetWarningCount: 0 });
  const [campaignWarnings, setCampaignWarnings] = useState([]);
  const [campaignLoading, setCampaignLoading] = useState(false);
  const [campaignError, setCampaignError] = useState("");
  const campaignSequence = useRef(0);
  const [campaignVoucherRows, setCampaignVoucherRows] = useState([]);
  const [campaignVoucherTotal, setCampaignVoucherTotal] = useState(0);
  const [campaignVoucherPage, setCampaignVoucherPage] = useState(1);
  const [campaignVoucherPageSize, setCampaignVoucherPageSize] = useState(20);
  const [campaignVoucherQuery, setCampaignVoucherQuery] = useState("");
  const [campaignVoucherSearch, setCampaignVoucherSearch] = useState("");
  const [campaignVoucherLoading, setCampaignVoucherLoading] = useState(false);
  const [campaignVoucherError, setCampaignVoucherError] = useState("");
  const campaignVoucherSequence = useRef(0);
  const [campaignDetailLoading, setCampaignDetailLoading] = useState(false);
  const campaignDetailSequence = useRef(0);
  const [dashboardData, setDashboardData] = useState(null);
  const dashboardSequence = useRef(0);
  const [campaigns, setCampaigns] = useState([]);
  const [redemptions, setRedemptions] = useState([]);
  const [approvals, setApprovals] = useState([]);

  const [modalOpen, setModalOpen] = useState(false);
  const [mode, setMode] = useState("create");
  const [form, setForm] = useState(formDefault());
  const [saving, setSaving] = useState(false);
  const [selected, setSelected] = useState(null);
  const [selectedCampaign, setSelectedCampaign] = useState(null);
  const [campaignDetailPage, setCampaignDetailPage] = useState(1);
  const [campaignDetailPageSize, setCampaignDetailPageSize] = useState(10);
  const [campaignDetailVouchers, setCampaignDetailVouchers] = useState([]);
  const [campaignDetailTotal, setCampaignDetailTotal] = useState(0);
  const [campaignDetailPageLoading, setCampaignDetailPageLoading] = useState(false);
  const [campaignDetailPageError, setCampaignDetailPageError] = useState("");
  const campaignDetailPageSequence = useRef(0);
  const [approvalBy, setApprovalBy] = useState(String(uid() || ""));
  const [approvalNote, setApprovalNote] = useState("");

  const [campaignModalOpen, setCampaignModalOpen] = useState(false);
  const [campaignMode, setCampaignMode] = useState("create");
  const [campaignSaving, setCampaignSaving] = useState(false);
  const [campaignForm, setCampaignForm] = useState(campaignDefault());
  const [campaignFilterStatus, setCampaignFilterStatus] = useState("ALL");

  const [policy, setPolicy] = useState(() => {
    try {
      return { requireCapPercent: true, blockStacking: true, ...JSON.parse(localStorage.getItem("voucher_admin_policy") || "{}") };
    } catch {
      return { requireCapPercent: true, blockStacking: true };
    }
  });

  const [confA, setConfA] = useState("");
  const [confB, setConfB] = useState("");
  const [confQuery, setConfQuery] = useState("");
  const [confSearch, setConfSearch] = useState("");
  const [confPage, setConfPage] = useState(1);
  const [confTotal, setConfTotal] = useState(0);
  const [confPageSize, setConfPageSize] = useState(20);
  const [confSelected, setConfSelected] = useState({});
  const [confLoading, setConfLoading] = useState(false);
  const confSequence = useRef(0);
  const [voucherPage, setVoucherPage] = useState(1);
  const [voucherPageSize, setVoucherPageSize] = useState(10);
  const [campaignPage, setCampaignPage] = useState(1);
  const [campaignPageSize, setCampaignPageSize] = useState(10);
  const [redemptionPage, setRedemptionPage] = useState(1);
  const [redemptionPageSize, setRedemptionPageSize] = useState(10);
  const [approvalPage, setApprovalPage] = useState(1);
  const [approvalPageSize, setApprovalPageSize] = useState(10);
  const [reportVoucherPage, setReportVoucherPage] = useState(1);
  const [reportVoucherPageSize, setReportVoucherPageSize] = useState(10);
  const [reportRedemptionPage, setReportRedemptionPage] = useState(1);
  const [reportRedemptionPageSize, setReportRedemptionPageSize] = useState(10);
  async function load(targetSection = section) {
    if (targetSection === "dashboard") {
      const sequence = ++dashboardSequence.current;
      try {
        const result = await voucherApi.getDashboard();
        if (sequence === dashboardSequence.current) { setDashboardData(result); setError(""); }
      } catch (e) {
        if (sequence === dashboardSequence.current) { setDashboardData(null); setError(e.message || "Không thể tải dữ liệu voucher."); }
      }
      return;
    }
    const timeoutMs = 9000;
    const errors = [];
    const tasks = [];

    if (targetSection !== "list" && SECTION_NEEDS.vouchers.has(targetSection)) {
      tasks.push({ key: "vouchers", promise: withTimeout(getVoucherPage({ page: 1, pageSize: 20 }), timeoutMs, "Hết thời gian tải danh sách voucher") });
    }
    if (SECTION_NEEDS.campaigns.has(targetSection)) {
      tasks.push({ key: "campaigns", promise: withTimeout(getCampaignPage({ page: 1, pageSize: 20, includeDeleted: false }), timeoutMs, "Hết thời gian tải danh sách chiến dịch") });
    }
    if (SECTION_NEEDS.redemptions.has(targetSection)) {
      tasks.push({ key: "redemptions", promise: withTimeout(getRedemptionPage({ page: 1, pageSize: 20 }), timeoutMs, "Hết thời gian tải lịch sử sử dụng") });
    }
    if (SECTION_NEEDS.approvals.has(targetSection)) {
      tasks.push({ key: "approvals", promise: withTimeout(getApprovalPage({ page: 1, pageSize: 20 }), timeoutMs, "Hết thời gian tải hàng chờ phê duyệt") });
    }

    const results = await Promise.allSettled(tasks.map((x) => x.promise));
    results.forEach((result, index) => {
      const key = tasks[index].key;
      if (result.status === "fulfilled") {
        if (key === "vouchers") setVouchers(rows(result.value));
        if (key === "campaigns") setCampaigns(rows(result.value));
        if (key === "redemptions") setRedemptions(rows(result.value));
        if (key === "approvals") setApprovals(rows(result.value));
        return;
      }
      if (key === "vouchers") setVouchers([]);
      if (key === "campaigns") setCampaigns([]);
      if (key === "redemptions") setRedemptions([]);
      if (key === "approvals") setApprovals([]);
      errors.push(result.reason?.message || `Lỗi tải ${key}`);
    });

    setError(errors.join(" | "));
  }

  useEffect(() => {
    let on = true;
    setError("");
    campaignDetailSequence.current += 1;
    setCampaignDetailLoading(false);
    (async () => {
      setLoading(true);
      if (section === "list") await loadVoucherPage(); else if (section !== "redemptions" && section !== "approvals" && section !== "reports" && section !== "campaigns") await load(section);
      if (on) setLoading(false);
    })();
    return () => {
      on = false;
      dashboardSequence.current += 1;
    };
  }, [section]);

  useEffect(() => {
    const timer = window.setTimeout(() => setVoucherSearch(q.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [q]);

  const loadVoucherPage = useCallback(async () => {
    const sequence = ++voucherLoadSequence.current;
    setVoucherLoading(true);
    setVoucherError("");
    try {
      const result = await getVoucherPage({ search: voucherSearch || undefined, status, page: voucherPage, pageSize: voucherPageSize });
      if (sequence !== voucherLoadSequence.current) return;
      setVoucherRows(rows(result?.items));
      setVoucherTotal(Number(result?.total) || 0);
      setVoucherStatuses(Array.isArray(result?.statuses) ? result.statuses.filter(Boolean) : []);
    } catch (e) {
      if (sequence === voucherLoadSequence.current) setVoucherError(e.message || "Hết thời gian tải danh sách voucher");
    } finally {
      if (sequence === voucherLoadSequence.current) setVoucherLoading(false);
    }
  }, [voucherSearch, status, voucherPage, voucherPageSize]);

  useEffect(() => {
    if (section === "list") loadVoucherPage();
    return () => { voucherLoadSequence.current += 1; };
  }, [section, loadVoucherPage]);

  useEffect(() => {
    if (section !== "list" || voucherLoading || voucherError) return;
    const totalPages = Math.max(1, Math.ceil(voucherTotal / voucherPageSize));
    if (voucherPage > totalPages) setVoucherPage(totalPages);
  }, [section, voucherLoading, voucherError, voucherTotal, voucherPage, voucherPageSize]);

  const loadRedemptionPage = useCallback(async () => {
    const sequence = ++redemptionSequence.current;
    setRedemptionLoading(true);
    setRedemptionError("");
    try {
      const result = await getRedemptionPage({ search: voucherSearch || undefined, page: redemptionPage, pageSize: redemptionPageSize });
      if (sequence !== redemptionSequence.current) return;
      setRedemptionRows(rows(result?.items));
      setRedemptionTotal(Number(result?.total) || 0);
    } catch (e) {
      if (sequence === redemptionSequence.current) { setRedemptionRows([]); setRedemptionTotal(0); setRedemptionError(e.message || "Hết thời gian tải lịch sử sử dụng"); }
    } finally {
      if (sequence === redemptionSequence.current) setRedemptionLoading(false);
    }
  }, [voucherSearch, redemptionPage, redemptionPageSize]);

  const loadApprovalPage = useCallback(async () => {
    const sequence = ++approvalSequence.current;
    setApprovalLoading(true);
    setApprovalError("");
    try {
      const result = await getApprovalPage({ search: voucherSearch || undefined, page: approvalPage, pageSize: approvalPageSize });
      if (sequence !== approvalSequence.current) return;
      setApprovalRows(rows(result?.items));
      setApprovalTotal(Number(result?.total) || 0);
    } catch (e) {
      if (sequence === approvalSequence.current) { setApprovalRows([]); setApprovalTotal(0); setApprovalError(e.message || "Hết thời gian tải hàng chờ phê duyệt"); }
    } finally {
      if (sequence === approvalSequence.current) setApprovalLoading(false);
    }
  }, [voucherSearch, approvalPage, approvalPageSize]);

  useEffect(() => {
    if (section === "redemptions") loadRedemptionPage();
    return () => { redemptionSequence.current += 1; };
  }, [section, loadRedemptionPage]);
  useEffect(() => {
    if (section === "approvals") loadApprovalPage();
    return () => { approvalSequence.current += 1; };
  }, [section, loadApprovalPage]);
  useEffect(() => {
    if (section !== "redemptions" || redemptionLoading || redemptionError) return;
    const lastPage = Math.max(1, Math.ceil(redemptionTotal / redemptionPageSize));
    if (redemptionPage > lastPage) setRedemptionPage(lastPage);
  }, [section, redemptionLoading, redemptionError, redemptionTotal, redemptionPageSize, redemptionPage]);
  useEffect(() => {
    if (section !== "approvals" || approvalLoading || approvalError) return;
    const lastPage = Math.max(1, Math.ceil(approvalTotal / approvalPageSize));
    if (approvalPage > lastPage) setApprovalPage(lastPage);
  }, [section, approvalLoading, approvalError, approvalTotal, approvalPageSize, approvalPage]);

  const loadReport = useCallback(async () => {
    const sequence = ++reportSequence.current;
    setReportLoading(true);
    setReportError("");
    try {
      const [voucherResult, redemptionResult] = await Promise.all([
        getVoucherReportPage({ page: reportVoucherPage, pageSize: reportVoucherPageSize }),
        getRedemptionPage({ sort: "discountDesc", page: reportRedemptionPage, pageSize: reportRedemptionPageSize }),
      ]);
      if (sequence !== reportSequence.current) return;
      setReportVouchers(rows(voucherResult?.items).map((x) => ({ ...x, issued: num(x.issuedCount, num(x.usageLimit, 0)), redeemed: num(x.redeemedCount, num(x.usedCount, 0)) })));
      setReportVoucherTotal(Number(voucherResult?.total) || 0);
      setReportRedemptions(rows(redemptionResult?.items));
      setReportRedemptionTotal(Number(redemptionResult?.total) || 0);
      setReportStats({ issued: num(voucherResult?.issued, 0), redeemed: num(voucherResult?.redeemed, 0), budgetLimit: num(voucherResult?.budgetLimit, 0), budgetUsed: num(voucherResult?.budgetUsed, 0), discount: num(redemptionResult?.totalDiscount, 0) });
    } catch (e) {
      if (sequence === reportSequence.current) {
        setReportVouchers([]); setReportVoucherTotal(0); setReportRedemptions([]); setReportRedemptionTotal(0);
        setReportStats({ issued: 0, redeemed: 0, budgetLimit: 0, budgetUsed: 0, discount: 0 });
        setReportError(e.message || "Không thể tải báo cáo voucher.");
      }
    } finally {
      if (sequence === reportSequence.current) setReportLoading(false);
    }
  }, [reportVoucherPage, reportVoucherPageSize, reportRedemptionPage, reportRedemptionPageSize]);
  useEffect(() => {
    if (section === "reports") loadReport();
    return () => { reportSequence.current += 1; };
  }, [section, loadReport]);
  useEffect(() => {
    if (section !== "reports" || reportLoading || reportError) return;
    const lastVoucherPage = Math.max(1, Math.ceil(reportVoucherTotal / reportVoucherPageSize));
    const lastRedemptionPage = Math.max(1, Math.ceil(reportRedemptionTotal / reportRedemptionPageSize));
    if (reportVoucherPage > lastVoucherPage) setReportVoucherPage(lastVoucherPage);
    if (reportRedemptionPage > lastRedemptionPage) setReportRedemptionPage(lastRedemptionPage);
  }, [section, reportLoading, reportError, reportVoucherTotal, reportVoucherPage, reportVoucherPageSize, reportRedemptionTotal, reportRedemptionPage, reportRedemptionPageSize]);

  const loadCampaignPage = useCallback(async () => {
    const sequence = ++campaignSequence.current;
    setCampaignLoading(true);
    setCampaignError("");
    try {
      const result = await getCampaignPage({ search: voucherSearch || undefined, status: campaignFilterStatus, page: campaignPage, pageSize: campaignPageSize });
      if (sequence !== campaignSequence.current) return;
      setCampaignPageRows(rows(result?.items));
      setCampaignTotal(Number(result?.total) || 0);
      setCampaignStatuses(Array.isArray(result?.statuses) ? result.statuses.filter(Boolean) : []);
      setCampaignStats(result?.stats || { totalCampaigns: 0, activeCampaigns: 0, totalRedeemed: 0, budgetWarningCount: 0 });
      setCampaignWarnings(rows(result?.budgetWarnings));
    } catch (e) {
      if (sequence === campaignSequence.current) {
        setCampaignPageRows([]); setCampaignTotal(0); setCampaignStatuses([]); setCampaignWarnings([]);
        setCampaignStats({ totalCampaigns: 0, activeCampaigns: 0, totalRedeemed: 0, budgetWarningCount: 0 });
        setCampaignError(e.message || "Không thể tải chiến dịch.");
      }
    } finally { if (sequence === campaignSequence.current) setCampaignLoading(false); }
  }, [voucherSearch, campaignFilterStatus, campaignPage, campaignPageSize]);
  useEffect(() => {
    if (section === "campaigns") loadCampaignPage();
    return () => { campaignSequence.current += 1; };
  }, [section, loadCampaignPage]);
  useEffect(() => {
    if (section !== "campaigns" || campaignLoading || campaignError) return;
    const lastPage = Math.max(1, Math.ceil(campaignTotal / campaignPageSize));
    if (campaignPage > lastPage) setCampaignPage(lastPage);
  }, [section, campaignLoading, campaignError, campaignTotal, campaignPage, campaignPageSize]);
  useEffect(() => {
    const timer = window.setTimeout(() => setCampaignVoucherSearch(campaignVoucherQuery.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [campaignVoucherQuery]);
  useEffect(() => {
    if (!campaignModalOpen) return undefined;
    const sequence = ++campaignVoucherSequence.current;
    setCampaignVoucherLoading(true);
    setCampaignVoucherError("");
    getVoucherPage({ search: campaignVoucherSearch || undefined, includeDeleted: false, page: campaignVoucherPage, pageSize: campaignVoucherPageSize })
      .then((result) => {
        if (sequence !== campaignVoucherSequence.current) return;
        setCampaignVoucherRows(rows(result?.items));
        setCampaignVoucherTotal(Number(result?.total) || 0);
      }).catch((e) => {
        if (sequence !== campaignVoucherSequence.current) return;
        setCampaignVoucherRows([]); setCampaignVoucherTotal(0); setCampaignVoucherError(e.message || "Không thể tải danh sách voucher.");
      }).finally(() => { if (sequence === campaignVoucherSequence.current) setCampaignVoucherLoading(false); });
    return () => { campaignVoucherSequence.current += 1; };
  }, [campaignModalOpen, campaignVoucherSearch, campaignVoucherPage, campaignVoucherPageSize]);

  const statusOptions = ["ALL", ...voucherStatuses];
  const issued = num(dashboardData?.issued);
  const redeemed = num(dashboardData?.redeemed);
  const discount = num(dashboardData?.totalDiscount);
  const activeCount = num(dashboardData?.activeCount);
  const pendingApprovals = num(dashboardData?.pendingApprovals);
  const budgetLimit = num(dashboardData?.budgetLimit);
  const budgetUsed = num(dashboardData?.budgetUsed);
  const topVouchers = rows(dashboardData?.topVouchers).map((v) => ({ ...v, issued: num(v.issuedCount, num(v.usageLimit)), redeemed: num(v.redeemedCount, num(v.usedCount)) }));
  const nearQuotaVouchers = rows(dashboardData?.nearQuotaVouchers);
  const dashboardApprovals = rows(dashboardData?.approvalItems);

  const campaignStatusOptions = ["ALL", ...campaignStatuses];

  const isDashboard = section === "dashboard";
  const isList = section === "list";
  const isCampaigns = section === "campaigns";
  const isConflicts = section === "conflicts";
  const isRedemptions = section === "redemptions";
  const isApprovals = section === "approvals";
  const isReports = section === "reports";
  const isSettings = section === "settings";
  useEffect(() => {
    setVoucherPage(1);
    setCampaignPage(1);
    setRedemptionPage(1);
    setApprovalPage(1);
    setReportVoucherPage(1);
    setReportRedemptionPage(1);
  }, [section, q, status, campaignFilterStatus]);

  const refresh = async () => {
    setBusy(true);
    if (section === "list") await loadVoucherPage(); else if (section === "redemptions") await loadRedemptionPage(); else if (section === "approvals") await loadApprovalPage(); else if (section === "reports") await loadReport(); else if (section === "campaigns") await loadCampaignPage(); else await load(section);
    setBusy(false);
  };
  const openCreate = () => {
    setMode("create");
    setForm(formDefault());
    setModalOpen(true);
  };
  const openEdit = (v) => {
    setMode("edit");
    setForm(mapForm(v));
    setModalOpen(true);
  };

  const openCreateCampaign = () => {
    campaignDetailSequence.current += 1;
    setCampaignDetailLoading(false);
    setCampaignVoucherQuery("");
    setCampaignVoucherSearch("");
    setCampaignVoucherPage(1);
    setCampaignMode("create");
    setCampaignForm(campaignDefault());
    setCampaignModalOpen(true);
  };

  const openEditCampaign = async (campaign) => {
    const sequence = ++campaignDetailSequence.current;
    setCampaignDetailLoading(true); setError("");
    try {
      const fullCampaign = await voucherApi.getCampaignById(campaign.id);
      if (sequence !== campaignDetailSequence.current) return;
      setCampaignVoucherQuery(""); setCampaignVoucherSearch(""); setCampaignVoucherPage(1);
      setCampaignMode("edit"); setCampaignForm(mapCampaignForm(fullCampaign)); setCampaignModalOpen(true);
    } catch (e) { if (sequence === campaignDetailSequence.current) setError(e.message || "Không thể tải chiến dịch."); }
    finally { if (sequence === campaignDetailSequence.current) setCampaignDetailLoading(false); }
  };
  const openCampaignDetail = async (campaign) => {
    const sequence = ++campaignDetailSequence.current;
    setCampaignDetailLoading(true); setError("");
    try {
      const details = await voucherApi.getCampaignSummary(campaign.id);
      if (sequence === campaignDetailSequence.current) { setCampaignDetailVouchers([]); setCampaignDetailTotal(0); setCampaignDetailPage(1); setSelectedCampaign({ ...campaign, ...details }); }
    } catch (e) { if (sequence === campaignDetailSequence.current) setError(e.message || "Không thể tải chiến dịch."); }
    finally { if (sequence === campaignDetailSequence.current) setCampaignDetailLoading(false); }
  };

  useEffect(() => {
    if (!selectedCampaign?.id) return undefined;
    const sequence = ++campaignDetailPageSequence.current;
    setCampaignDetailPageLoading(true); setCampaignDetailPageError("");
    voucherApi.getCampaignVoucherPage(selectedCampaign.id, { page: campaignDetailPage, pageSize: campaignDetailPageSize })
      .then((result) => { if (sequence === campaignDetailPageSequence.current) { setCampaignDetailVouchers(rows(result?.items)); setCampaignDetailTotal(Number(result?.total) || 0); } })
      .catch((e) => { if (sequence === campaignDetailPageSequence.current) { setCampaignDetailVouchers([]); setCampaignDetailTotal(0); setCampaignDetailPageError(e.message || "Không thể tải danh sách voucher."); } })
      .finally(() => { if (sequence === campaignDetailPageSequence.current) setCampaignDetailPageLoading(false); });
    return () => { campaignDetailPageSequence.current += 1; };
  }, [selectedCampaign?.id, campaignDetailPage, campaignDetailPageSize]);

  useEffect(() => {
    if (!selectedCampaign || campaignDetailPageLoading || campaignDetailPageError) return;
    const lastPage = Math.max(1, Math.ceil(campaignDetailTotal / campaignDetailPageSize));
    if (campaignDetailPage > lastPage) setCampaignDetailPage(lastPage);
  }, [selectedCampaign, campaignDetailPageLoading, campaignDetailPageError, campaignDetailTotal, campaignDetailPageSize, campaignDetailPage]);

  const toggleCampaignVoucher = (voucherId) => {
    const id = Number(voucherId);
    if (!Number.isFinite(id)) return;
    setCampaignForm((prev) => {
      const set = new Set(prev.voucherIds || []);
      if (set.has(id)) set.delete(id);
      else set.add(id);
      return { ...prev, voucherIds: Array.from(set) };
    });
  };

  async function submitCampaign(e) {
    e.preventDefault();
    if (!campaignForm.name.trim()) {
      setError("Tên chiến dịch không được để trống.");
      return;
    }
    if (new Date(campaignForm.endDate) < new Date(campaignForm.startDate)) {
      setError("Thời gian kết thúc phải lơn hơn hoặc bằng thời gian bắt đầu.");
      return;
    }

    const payload = {
      campaignCode: tnull(campaignForm.campaignCode),
      name: tnull(campaignForm.name),
      objective: tnull(campaignForm.objective),
      description: tnull(campaignForm.description),
      startDate: toIso(campaignForm.startDate),
      endDate: toIso(campaignForm.endDate),
      budgetLimit: nnull(campaignForm.budgetLimit),
      status: tnull(campaignForm.status),
      ownerStaffId: nnull(campaignForm.ownerStaffId),
      isActive: !!campaignForm.isActive,
      updatedBy: uid(),
      voucherIds: (campaignForm.voucherIds || []).map((x) => Number(x)).filter((x) => Number.isFinite(x)),
      replaceVoucherScope: campaignForm.replaceVoucherScope !== false
    };

    if (campaignMode === "create") {
      payload.createdBy = uid();
    }

    try {
      setCampaignSaving(true);
      if (campaignMode === "create") {
        await voucherApi.createCampaign(payload);
      } else {
        await voucherApi.updateCampaign(campaignForm.id, payload);
      }
      setCampaignModalOpen(false);
      await refresh();
    } catch (ex) {
      setError(ex.message || "Không lưu được chiến dịch.");
    } finally {
      setCampaignSaving(false);
    }
  }

  async function removeCampaign(campaign) {
    const label = campaign?.name || campaign?.campaignCode || `#${campaign?.id}`;
    const ok = window.confirm(`Bạn có chắc muốn xóa chiến dịch ${label}?`);
    if (!ok) return;

    try {
      await voucherApi.deleteCampaign(campaign.id);
      if (selectedCampaign?.id === campaign.id) setSelectedCampaign(null);
      await refresh();
    } catch (ex) {
      setError(ex.message || "Không xóa được chiến dịch.");
    }
  }

  async function submit(e) {
    e.preventDefault();
    if (!form.code.trim() || !form.voucherName.trim()) return;
    const payload = {
      code: tnull(form.code),
      voucherName: tnull(form.voucherName),
      type: tnull(form.type),
      value: nnull(form.value),
      minOrderValue: nnull(form.minOrderValue),
      maxDiscountAmount: nnull(form.maxDiscountAmount),
      usageLimit: nnull(form.usageLimit),
      budgetLimit: nnull(form.budgetLimit),
      campaignId: nnull(form.campaignId),
      startDate: toIso(form.startDate),
      endDate: toIso(form.endDate),
      allowedChannels: tnull(form.allowedChannels),
      allowedPaymentMethods: tnull(form.allowedPaymentMethods),
      isStackable: !!form.isStackable,
      approvalRequired: !!form.approvalRequired,
      isActive: !!form.isActive,
      description: tnull(form.description),
      updatedBy: uid()
    };
    if (mode === "create") {
      payload.createdBy = uid();
      payload.status = payload.isActive ? "ACTIVE" : "PAUSED";
      payload.usedCount = 0;
      payload.issuedCount = 0;
      payload.redeemedCount = 0;
      payload.budgetUsed = 0;
    }

    try {
      setSaving(true);
      if (mode === "create") await voucherApi.createVoucher(payload);
      else await voucherApi.updateVoucher(form.id, payload);
      setModalOpen(false);
      await refresh();
    } catch (e2) {
      setError(e2.message || "Không lưu được voucher");
    } finally {
      setSaving(false);
    }
  }
  async function changeStatus(id, action) {
    try {
      if (action === "activate") await voucherApi.activateVoucher(id);
      if (action === "pause") await voucherApi.pauseVoucher(id);
      if (action === "revoke") await voucherApi.revokeVoucher(id);
      await refresh();
    } catch (e) {
      setError(e.message || "Không đđ.i được trạng thái");
    }
  }

  async function decide(item, act) {
    const approverId = nnull(approvalBy);
    if (!approverId) {
      setError("Cần nhập ID người duyệt");
      return;
    }
    try {
      if (act === "approve") await voucherApi.approveRequest(item.id, { approverId, note: tnull(approvalNote) });
      else await voucherApi.rejectRequest(item.id, { approverId, note: tnull(approvalNote), reason: tnull(approvalNote) || "Từ chối" });
      await refresh();
    } catch (e) {
      setError(e.message || "Không xử lý được phê duyệt");
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => { setConfSearch(confQuery.trim()); setConfPage(1); }, 250);
    return () => window.clearTimeout(timer);
  }, [confQuery]);

  useEffect(() => {
    if (section !== "conflicts") return undefined;
    const sequence = ++confSequence.current;
    setConfLoading(true);
    getVoucherPage({ search: confSearch || undefined, page: confPage, pageSize: confPageSize })
      .then((result) => {
        if (sequence !== confSequence.current) return;
        setVouchers(rows(result?.items)); setConfTotal(Number(result?.total) || 0); setError("");
      }).catch((e) => {
        if (sequence === confSequence.current) { setVouchers([]); setConfTotal(0); setError(e.message || "Không thể tải danh sách voucher."); }
      }).finally(() => { if (sequence === confSequence.current) setConfLoading(false); });
    return () => { confSequence.current += 1; };
  }, [section, confSearch, confPage, confPageSize]);

  const selectConflict = (value, setter) => {
    setter(value);
    const item = vouchers.find((v) => String(v.id) === value) || confSelected[value];
    if (item) setConfSelected((previous) => ({ ...previous, [value]: item }));
  };
  const conflictOptions = [...new Map([...vouchers, ...Object.values(confSelected)].map((v) => [String(v.id), v])).values()];
  const confResult = useMemo(() => {
    const a = confSelected[confA];
    const b = confSelected[confB];
    if (!a || !b || a.id === b.id) return [];
    const out = [];
    if (up(a.code) === up(b.code)) out.push("Trùng mã voucher: bắt buộc chọn.");
    if (!a.isStackable || !b.isStackable) out.push("Có voucher không cho phép cộng dôn.");
    const overlap = new Date(a.startDate) <= new Date(b.endDate) && new Date(b.startDate) <= new Date(a.endDate);
    out.push(overlap ? "Trùng thời gian hiệu lực." : "Không trùng thời gian hiệu lực.");
    return out;
  }, [confA, confB, confSelected]);

  if (loading) {
    return (
      <div className="management-content voucher-admin-page">
        <div className="loading-state"><span className="spinner" /><span className="voucher-loading-text">Đang tải dữ liệu voucher...</span></div>
      </div>
    );
  }

  return (
    <div className="management-content voucher-admin-page">
      {(error || (isList && voucherError) || (isRedemptions && redemptionError) || (isApprovals && approvalError) || (isReports && reportError) || (isCampaigns && campaignError)) ? <div className="alert alert-error">{error || (isList ? voucherError : isRedemptions ? redemptionError : isApprovals ? approvalError : isReports ? reportError : campaignError)}</div> : null}

      {isDashboard ? (
        <>
          <div className="stats-bar">
            <div className="stat-card"><div className="stat-value">{activeCount}</div><div className="stat-label">Voucher hoạt động</div></div>
            <div className="stat-card"><div className="stat-value">{issued}</div><div className="stat-label">Đã phát hành</div></div>
            <div className="stat-card"><div className="stat-value">{redeemed}</div><div className="stat-label">Đã sử dụng</div></div>
            <div className="stat-card"><div className="stat-value">{pct(redeemed, issued)}%</div><div className="stat-label">Tỷ lệ sử dụng</div></div>
            <div className="stat-card"><div className="stat-value">{money(discount)}</div><div className="stat-label">Tổng giảm giá</div></div>
            <div className="stat-card"><div className="stat-value">{pendingApprovals}</div><div className="stat-label">Chờ phê duyệt</div></div>
          </div>

          <div className="table-toolbar voucher-toolbar">
            <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới bảng điều khiển"}</button>
          </div>

          <div className="table-wrapper">
            <table className="admin-table">
              <thead><tr><th>Top voucher</th><th>Mã</th><th>Đã sử dụng</th><th>Đã phát hành</th><th>Tỷ lệ</th></tr></thead>
              <tbody>
                {topVouchers.length === 0 ? <tr><td colSpan={5}><EmptyState message="Chưa có dữ liệu top voucher" /></td></tr> : topVouchers.map((v) => (
                  <tr key={v.id}><td>{v.voucherName || "-"}</td><td>{v.code || "-"}</td><td>{v.redeemed}</td><td>{v.issued}</td><td>{pct(v.redeemed, v.issued)}%</td></tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="voucher-card-grid">
            <div className="voucher-panel-card">
              <h3>Ngân sách voucher</h3>
              <p>Đã dùng: <strong>{money(budgetUsed)}</strong></p>
              <p>Giới hạn: <strong>{money(budgetLimit)}</strong></p>
              <div className="voucher-progress"><div className="voucher-progress-fill" style={{ width: `${pct(budgetUsed, budgetLimit)}%` }} /></div>
            </div>
            <div className="voucher-panel-card">
              <h3>Voucher sắp hết quota</h3>
              {nearQuotaVouchers.length === 0 ? <p className="voucher-subtext">Không có voucher cảnh báo</p> : nearQuotaVouchers.map((v) => (
                <div key={v.id} className="voucher-subtext">{v.code || v.voucherName}: {Math.round(v.ratio * 100)}%</div>
              ))}
            </div>
            <div className="voucher-panel-card">
              <h3>Phê duyệt cần xử lý</h3>
              {dashboardApprovals.map((x) => (
                <div key={x.id} className="voucher-subtext">#{x.id} - {x.requestType || "N/A"} - {x.riskLevel || "-"}</div>
              ))}
              {pendingApprovals === 0 ? <p className="voucher-subtext">Không có yêu cầu chờ duyệt</p> : null}
            </div>
          </div>
        </>
      ) : null}

      {isList ? (
        <>
          <div className="table-toolbar voucher-toolbar">
            <input className="search-input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm voucher..." />
            <select className="voucher-select" value={status} onChange={(e) => setStatus(e.target.value)}>
              {statusOptions.map((x) => <option key={x} value={x}>{x === "ALL" ? "Tất cả" : statusText(x)}</option>)}
            </select>
            <button className="btn-primary" onClick={openCreate}>+ Tạo voucher</button>
            <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới danh sách"}</button>
          </div>

          <div className="table-wrapper">
            <table className="admin-table">
              <thead><tr><th>ID</th><th>Mã</th><th>Tên voucher</th><th>Loại</th><th>Giá trị</th><th>Thời gian hiệu lực</th><th>Lượt dùng</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
              <tbody>
                {voucherLoading ? <tr><td colSpan={9}>Đang tải dữ liệu voucher...</td></tr> : voucherError ? null : voucherRows.length === 0 ? <tr><td colSpan={9}><EmptyState message="Không có voucher" /></td></tr> : voucherRows.map((v) => (
                  <tr key={v.id}>
                    <td className="td-id">#{v.id}</td><td>{v.code}</td><td>{v.voucherName}</td><td>{v.type}</td>
                    <td>{v.type === "PERCENT" ? `${v.value || 0}%` : money(v.value)}</td>
                    <td>{dt(v.startDate)} - {dt(v.endDate)}</td>
                    <td>{num(v.redeemedCount, num(v.usedCount, 0))}/{num(v.issuedCount, num(v.usageLimit, 0))}</td>
                    <td><span className={`badge ${STATUS_BADGE[up(v.status)] || "badge-inactive"}`}>{statusText(v.status)}</span></td>
                    <td><div className="row-actions voucher-actions-col">
                      <button className="btn-icon" onClick={() => setSelected(v)}>Chi tiết</button>
                      <button className="btn-icon" onClick={() => openEdit(v)}>Sửa</button>
                      {up(v.status) === "ACTIVE" ? <button className="btn-icon" onClick={() => changeStatus(v.id, "pause")}>Tạm dừng</button> : <button className="btn-icon" onClick={() => changeStatus(v.id, "activate")}>Kích hoạt</button>}
                      <button className="btn-icon" onClick={() => changeStatus(v.id, "revoke")}>Thu hôi</button>
                    </div></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div style={{ display: "flex", justifyContent: "center" }}>
            {!voucherError && <TablePagination
              totalItems={voucherTotal}
              currentPage={voucherPage}
              pageSize={voucherPageSize}
              onPageChange={setVoucherPage}
              onPageSizeChange={(size) => {
                setVoucherPageSize(size);
                setVoucherPage(1);
              }}
            />}
          </div>
        </>
      ) : null}

      {isCampaigns ? (
        <>
          <div className="voucher-card-grid">
            <div className="voucher-panel-card"><h3>Tổng chiến dịch</h3><p><strong>{campaignStats.totalCampaigns}</strong></p></div>
            <div className="voucher-panel-card"><h3>Chiến dịch đang hoạt động</h3><p><strong>{campaignStats.activeCampaigns}</strong></p></div>
            <div className="voucher-panel-card"><h3>Tổng lượt sử dụng</h3><p><strong>{campaignStats.totalRedeemed}</strong></p></div>
            <div className="voucher-panel-card"><h3>Cảnh báo ngân sách</h3><p><strong>{campaignStats.budgetWarningCount}</strong></p></div>
          </div>
          <div className="table-toolbar voucher-toolbar">
            <input className="search-input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm chiến dịch..." />
            <select className="voucher-select" value={campaignFilterStatus} onChange={(e) => setCampaignFilterStatus(e.target.value)}>
              {campaignStatusOptions.map((x) => <option key={x} value={x}>{x === "ALL" ? "Tất cả trạng thái" : statusText(x)}</option>)}
            </select>
            <button className="btn-primary" onClick={openCreateCampaign}>+ Tạo chiến dịch</button>
            <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới chiến dịch"}</button>
          </div>
          {campaignStats.budgetWarningCount > 0 ? (
            <div className="voucher-budget-warning-box">
              <strong>{campaignStats.budgetWarningCount} chiến dịch đang sắp hết hoặc vượt ngân sách:</strong>
              <div className="voucher-budget-warning-list">
                {campaignWarnings.map((c) => (
                  <span key={c.id} className="voucher-budget-warning-chip">
                    {c.name || c.campaignCode || `#${c.id}`}: {num(c.budgetUsagePercent, 0)}%
                  </span>
                ))}
              </div>
            </div>
          ) : null}
          <div className="table-wrapper">
            <table className="admin-table">
              <thead><tr><th>ID</th><th>Mã chiến dịch</th><th>Tên chiến dịch</th><th>Thời gian</th><th>Voucher áp dụng</th><th>Đã sử dụng</th><th>Ngân sách đã dùng</th><th>Tỷ lệ ngân sách</th><th>Cảnh báo</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
              <tbody>
                {campaignLoading ? <tr><td colSpan={11}>Đang tải dữ liệu voucher...</td></tr> : campaignPageRows.length === 0 ? <tr><td colSpan={11}><EmptyState message="Không có chiến dịch phù hợp" /></td></tr> : campaignPageRows.map((r) => (
                  <tr key={r.id}>
                    <td className="td-id">#{r.id}</td>
                    <td>{r.campaignCode || "-"}</td>
                    <td>
                      <div className="voucher-cell-title">{r.name || "-"}</div>
                      <div className="voucher-subtext">{r.description || r.objective || "Chưa có mô tả"}</div>
                    </td>
                    <td>{dt(r.startDate)} - {dt(r.endDate)}</td>
                    <td>{r.voucherCount}</td>
                    <td>{r.redeemed}</td>
                    <td>{money(r.budgetUsed)} / {money(r.budgetLimit)}</td>
                    <td>{num(r.budgetUsagePercent, 0)}%</td>
                    <td><span className={`badge ${budgetAlertClass(r)}`}>{budgetAlertText(r)}</span></td>
                    <td><span className={`badge ${STATUS_BADGE[up(r.status)] || "badge-inactive"}`}>{statusText(r.status)}</span></td>
                    <td>
                      <div className="row-actions voucher-actions-col">
                        <button className="btn-icon" onClick={() => openCampaignDetail(r)} disabled={campaignDetailLoading}>Chi tiết</button>
                        <button className="btn-icon" onClick={() => openEditCampaign(r)} disabled={campaignDetailLoading}>Sửa</button>
                        <button className="btn-icon" onClick={() => removeCampaign(r)}>Xóa</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div style={{ display: "flex", justifyContent: "center" }}>
            <TablePagination
              totalItems={campaignTotal}
              currentPage={campaignPage}
              pageSize={campaignPageSize}
              onPageChange={setCampaignPage}
              onPageSizeChange={(size) => {
                setCampaignPageSize(size);
                setCampaignPage(1);
              }}
            />
          </div>
        </>
      ) : null}
      {isConflicts ? (
        <div className="voucher-card-grid">
          <div className="voucher-panel-card">
            <h3>So sánh voucher</h3>
            <p className="voucher-subtext">Chọn 2 voucher để kiểm tra xung đột cơ bản về mã, cộng dồn và thời gian.</p>
            <input className="search-input" value={confQuery} onChange={(e) => setConfQuery(e.target.value)} placeholder="Tìm voucher..." />
            <div className="voucher-form-grid voucher-form-grid--2">
              <select value={confA} onChange={(e) => selectConflict(e.target.value, setConfA)} disabled={confLoading}><option value="">Voucher A</option>{conflictOptions.map((v) => <option key={v.id} value={v.id}>#{v.id} - {v.code}</option>)}</select>
              <select value={confB} onChange={(e) => selectConflict(e.target.value, setConfB)} disabled={confLoading}><option value="">Voucher B</option>{conflictOptions.map((v) => <option key={v.id} value={v.id}>#{v.id} - {v.code}</option>)}</select>
            </div>
            <TablePagination totalItems={confTotal} currentPage={confPage} pageSize={confPageSize} onPageChange={setConfPage} onPageSizeChange={(size) => { setConfPageSize(size); setConfPage(1); }} />
            <div className="voucher-finding-list">
              {confResult.length === 0 ? <div className="voucher-finding">Chưa có kết quả. Vui lòng chọn 2 voucher.</div> : confResult.map((x, i) => <div key={`${x}-${i}`} className="voucher-finding voucher-finding-warning">{x}</div>)}
            </div>
          </div>
          <div className="voucher-panel-card">
            <h3>Hương dẫn xử lý</h3>
            <p className="voucher-subtext">1. Nếu trùng mã hoặc không cộng dồn được thì chặn ngay.</p>
            <p className="voucher-subtext">2. Nếu trùng thời gian thì cần thêm rule xung đột ở backend.</p>
            <p className="voucher-subtext">3. Nếu voucher % cộng với voucher % thì cần giới hạn trần tổng.</p>
          </div>
        </div>
      ) : null}

      {isRedemptions ? (
        <>
          <div className="table-toolbar voucher-toolbar">
            <input className="search-input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm lịch sử sử dụng..." />
            <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới lịch sử"}</button>
          </div>
          <div className="table-wrapper">
            <table className="admin-table">
              <thead><tr><th>ID</th><th>Voucher</th><th>Khách hàng</th><th>Giảm giá</th><th>Trạng thái</th><th>Ngày tạo</th></tr></thead>
              <tbody>
                {redemptionLoading ? <tr><td colSpan={6}>Đang tải dữ liệu voucher...</td></tr> : redemptionRows.length === 0 ? <tr><td colSpan={6}><EmptyState message="Không có lịch sử sử dụng" /></td></tr> : redemptionRows.map((r) => (
                  <tr key={r.id}><td>#{r.id}</td><td>{r.voucherCode || `#${r.voucherId}`}</td><td>{r.customerId || "-"}</td><td>{money(r.discountAmount)}</td><td><span className={`badge ${STATUS_BADGE[up(r.redemptionStatus)] || "badge-inactive"}`}>{statusText(r.redemptionStatus)}</span></td><td>{dt(r.createdAt || r.redeemedAt)}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
          <div style={{ display: "flex", justifyContent: "center" }}>
            <TablePagination
              totalItems={redemptionTotal}
              currentPage={redemptionPage}
              pageSize={redemptionPageSize}
              onPageChange={setRedemptionPage}
              onPageSizeChange={(size) => {
                setRedemptionPageSize(size);
                setRedemptionPage(1);
              }}
            />
          </div>
        </>
      ) : null}

      {isApprovals ? (
        <>
          <div className="voucher-inline-form">
            <input value={approvalBy} onChange={(e) => setApprovalBy(e.target.value)} placeholder="ID người duyệt" />
            <input value={approvalNote} onChange={(e) => setApprovalNote(e.target.value)} placeholder="Ghi chú/Lý do" />
          </div>
          <div className="table-toolbar voucher-toolbar">
            <input className="search-input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm yêu cầu phê duyệt..." />
            <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới phê duyệt"}</button>
          </div>
          <div className="table-wrapper">
            <table className="admin-table">
              <thead><tr><th>ID</th><th>Loại yêu cầu</th><th>Voucher</th><th>Mức rủi ro</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
              <tbody>
                {approvalLoading ? <tr><td colSpan={6}>Đang tải dữ liệu voucher...</td></tr> : approvalRows.length === 0 ? <tr><td colSpan={6}><EmptyState message="Không có yêu cầu phê duyệt" /></td></tr> : approvalRows.map((a) => (
                  <tr key={a.id}><td>#{a.id}</td><td>{a.requestType}</td><td>{a.voucherId ? `#${a.voucherId}` : "-"}</td><td>{a.riskLevel || "-"}</td><td><span className={`badge ${STATUS_BADGE[up(a.status)] || "badge-inactive"}`}>{statusText(a.status)}</span></td><td>{up(a.status) === "PENDING" ? <div className="row-actions"><button className="btn-icon" onClick={() => decide(a, "approve")}>Duyệt</button><button className="btn-icon" onClick={() => decide(a, "reject")}>Từ chối</button></div> : "Đã xử lý"}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
          <div style={{ display: "flex", justifyContent: "center" }}>
            <TablePagination
              totalItems={approvalTotal}
              currentPage={approvalPage}
              pageSize={approvalPageSize}
              onPageChange={setApprovalPage}
              onPageSizeChange={(size) => {
                setApprovalPageSize(size);
                setApprovalPage(1);
              }}
            />
          </div>
        </>
      ) : null}

      {isReports ? (
        <>
          <div className="voucher-card-grid">
            <div className="voucher-panel-card"><h3>Tỷ lệ sử dụng</h3><p><strong>{pct(reportStats.redeemed, reportStats.issued)}%</strong></p></div>
            <div className="voucher-panel-card"><h3>Tổng giá trị giảm</h3><p><strong>{money(reportStats.discount)}</strong></p></div>
            <div className="voucher-panel-card"><h3>Tỷ lệ dùng ngân sách</h3><p><strong>{pct(reportStats.budgetUsed, reportStats.budgetLimit)}%</strong></p></div>
          </div>
          <div className="table-toolbar voucher-toolbar">
            <button className="btn-ghost" onClick={refresh} disabled={busy}>{busy ? "Đang làm mới..." : "Làm mới báo cáo"}</button>
          </div>
          <div className="table-wrapper">
            <table className="admin-table">
              <thead><tr><th>Voucher</th><th>Mã</th><th>Đã phát hành</th><th>Đã sử dụng</th><th>Tỷ lệ</th><th>Ngân sách đã dùng</th></tr></thead>
              <tbody>
                {reportLoading ? <tr><td colSpan={6}>Đang tải dữ liệu voucher...</td></tr> : reportVouchers.length === 0 ? <tr><td colSpan={6}><EmptyState message="Không có dữ liệu báo cáo voucher" /></td></tr> : reportVouchers.map((v) => (
                  <tr key={v.id}><td>{v.voucherName || "-"}</td><td>{v.code || "-"}</td><td>{v.issued}</td><td>{v.redeemed}</td><td>{pct(v.redeemed, v.issued)}%</td><td>{money(v.budgetUsed)}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
          <div style={{ display: "flex", justifyContent: "center" }}>
            <TablePagination
              totalItems={reportVoucherTotal}
              currentPage={reportVoucherPage}
              pageSize={reportVoucherPageSize}
              onPageChange={setReportVoucherPage}
              onPageSizeChange={(size) => {
                setReportVoucherPageSize(size);
                setReportVoucherPage(1);
              }}
            />
          </div>
          <div className="table-wrapper">
            <table className="admin-table">
              <thead><tr><th>ID sử dụng</th><th>Voucher</th><th>Khách hàng</th><th>Giảm giá</th><th>Trạng thái</th><th>Ngày tạo</th></tr></thead>
              <tbody>
                {reportLoading ? <tr><td colSpan={6}>Đang tải dữ liệu voucher...</td></tr> : reportRedemptions.length === 0 ? <tr><td colSpan={6}><EmptyState message="Không có dữ liệu lịch sử sử dụng" /></td></tr> : reportRedemptions.map((r) => (
                  <tr key={r.id}><td>#{r.id}</td><td>{r.voucherCode || `#${r.voucherId}`}</td><td>{r.customerId || "-"}</td><td>{money(r.discountAmount)}</td><td>{statusText(r.redemptionStatus)}</td><td>{dt(r.createdAt || r.redeemedAt)}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
          <div style={{ display: "flex", justifyContent: "center" }}>
            <TablePagination
              totalItems={reportRedemptionTotal}
              currentPage={reportRedemptionPage}
              pageSize={reportRedemptionPageSize}
              onPageChange={setReportRedemptionPage}
              onPageSizeChange={(size) => {
                setReportRedemptionPageSize(size);
                setReportRedemptionPage(1);
              }}
            />
          </div>
        </>
      ) : null}

      {isSettings ? (
        <div className="voucher-card-grid">
          <div className="voucher-panel-card">
            <h3>Chính sách</h3>
            <div className="voucher-settings-policy-grid">
              <label className="voucher-toggle-item voucher-settings-policy-item">
                <span>Bắt buộc trần giảm cho voucher phần trểm</span>
                <input
                  type="checkbox"
                  checked={policy.requireCapPercent}
                  onChange={(e) => setPolicy((p) => ({ ...p, requireCapPercent: e.target.checked }))}
                />
              </label>
              <label className="voucher-toggle-item voucher-settings-policy-item">
                <span>Chặn cộng dồn theo mặc định</span>
                <input
                  type="checkbox"
                  checked={policy.blockStacking}
                  onChange={(e) => setPolicy((p) => ({ ...p, blockStacking: e.target.checked }))}
                />
              </label>
            </div>
            <div className="form-actions voucher-form-actions-left">
              <button className="btn-primary" onClick={() => localStorage.setItem("voucher_admin_policy", JSON.stringify(policy))}>
                Lưu chính sách
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {modalOpen ? (
        <Modal title={mode === "create" ? "Tạo voucher" : `Sửa voucher #${form.id}`} onClose={() => setModalOpen(false)}>
          <form className="admin-form" onSubmit={submit}>
            <div className="voucher-form-grid voucher-form-grid--2">
              <input value={form.code} onChange={(e) => setForm((p) => ({ ...p, code: e.target.value }))} placeholder="Mã voucher *" required />
              <input value={form.voucherName} onChange={(e) => setForm((p) => ({ ...p, voucherName: e.target.value }))} placeholder="Tên voucher *" required />
              <input value={form.type} onChange={(e) => setForm((p) => ({ ...p, type: e.target.value }))} placeholder="Loại voucher" />
              <input value={form.value} onChange={(e) => setForm((p) => ({ ...p, value: e.target.value }))} placeholder="Giá trị" />
              <input value={form.minOrderValue} onChange={(e) => setForm((p) => ({ ...p, minOrderValue: e.target.value }))} placeholder="Hóa đơn tđi thiểu" />
              <input value={form.maxDiscountAmount} onChange={(e) => setForm((p) => ({ ...p, maxDiscountAmount: e.target.value }))} placeholder="Giảm tđi đa" />
              <input type="datetime-local" value={form.startDate} onChange={(e) => setForm((p) => ({ ...p, startDate: e.target.value }))} />
              <input type="datetime-local" value={form.endDate} onChange={(e) => setForm((p) => ({ ...p, endDate: e.target.value }))} />
              <input value={form.usageLimit} onChange={(e) => setForm((p) => ({ ...p, usageLimit: e.target.value }))} placeholder="Giới hạn lượt dùng" />
              <input value={form.budgetLimit} onChange={(e) => setForm((p) => ({ ...p, budgetLimit: e.target.value }))} placeholder="Ngân sách tối đa" />
            </div>
            <textarea rows={3} value={form.description} onChange={(e) => setForm((p) => ({ ...p, description: e.target.value }))} placeholder="Mô tả" />
            <div className="voucher-toggle-grid">
              <label className="voucher-check-item"><input type="checkbox" checked={form.isStackable} onChange={(e) => setForm((p) => ({ ...p, isStackable: e.target.checked }))} /><span>Cho phép cộng dồn</span></label>
              <label className="voucher-check-item"><input type="checkbox" checked={form.approvalRequired} onChange={(e) => setForm((p) => ({ ...p, approvalRequired: e.target.checked }))} /><span>Yêu cầu phê duyệt</span></label>
              <label className="voucher-check-item"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm((p) => ({ ...p, isActive: e.target.checked }))} /><span>Kích hoạt</span></label>
            </div>
            <div className="form-actions"><button type="button" className="btn-ghost" onClick={() => setModalOpen(false)}>Hủy</button><button type="submit" className="btn-primary" disabled={saving}>{saving ? "Đang lưu..." : "Lưu thay đổi"}</button></div>
          </form>
        </Modal>
      ) : null}

      {campaignModalOpen ? (
        <Modal title={campaignMode === "create" ? "Tạo chiến dịch" : `Sửa chiến dịch #${campaignForm.id}`} onClose={() => setCampaignModalOpen(false)}>
          <form className="admin-form" onSubmit={submitCampaign}>
            <div className="voucher-form-grid voucher-form-grid--2">
              <input value={campaignForm.campaignCode} onChange={(e) => setCampaignForm((p) => ({ ...p, campaignCode: e.target.value }))} placeholder="Mã chiến dịch (không bắt buộc)" />
              <input value={campaignForm.name} onChange={(e) => setCampaignForm((p) => ({ ...p, name: e.target.value }))} placeholder="Tên chiến dịch *" required />
              <input value={campaignForm.objective} onChange={(e) => setCampaignForm((p) => ({ ...p, objective: e.target.value }))} placeholder="Mục tiêu chiến dịch" />
              <input value={campaignForm.ownerStaffId} onChange={(e) => setCampaignForm((p) => ({ ...p, ownerStaffId: e.target.value }))} placeholder="ID quản lý chiến dịch" />
              <input type="datetime-local" value={campaignForm.startDate} onChange={(e) => setCampaignForm((p) => ({ ...p, startDate: e.target.value }))} />
              <input type="datetime-local" value={campaignForm.endDate} onChange={(e) => setCampaignForm((p) => ({ ...p, endDate: e.target.value }))} />
              <input value={campaignForm.budgetLimit} onChange={(e) => setCampaignForm((p) => ({ ...p, budgetLimit: e.target.value }))} placeholder="Ngân sách giới hạn (VND)" />
              <select value={campaignForm.status} onChange={(e) => setCampaignForm((p) => ({ ...p, status: e.target.value }))}>
                <option value="">Tự động theo thời gian</option>
                <option value="DRAFT">Nháp</option>
                <option value="PENDING_APPROVAL">Chờ duyệt</option>
                <option value="SCHEDULED">Đã lên lịch</option>
                <option value="ACTIVE">Đang hoạt động</option>
                <option value="PAUSED">Tạm dừng</option>
                <option value="ENDED">Đã kết thúc</option>
                <option value="ARCHIVED">Lưu trữ</option>
              </select>
            </div>

            <textarea rows={3} value={campaignForm.description} onChange={(e) => setCampaignForm((p) => ({ ...p, description: e.target.value }))} placeholder="Mô tả chiến dịch" />

            <div className="voucher-toggle-grid">
              <label className="voucher-check-item"><input type="checkbox" checked={campaignForm.isActive} onChange={(e) => setCampaignForm((p) => ({ ...p, isActive: e.target.checked }))} /><span>Chiến dịch đang hoạt động</span></label>
              <label className="voucher-check-item"><input type="checkbox" checked={campaignForm.replaceVoucherScope} onChange={(e) => setCampaignForm((p) => ({ ...p, replaceVoucherScope: e.target.checked }))} /><span>Thay toàn bộ danh sách voucher áp dụng</span></label>
            </div>

            <div className="voucher-campaign-voucher-box">
              <div className="voucher-campaign-voucher-head">
                <h3>Voucher được phép áp dụng trong chiến dịch</h3>
                <span>{campaignForm.voucherIds.length} voucher đã chọn</span>
              </div>
              <input className="search-input" value={campaignVoucherQuery} onChange={(e) => { setCampaignVoucherQuery(e.target.value); setCampaignVoucherPage(1); }} placeholder="Tìm voucher..." />
              {campaignVoucherError ? <div className="alert alert-error">{campaignVoucherError}</div> : null}
              <div className="voucher-campaign-voucher-list">
                {campaignVoucherLoading ? (
                  <div className="voucher-subtext">Đang tải dữ liệu voucher...</div>
                ) : campaignVoucherRows.length === 0 ? (
                  <div className="voucher-subtext">Chưa có voucher để gán.</div>
                ) : campaignVoucherRows.map((voucher) => (
                  <label key={voucher.id} className="voucher-campaign-voucher-item">
                    <input
                      type="checkbox"
                      checked={(campaignForm.voucherIds || []).includes(Number(voucher.id))}
                      onChange={() => toggleCampaignVoucher(voucher.id)}
                    />
                    <span className="voucher-campaign-voucher-code">{voucher.code || `#${voucher.id}`}</span>
                    <span>{voucher.voucherName || "Chưa đặt tên"}</span>
                  </label>
                ))}
              </div>
            </div>

            <TablePagination totalItems={campaignVoucherTotal} currentPage={campaignVoucherPage} pageSize={campaignVoucherPageSize} onPageChange={setCampaignVoucherPage} onPageSizeChange={(size) => { setCampaignVoucherPageSize(size); setCampaignVoucherPage(1); }} />
            <div className="form-actions">
              <button type="button" className="btn-ghost" onClick={() => setCampaignModalOpen(false)}>Hủy</button>
              <button type="submit" className="btn-primary" disabled={campaignSaving || campaignVoucherLoading || Boolean(campaignVoucherError)}>{campaignSaving ? "Đang lưu..." : "Lưu chiến dịch"}</button>
            </div>
          </form>
        </Modal>
      ) : null}

      {selectedCampaign ? (
        <Modal title={`Chi tiết chiến dịch #${selectedCampaign.id}`} onClose={() => setSelectedCampaign(null)}>
          <div className="voucher-detail-grid">
            <div><strong>Mã chiến dịch:</strong> {selectedCampaign.campaignCode || "-"}</div>
            <div><strong>Tên:</strong> {selectedCampaign.name || "-"}</div>
            <div><strong>Mục tiêu:</strong> {selectedCampaign.objective || "-"}</div>
            <div><strong>Mô tả:</strong> {selectedCampaign.description || "-"}</div>
            <div><strong>Thời gian:</strong> {dt(selectedCampaign.startDate)} - {dt(selectedCampaign.endDate)}</div>
            <div><strong>Trạng thái:</strong> {statusText(selectedCampaign.status)}</div>
            <div><strong>Voucher áp dụng:</strong> {selectedCampaign.voucherCount || 0}</div>
            <div><strong>Ngân sách đã dùng:</strong> {money(selectedCampaign.budgetUsed)} / {money(selectedCampaign.budgetLimit)}</div>
            <div><strong>Tỷ lệ ngân sách:</strong> {num(selectedCampaign.budgetUsagePercent, 0)}%</div>
            <div><strong>Cảnh báo:</strong> {budgetAlertText(selectedCampaign)}</div>
          </div>
          <div className="voucher-campaign-detail-vouchers">
            <strong>Danh sách voucher:</strong>
            {campaignDetailPageError ? <div className="alert alert-error">{campaignDetailPageError}</div> : null}
            {campaignDetailPageLoading ? <p className="voucher-subtext">Đang tải dữ liệu voucher...</p> : campaignDetailPageError ? null : campaignDetailVouchers.length > 0 ? (
              <div className="voucher-campaign-tags">
                {campaignDetailVouchers.map((v) => (
                  <span className="voucher-campaign-tag" key={v.id}>{v.code || `#${v.id}`} - {v.voucherName || "Chưa đặt tên"}</span>
                ))}
              </div>
            ) : (
              <p className="voucher-subtext">Chưa có voucher nào gắn vào chiến dịch này.</p>
            )}
            <TablePagination totalItems={campaignDetailTotal} currentPage={campaignDetailPage} pageSize={campaignDetailPageSize} onPageChange={setCampaignDetailPage} onPageSizeChange={(size) => { setCampaignDetailPageSize(size); setCampaignDetailPage(1); }} />
          </div>
        </Modal>
      ) : null}

      {selected ? (
        <Modal title={`Chi tiết voucher #${selected.id}`} onClose={() => setSelected(null)}>
          <div className="voucher-detail-grid">
            <div><strong>M?:</strong> {selected.code || "-"}</div>
            <div><strong>Tên:</strong> {selected.voucherName || "-"}</div>
            <div><strong>Loại:</strong> {selected.type || "-"}</div>
            <div><strong>Giá trị:</strong> {selected.type === "PERCENT" ? `${selected.value || 0}%` : money(selected.value)}</div>
            <div><strong>Trạng thái:</strong> {statusText(selected.status)}</div>
            <div><strong>Thời gian:</strong> {dt(selected.startDate)} - {dt(selected.endDate)}</div>
            <div><strong>Hạn mức:</strong> {num(selected.redeemedCount, num(selected.usedCount, 0))}/{num(selected.issuedCount, num(selected.usageLimit, 0))}</div>
            <div><strong>Ngân sách:</strong> {money(selected.budgetUsed)} / {money(selected.budgetLimit)}</div>
          </div>
        </Modal>
      ) : null}
    </div>
  );
}









