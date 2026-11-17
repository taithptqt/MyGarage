import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { voucherApi } from "@features/voucher/api/voucherApi";
import { getVoucherPage, getRedemptionPage, getApprovalPage, getVoucherReportPage, getCampaignPage } from "@features/voucher/api/voucherPagingApi";
import "../../admin/pages/AdminPage.css";
import "./VoucherManagementPage.css";

import {
  SECTION_NEEDS,
  campaignDefault,
  formDefault,
  mapCampaignForm,
  mapForm,
  nnull,
  num,
  rows,
  tnull,
  toIso,
  uid,
  up,
  withTimeout
} from "./voucherManagementHelpers";
import {
  VoucherApprovalsSection,
  VoucherCampaignsSection,
  VoucherConflictsSection,
  VoucherDashboardSection,
  VoucherListSection,
  VoucherRedemptionsSection,
  VoucherReportsSection,
  VoucherSettingsSection
} from "./VoucherManagementSections";
import {
  CampaignDetailModal,
  CampaignFormModal,
  VoucherDetailModal,
  VoucherFormModal
} from "./VoucherModals";

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
        <VoucherDashboardSection
          activeCount={activeCount}
          issued={issued}
          redeemed={redeemed}
          discount={discount}
          pendingApprovals={pendingApprovals}
          refresh={refresh}
          busy={busy}
          topVouchers={topVouchers}
          budgetUsed={budgetUsed}
          budgetLimit={budgetLimit}
          nearQuotaVouchers={nearQuotaVouchers}
          dashboardApprovals={dashboardApprovals}
        />
      ) : null}

      {isList ? (
        <VoucherListSection
          q={q}
          setQ={setQ}
          status={status}
          setStatus={setStatus}
          statusOptions={statusOptions}
          openCreate={openCreate}
          refresh={refresh}
          busy={busy}
          voucherLoading={voucherLoading}
          voucherError={voucherError}
          voucherRows={voucherRows}
          setSelected={setSelected}
          openEdit={openEdit}
          changeStatus={changeStatus}
          voucherTotal={voucherTotal}
          voucherPage={voucherPage}
          voucherPageSize={voucherPageSize}
          setVoucherPage={setVoucherPage}
          setVoucherPageSize={setVoucherPageSize}
        />
      ) : null}

      {isCampaigns ? (
        <VoucherCampaignsSection
          campaignStats={campaignStats}
          q={q}
          setQ={setQ}
          campaignFilterStatus={campaignFilterStatus}
          setCampaignFilterStatus={setCampaignFilterStatus}
          campaignStatusOptions={campaignStatusOptions}
          openCreateCampaign={openCreateCampaign}
          refresh={refresh}
          busy={busy}
          campaignWarnings={campaignWarnings}
          campaignLoading={campaignLoading}
          campaignPageRows={campaignPageRows}
          openCampaignDetail={openCampaignDetail}
          campaignDetailLoading={campaignDetailLoading}
          openEditCampaign={openEditCampaign}
          removeCampaign={removeCampaign}
          campaignTotal={campaignTotal}
          campaignPage={campaignPage}
          campaignPageSize={campaignPageSize}
          setCampaignPage={setCampaignPage}
          setCampaignPageSize={setCampaignPageSize}
        />
      ) : null}

      {isConflicts ? (
        <VoucherConflictsSection
          confQuery={confQuery}
          setConfQuery={setConfQuery}
          confA={confA}
          setConfA={setConfA}
          confB={confB}
          setConfB={setConfB}
          selectConflict={selectConflict}
          confLoading={confLoading}
          conflictOptions={conflictOptions}
          confTotal={confTotal}
          confPage={confPage}
          confPageSize={confPageSize}
          setConfPage={setConfPage}
          setConfPageSize={setConfPageSize}
          confResult={confResult}
        />
      ) : null}

      {isRedemptions ? (
        <VoucherRedemptionsSection
          q={q}
          setQ={setQ}
          refresh={refresh}
          busy={busy}
          redemptionLoading={redemptionLoading}
          redemptionRows={redemptionRows}
          redemptionTotal={redemptionTotal}
          redemptionPage={redemptionPage}
          redemptionPageSize={redemptionPageSize}
          setRedemptionPage={setRedemptionPage}
          setRedemptionPageSize={setRedemptionPageSize}
        />
      ) : null}

      {isApprovals ? (
        <VoucherApprovalsSection
          approvalBy={approvalBy}
          setApprovalBy={setApprovalBy}
          approvalNote={approvalNote}
          setApprovalNote={setApprovalNote}
          q={q}
          setQ={setQ}
          refresh={refresh}
          busy={busy}
          approvalLoading={approvalLoading}
          approvalRows={approvalRows}
          decide={decide}
          approvalTotal={approvalTotal}
          approvalPage={approvalPage}
          approvalPageSize={approvalPageSize}
          setApprovalPage={setApprovalPage}
          setApprovalPageSize={setApprovalPageSize}
        />
      ) : null}

      {isReports ? (
        <VoucherReportsSection
          reportStats={reportStats}
          refresh={refresh}
          busy={busy}
          reportLoading={reportLoading}
          reportVouchers={reportVouchers}
          reportVoucherTotal={reportVoucherTotal}
          reportVoucherPage={reportVoucherPage}
          reportVoucherPageSize={reportVoucherPageSize}
          setReportVoucherPage={setReportVoucherPage}
          setReportVoucherPageSize={setReportVoucherPageSize}
          reportRedemptions={reportRedemptions}
          reportRedemptionTotal={reportRedemptionTotal}
          reportRedemptionPage={reportRedemptionPage}
          reportRedemptionPageSize={reportRedemptionPageSize}
          setReportRedemptionPage={setReportRedemptionPage}
          setReportRedemptionPageSize={setReportRedemptionPageSize}
        />
      ) : null}

      {isSettings ? <VoucherSettingsSection policy={policy} setPolicy={setPolicy} /> : null}

      {modalOpen ? (
        <VoucherFormModal
          mode={mode}
          form={form}
          setForm={setForm}
          submit={submit}
          saving={saving}
          onClose={() => setModalOpen(false)}
        />
      ) : null}

      {campaignModalOpen ? (
        <CampaignFormModal
          campaignMode={campaignMode}
          campaignForm={campaignForm}
          setCampaignForm={setCampaignForm}
          submitCampaign={submitCampaign}
          campaignVoucherQuery={campaignVoucherQuery}
          setCampaignVoucherQuery={setCampaignVoucherQuery}
          setCampaignVoucherPage={setCampaignVoucherPage}
          campaignVoucherError={campaignVoucherError}
          campaignVoucherLoading={campaignVoucherLoading}
          campaignVoucherRows={campaignVoucherRows}
          toggleCampaignVoucher={toggleCampaignVoucher}
          campaignVoucherTotal={campaignVoucherTotal}
          campaignVoucherPage={campaignVoucherPage}
          campaignVoucherPageSize={campaignVoucherPageSize}
          setCampaignVoucherPageSize={setCampaignVoucherPageSize}
          campaignSaving={campaignSaving}
          onClose={() => setCampaignModalOpen(false)}
        />
      ) : null}

      {selectedCampaign ? (
        <CampaignDetailModal
          selectedCampaign={selectedCampaign}
          onClose={() => setSelectedCampaign(null)}
          campaignDetailPageError={campaignDetailPageError}
          campaignDetailPageLoading={campaignDetailPageLoading}
          campaignDetailVouchers={campaignDetailVouchers}
          campaignDetailTotal={campaignDetailTotal}
          campaignDetailPage={campaignDetailPage}
          campaignDetailPageSize={campaignDetailPageSize}
          setCampaignDetailPage={setCampaignDetailPage}
          setCampaignDetailPageSize={setCampaignDetailPageSize}
        />
      ) : null}

      {selected ? <VoucherDetailModal selected={selected} onClose={() => setSelected(null)} /> : null}
    </div>
  );
}









