
import { useCallback, useEffect, useMemo, useState } from "react";
import { inventoryApi } from "../api/inventoryApi";
import "../../admin/pages/AdminPage.css";
import "./InventoryAdminPage.css";

const SECTION_META = {
  warehouseStructure: {
    title: "Quản lý cấu trúc kho",
    subtitle: "Tạo/sửa/khóa kho, cấu hình loại kho, bin/location và kho mặc định",
    hint: "Bắt đầu bằng việc tạo kho theo từng chi nhánh, sau đó gán kho mặc định cho từng loại hàng."
  },
  catalog: {
    title: "Quản lý danh mục hàng hóa",
    subtitle: "Sản phẩm, danh mục, nhà cung cấp, đơn vị tính, phân loại hàng và import/export",
    hint: "Mọi vật tư mới nên được chuẩn hóa mã SKU và danh mục trước khi đưa vào vận hành."
  },
  policy: {
    title: "Cấu hình chính sách tồn kho",
    subtitle: "Min/Max, ngưỡng cảnh báo, âm kho, reserve, vượt định mức, hoàn trả, chuyển kho",
    hint: "Thiết lập chính sách chung trước, sau đó điều chỉnh tồn an toàn riêng cho từng chi nhánh."
  },
  monitoring: {
    title: "Giám sát tồn kho toàn hệ thống",
    subtitle: "Theo chi nhánh, kho, sản phẩm, hàng sắp hết, tồn chậm, reserved, giá trị tồn",
    hint: "Theo dõi các chỉ số tổng quan hàng ngày để phát hiện sớm thiếu hàng hoặc lệch tồn."
  },
  audits: {
    title: "Quản lý và duyệt kiểm kê",
    subtitle: "Theo dõi kỳ kiểm kê, chênh lệch và duyệt điều chỉnh sau kiểm kê",
    hint: "Ưu tiên xử lý các phiếu có chênh lệch lớn trước để giảm rủi ro sai lệch tồn kho."
  },
  transactions: {
    title: "Lịch sử nhập xuất và truy vết",
    subtitle: "Truy vết đầy đủ theo chi nhánh/kho/sản phẩm/loại giao dịch và chứng từ nguồn",
    hint: "Dùng bộ lọc theo loại giao dịch để kiểm tra nhanh các luồng nhập/xuất bất thường."
  },
  approvals: {
    title: "Phê duyệt ngoại lệ kho",
    subtitle: "Duyệt các case xuất vượt, điều chỉnh tồn, hoàn trả bất thường, kho lỗi",
    hint: "Chỉ duyệt ngoại lệ khi có đủ chứng từ và ghi chú rõ nguyên nhân."
  },
  alerts: {
    title: "Cảnh báo và xử lý block",
    subtitle: "Trung tâm cảnh báo tồn thấp, âm kho, chứng từ treo, giao dịch bất thường",
    hint: "Xử lý cảnh báo mức cao trước để tránh ảnh hưởng đến hoàn tất sửa chữa và xuất hàng."
  },
  reports: {
    title: "Báo cáo quản trị kho",
    subtitle: "Báo cáo tồn kho, giá trị, luân chuyển, top xuất/hoàn, chênh lệch kiểm kê và audit log",
    hint: "Có thể dùng báo cáo này để đối soát nội bộ và chuẩn bị hồ sơ kiểm toán."
  }
};

const ITEM_TYPE_OPTIONS = [
  { value: "SELLABLE", label: "Bán được" },
  { value: "DEFECTIVE", label: "Lỗi/Hỏng" },
  { value: "CONSUMABLE", label: "Tiêu hao" },
  { value: "TRANSIT", label: "Trung chuyển" }
];

const OPERATION_OPTIONS = [
  { value: "PURCHASE_RECEIVE", label: "Nhập mua hàng" },
  { value: "SERVICE_ISSUE", label: "Xuất cho lệnh sửa chữa" },
  { value: "RETAIL_ISSUE", label: "Xuất bán lẻ" },
  { value: "MATERIAL_RETURN", label: "Hoàn trả vật tư" },
  { value: "TRANSFER", label: "Điều chuyển kho" }
];

const RESERVE_RULE_OPTIONS = [
  { value: "STRICT", label: "Nghiêm ngặt (giữ đúng số lượng đã reserve)" },
  { value: "FLEXIBLE", label: "Linh hoạt (cho phép điều phối khi thiếu)" }
];

const RETURN_RULE_OPTIONS = [
  { value: "ALLOW_WITH_REASON", label: "Cho phép, bắt buộc ghi lý do" },
  { value: "REQUIRE_APPROVAL", label: "Phải duyệt trước khi hoàn" },
  { value: "BLOCK_IF_MISSING_INFO", label: "Chặn nếu thiếu thông tin" }
];

const TRANSFER_RULE_OPTIONS = [
  { value: "REQUIRE_APPROVAL", label: "Phải duyệt khi chuyển kho" },
  { value: "AUTO_APPROVE_INTERNAL", label: "Tự duyệt trong nội bộ chi nhánh" },
  { value: "BLOCK_NEGATIVE", label: "Chặn nếu gây âm kho" }
];

const APPROVAL_STATUS_META = {
  PENDING: { label: "Chờ duyệt", className: "badge-suspended" },
  APPROVED: { label: "Đã duyệt", className: "badge-active" },
  REJECTED: { label: "Từ chối", className: "badge-deleted" }
};

const AUDIT_STATUS_META = {
  DRAFT: { label: "Nháp", className: "badge-inactive" },
  PENDING: { label: "Chờ duyệt", className: "badge-suspended" },
  APPROVED: { label: "Đã duyệt", className: "badge-active" },
  REJECTED: { label: "Từ chối", className: "badge-deleted" },
  CLOSED: { label: "Đã chốt", className: "badge-active" }
};

const ALERT_SEVERITY_META = {
  CRITICAL: { label: "Nghiêm trọng", className: "badge-deleted" },
  HIGH: { label: "Cao", className: "badge-suspended" },
  MEDIUM: { label: "Trung bình", className: "badge-inactive" },
  LOW: { label: "Thấp", className: "badge-inactive" }
};

const SOURCE_LABELS = {
  PurchaseOrder: "Đơn mua hàng",
  RetailOrder: "Đơn bán lẻ",
  RetailReturn: "Trả hàng bán lẻ",
  MaterialRequest: "Phiếu yêu cầu vật tư",
  MaterialReturn: "Phiếu hoàn vật tư",
  Unknown: "Không rõ nguồn"
};

const TRANSACTION_TYPE_LABELS = {
  PURCHASE_RECEIVE: "Nhập mua hàng",
  MATERIAL_RETURN: "Hoàn trả vật tư",
  SERVICE_ISSUE: "Xuất cho lệnh sửa chữa",
  RETAIL_ISSUE: "Xuất bán lẻ",
  TRANSFER: "Điều chuyển kho",
  IN: "Nhập kho",
  OUT: "Xuất kho",
  RETURN: "Hoàn trả"
};

const APPROVAL_TYPE_LABELS = {
  OVER_ISSUE: "Xuất vượt định mức",
  OVER_SERVICE_ORDER_QTY: "Xuất vượt số lượng lệnh sửa chữa",
  AUDIT_ADJUSTMENT: "Điều chỉnh tồn sau kiểm kê",
  DEFECTIVE_RETURN: "Nhập lại hàng lỗi",
  DEFECTIVE_TRANSFER: "Chuyển hàng sang kho lỗi",
  DAMAGE_BY_STAFF: "Hàng hỏng do nhân viên",
  ABNORMAL_RETURN: "Hoàn trả bất thường",
  UNBLOCK_CASE: "Mở khóa case bị block"
};

const ALERT_TYPE_LABELS = {
  LOW_STOCK: "Sắp hết hàng",
  NEGATIVE_STOCK: "Âm kho",
  TX_NO_SOURCE: "Giao dịch thiếu chứng từ",
  RETURN_PENDING: "Phiếu hoàn trả treo",
  ISSUE_BLOCKED: "Phiếu xuất bị treo",
  AUDIT_GAP: "Chênh lệch kiểm kê",
  DEFECTIVE_MISMATCH: "Kho lỗi chưa đồng bộ"
};

const ITEM_TYPE_LABEL_MAP = Object.fromEntries(ITEM_TYPE_OPTIONS.map((opt) => [opt.value, opt.label]));
const OPERATION_LABEL_MAP = Object.fromEntries(OPERATION_OPTIONS.map((opt) => [opt.value, opt.label]));

function normalizeKey(value) {
  return String(value || "").trim().toUpperCase();
}

function toNumber(value, fallback = 0) {
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

function csvEscape(value) {
  const str = String(value ?? "");
  if (!str.includes(",") && !str.includes("\"") && !str.includes("\n")) return str;
  return `"${str.replace(/"/g, '""')}"`;
}

function normalizeBranchStatus(status) {
  const normalized = normalizeKey(status);
  if (["ACTIVE", "ENABLED", "OPEN", "HOAT_DONG", "HOẠT_ĐỘNG"].includes(normalized)) return "Hoạt động";
  if (["INACTIVE", "DISABLED", "CLOSED", "DONG", "ĐÓNG"].includes(normalized)) return "Ngừng hoạt động";
  return status || "Chưa cấu hình";
}

function normalizeBranchStatusClass(status) {
  const normalized = normalizeKey(status);
  if (["ACTIVE", "ENABLED", "OPEN", "HOAT_DONG", "HOẠT_ĐỘNG"].includes(normalized)) return "is-active";
  if (["INACTIVE", "DISABLED", "CLOSED", "DONG", "ĐÓNG"].includes(normalized)) return "is-inactive";
  return "is-unknown";
}

function friendlyType(type) {
  return ITEM_TYPE_LABEL_MAP[normalizeKey(type)] || type || "-";
}

function friendlyOperation(operation) {
  return OPERATION_LABEL_MAP[normalizeKey(operation)] || operation || "-";
}

function friendlyTransactionType(type) {
  const normalized = normalizeKey(type);
  if (!normalized) return "-";
  if (TRANSACTION_TYPE_LABELS[normalized]) return TRANSACTION_TYPE_LABELS[normalized];
  return normalized.replace(/_/g, " ");
}

function friendlySource(source) {
  return SOURCE_LABELS[source] || source || "-";
}

function friendlyApprovalType(type) {
  const normalized = normalizeKey(type);
  if (!normalized) return "-";
  return APPROVAL_TYPE_LABELS[normalized] || normalized.replace(/_/g, " ");
}

function friendlyAlertType(type) {
  const normalized = normalizeKey(type);
  if (!normalized) return "-";
  return ALERT_TYPE_LABELS[normalized] || normalized.replace(/_/g, " ");
}

function resolveStatusMeta(status, mapping) {
  const normalized = normalizeKey(status);
  const meta = mapping[normalized];
  if (meta) return meta;
  return { label: status || "Chưa rõ", className: "badge-inactive" };
}

function SectionHeader({ section }) {
  const meta = SECTION_META[section] || {
    title: "Quản trị kho",
    subtitle: "Theo dõi và cấu hình nghiệp vụ kho",
    hint: "Chọn menu bên trái để thao tác."
  };
  return (
    <header className="admin-header">
      <div>
        <h1>{meta.title}</h1>
        <p className="header-sub">{meta.subtitle}</p>
        <div className="inventory-header-tip">{meta.hint}</div>
      </div>
    </header>
  );
}

function StatCards({ items }) {
  return (
    <div className="stats-bar">
      {items.map((item) => (
        <div className="stat-card" key={item.label}>
          <div className="stat-value">{item.value}</div>
          <div className="stat-label">{item.label}</div>
        </div>
      ))}
    </div>
  );
}

function ActionPanel({ title, description, children }) {
  return (
    <section className="inventory-panel">
      <div className="inventory-panel-head">
        <h3>{title}</h3>
        {description ? <p>{description}</p> : null}
      </div>
      <div className="inventory-panel-body">{children}</div>
    </section>
  );
}

function NoDataRow({ colSpan, message }) {
  return (
    <tr>
      <td colSpan={colSpan}>
        <div className="inventory-empty">{message}</div>
      </td>
    </tr>
  );
}

export default function InventoryAdminPage({ section }) {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [overview, setOverview] = useState(null);
  const [branchSummary, setBranchSummary] = useState([]);
  const [warehouseStructure, setWarehouseStructure] = useState({ warehouses: [], bins: [], branches: [] });
  const [settings, setSettings] = useState(null);

  const [products, setProducts] = useState([]);
  const [categories, setCategories] = useState([]);
  const [suppliers, setSuppliers] = useState([]);
  const [productImages, setProductImages] = useState([]);

  const [audits, setAudits] = useState([]);
  const [approvals, setApprovals] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [reports, setReports] = useState(null);

  const [txParams, setTxParams] = useState({
    page: 1,
    pageSize: 30,
    branchId: "",
    warehouseId: "",
    type: "",
    from: "",
    to: ""
  });
  const [transactions, setTransactions] = useState({ items: [], total: 0, page: 1, pageSize: 30 });

  const [form, setForm] = useState({
    newWarehouseCode: "",
    newWarehouseName: "",
    newWarehouseType: "SELLABLE",
    newWarehouseBranchId: "",
    productName: "",
    productSku: "",
    productUnit: "Cái",
    productCategoryId: "",
    supplierName: "",
    categoryName: ""
  });

  const runAction = async (work, fallbackMessage = "Không thể thực hiện thao tác.") => {
    setError("");
    try {
      await work();
    } catch (err) {
      setError(err?.message || fallbackMessage);
    }
  };

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      if (section === "warehouseStructure") {
        const [structure, branchData, cfg] = await Promise.all([
          inventoryApi.getWarehouseStructure(),
          inventoryApi.getBranches(),
          inventoryApi.getSettings()
        ]);
        setWarehouseStructure({
          warehouses: structure?.warehouses || [],
          bins: structure?.bins || [],
          branches: branchData || []
        });
        setSettings(cfg);
      }

      if (section === "catalog") {
        const [prods, cats, sups, imgs, cfg, structure, branchData] = await Promise.all([
          inventoryApi.getProducts(),
          inventoryApi.getPartCategories(),
          inventoryApi.getSuppliers(),
          inventoryApi.getProductImages(),
          inventoryApi.getSettings(),
          inventoryApi.getWarehouseStructure(),
          inventoryApi.getBranches()
        ]);
        setProducts(prods || []);
        setCategories(cats || []);
        setSuppliers(sups || []);
        setProductImages(imgs || []);
        setSettings(cfg);
        setWarehouseStructure({
          warehouses: structure?.warehouses || [],
          bins: structure?.bins || [],
          branches: branchData || []
        });
      }

      if (section === "policy") {
        const [cfg, branches] = await Promise.all([
          inventoryApi.getSettings(),
          inventoryApi.getBranchSummary()
        ]);
        const branchRows = branches || [];
        const threshold = toNumber(cfg?.policyRules?.lowStockThresholdPercent, 15);
        const safeStockByBranch = { ...(cfg?.policyRules?.safeStockByBranch || {}) };
        branchRows.forEach((branch) => {
          if (safeStockByBranch[branch.branchId] == null) {
            safeStockByBranch[branch.branchId] = Math.max(
              0,
              Math.ceil(toNumber(branch.totalQuantity, 0) * threshold / 100)
            );
          }
        });
        setSettings({
          ...(cfg || {}),
          policyRules: {
            ...(cfg?.policyRules || {}),
            safeStockByBranch
          }
        });
        setBranchSummary(branchRows);
      }

      if (section === "monitoring") {
        const [ov, branches] = await Promise.all([
          inventoryApi.getOverview(),
          inventoryApi.getBranchSummary()
        ]);
        setOverview(ov);
        setBranchSummary(branches || []);
      }

      if (section === "audits") {
        const res = await inventoryApi.getAudits();
        setAudits(res || []);
      }

      if (section === "transactions") {
        const query = {
          page: txParams.page,
          pageSize: txParams.pageSize
        };
        if (txParams.branchId) query.branchId = Number(txParams.branchId);
        if (txParams.warehouseId) query.warehouseId = Number(txParams.warehouseId);
        if (txParams.type?.trim()) query.type = txParams.type.trim();
        if (txParams.from) query.from = txParams.from;
        if (txParams.to) query.to = txParams.to;

        const [res, structure, branchData] = await Promise.all([
          inventoryApi.getTransactions(query),
          inventoryApi.getWarehouseStructure(),
          inventoryApi.getBranches()
        ]);
        setTransactions(res || { items: [], total: 0, page: 1, pageSize: 30 });
        setWarehouseStructure({
          warehouses: structure?.warehouses || [],
          bins: structure?.bins || [],
          branches: branchData || []
        });
      }

      if (section === "approvals") {
        const res = await inventoryApi.getApprovals();
        setApprovals(res || []);
      }

      if (section === "alerts") {
        const res = await inventoryApi.getAlerts();
        setAlerts(res || []);
      }

      if (section === "reports") {
        const [rep, logs] = await Promise.all([
          inventoryApi.getReports(),
          inventoryApi.getAuditLogs(300)
        ]);
        setReports({ ...(rep || {}), auditLogs: logs || [] });
      }
    } catch (err) {
      setError(err?.message || "Không tải được dữ liệu quản trị kho.");
    } finally {
      setLoading(false);
    }
  }, [section, txParams]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const categoryMap = useMemo(() => {
    const map = new Map();
    (categories || []).forEach((item) => map.set(item.id, item.categoryName));
    return map;
  }, [categories]);

  const imagesByProduct = useMemo(() => {
    const map = new Map();
    (productImages || []).forEach((item) => {
      if (!map.has(item.productId)) map.set(item.productId, []);
      map.get(item.productId).push(item);
    });
    return map;
  }, [productImages]);

  const policyOverview = useMemo(() => {
    const branchCount = branchSummary.length;
    const activeBranchCount = branchSummary.filter((b) => normalizeBranchStatusClass(b.branchStatus) === "is-active").length;
    const totalCurrent = branchSummary.reduce((sum, b) => sum + toNumber(b.totalQuantity, 0), 0);
    const totalReserved = branchSummary.reduce((sum, b) => sum + toNumber(b.reservedQuantity, 0), 0);
    const totalSafeStock = branchSummary.reduce(
      (sum, b) => sum + toNumber(settings?.policyRules?.safeStockByBranch?.[b.branchId], 0),
      0
    );
    return {
      branchCount,
      activeBranchCount,
      totalCurrent,
      totalReserved,
      totalSafeStock
    };
  }, [branchSummary, settings]);

  const txTotalPages = useMemo(() => {
    const total = toNumber(transactions.total, 0);
    const size = toNumber(transactions.pageSize || txParams.pageSize, 30);
    return Math.max(1, Math.ceil(total / Math.max(1, size)));
  }, [transactions, txParams.pageSize]);

  const warehouseOptionsByBranch = useMemo(() => {
    if (!txParams.branchId) return warehouseStructure.warehouses || [];
    return (warehouseStructure.warehouses || []).filter((w) => String(w.branchId) === String(txParams.branchId));
  }, [warehouseStructure.warehouses, txParams.branchId]);

  const updateForm = (name, value) => setForm((prev) => ({ ...prev, [name]: value }));

  const saveSettings = async (nextSettings) => {
    const saved = await inventoryApi.saveSettings(nextSettings);
    setSettings(saved);
  };

  const handleCreateWarehouse = async () => {
    const code = form.newWarehouseCode.trim();
    const name = form.newWarehouseName.trim();
    if (!code || !name || !form.newWarehouseBranchId) return;
    await inventoryApi.createWarehouse({
      branchId: Number(form.newWarehouseBranchId),
      code,
      name,
      type: form.newWarehouseType,
      isActive: true
    });
    setForm((prev) => ({ ...prev, newWarehouseCode: "", newWarehouseName: "" }));
    await fetchData();
  };

  const handleToggleWarehouse = async (warehouse) => {
    await inventoryApi.updateWarehouse(warehouse.id, {
      ...warehouse,
      branchId: warehouse.branchId,
      isActive: !warehouse.isActive
    });
    await fetchData();
  };

  const handleCreateCategory = async () => {
    const categoryName = form.categoryName.trim();
    if (!categoryName) return;
    await inventoryApi.createPartCategory({ categoryName, description: "", isDepositRequired: false });
    setForm((prev) => ({ ...prev, categoryName: "" }));
    await fetchData();
  };

  const handleCreateSupplier = async () => {
    const supplierName = form.supplierName.trim();
    if (!supplierName) return;
    await inventoryApi.createSupplier({ supplierName, currentDebt: 0 });
    setForm((prev) => ({ ...prev, supplierName: "" }));
    await fetchData();
  };

  const handleCreateProduct = async () => {
    const productName = form.productName.trim();
    const productSku = form.productSku.trim();
    if (!productName || !productSku) return;
    await inventoryApi.createProduct({
      name: productName,
      sku: productSku,
      unit: form.productUnit || "Cái",
      categoryId: form.productCategoryId ? Number(form.productCategoryId) : null,
      retailPrice: 0,
      costPrice: 0,
      servicePrice: 0,
      taxRate: 0,
      warrantyMonths: 0,
      isActive: true
    });
    setForm((prev) => ({ ...prev, productName: "", productSku: "" }));
    await fetchData();
  };

  const markDiscontinued = async (product) => {
    await inventoryApi.updateProduct(product.id, { ...product, isActive: false });
    await fetchData();
  };

  const exportProductsCsv = () => {
    const header = ["id", "sku", "name", "unit", "category", "isActive"];
    const rows = (products || []).map((p) => [p.id, p.sku, p.name, p.unit, categoryMap.get(p.categoryId) || "", p.isActive]);
    const csv = [header, ...rows].map((row) => row.map(csvEscape).join(",")).join("\n");
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `danh-muc-kho-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const importProductsCsv = async (file) => {
    const text = await file.text();
    const lines = text.split(/\r?\n/).filter(Boolean);
    if (lines.length <= 1) return;
    for (let i = 1; i < lines.length; i += 1) {
      const [sku, name, unit] = lines[i].split(",").map((s) => s.trim());
      if (!sku || !name) continue;
      await inventoryApi.createProduct({
        sku,
        name,
        unit: unit || "Cái",
        retailPrice: 0,
        costPrice: 0,
        servicePrice: 0,
        taxRate: 0,
        warrantyMonths: 0,
        isActive: true
      });
    }
    await fetchData();
  };

  const updatePolicyField = (field, value) => {
    if (!settings) return;
    setSettings((prev) => ({
      ...prev,
      policyRules: {
        ...prev.policyRules,
        [field]: value
      }
    }));
  };

  const updateSafeStockBranch = (branchId, value) => {
    if (!settings) return;
    setSettings((prev) => ({
      ...prev,
      policyRules: {
        ...prev.policyRules,
        safeStockByBranch: {
          ...(prev.policyRules?.safeStockByBranch || {}),
          [branchId]: toNumber(value, 0)
        }
      }
    }));
  };

  const handleApproveAudit = async (id) => {
    await inventoryApi.approveAudit(id, {});
    await fetchData();
  };

  const handleRejectAudit = async (id) => {
    await inventoryApi.rejectAudit(id, { note: "Từ chối bởi quản trị viên" });
    await fetchData();
  };

  const handleApproveRequest = async (id) => {
    await inventoryApi.approveRequest(id, {});
    await fetchData();
  };

  const handleRejectRequest = async (id) => {
    await inventoryApi.rejectRequest(id, { note: "Từ chối bởi quản trị viên" });
    await fetchData();
  };

  const handleTxFilterChange = (name, value) => {
    setTxParams((prev) => ({ ...prev, [name]: value, page: 1 }));
  };

  const resetTxFilters = () => {
    setTxParams((prev) => ({
      ...prev,
      page: 1,
      branchId: "",
      warehouseId: "",
      type: "",
      from: "",
      to: ""
    }));
  };

  const changePage = (delta) => {
    setTxParams((prev) => {
      const next = Math.max(1, prev.page + delta);
      return { ...prev, page: next };
    });
  };

  return (
    <div className="management-content inventory-admin-page">
      <SectionHeader section={section} />
      {error ? <div className="alert alert-error">{error}</div> : null}

      {loading ? (
        <div className="loading-state">
          <div className="spinner" />
          <p>Đang tải dữ liệu kho...</p>
        </div>
      ) : (
        <>
          {section === "warehouseStructure" ? (
            <>
              <StatCards
                items={[
                  { label: "Tổng số kho", value: warehouseStructure.warehouses.length },
                  { label: "Số bin", value: warehouseStructure.bins.length },
                  { label: "Kho đang hoạt động", value: warehouseStructure.warehouses.filter((w) => w.isActive).length }
                ]}
              />
              <ActionPanel
                title="Tạo kho mới"
                description="Nhập thông tin cơ bản để thêm kho mới theo từng chi nhánh."
              >
                <div className="table-toolbar inventory-form-grid">
                  <label className="compact-field">
                    <span>Mã kho</span>
                    <input
                      className="search-input"
                      placeholder="Ví dụ: MAIN-WH"
                      value={form.newWarehouseCode}
                      onChange={(e) => updateForm("newWarehouseCode", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Tên kho</span>
                    <input
                      className="search-input"
                      placeholder="Ví dụ: Kho phụ tùng chính"
                      value={form.newWarehouseName}
                      onChange={(e) => updateForm("newWarehouseName", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Loại kho</span>
                    <select
                      className="search-input"
                      value={form.newWarehouseType}
                      onChange={(e) => updateForm("newWarehouseType", e.target.value)}
                    >
                      {ITEM_TYPE_OPTIONS.map((type) => (
                        <option key={type.value} value={type.value}>{type.label}</option>
                      ))}
                    </select>
                  </label>
                  <label className="compact-field">
                    <span>Chi nhánh</span>
                    <select
                      className="search-input"
                      value={form.newWarehouseBranchId}
                      onChange={(e) => updateForm("newWarehouseBranchId", e.target.value)}
                    >
                      <option value="">Chọn chi nhánh</option>
                      {(warehouseStructure.branches || []).map((b) => (
                        <option key={b.id} value={b.id}>{b.name}</option>
                      ))}
                    </select>
                  </label>
                  <button className="btn-primary" onClick={() => runAction(handleCreateWarehouse)}>
                    + Tạo kho
                  </button>
                </div>
              </ActionPanel>

              {settings ? (
                <ActionPanel
                  title="Gán kho mặc định theo loại hàng"
                  description="Hệ thống sẽ tự động ưu tiên kho này khi phát sinh nghiệp vụ tương ứng."
                >
                  <div className="table-toolbar inventory-form-grid">
                    {ITEM_TYPE_OPTIONS.map((type) => (
                      <label key={type.value} className="compact-field">
                        <span>{type.label}</span>
                        <select
                          className="search-input"
                          value={settings.defaultWarehouseByItemType?.[type.value] ?? 0}
                          onChange={(e) => {
                            const next = {
                              ...settings,
                              defaultWarehouseByItemType: {
                                ...(settings.defaultWarehouseByItemType || {}),
                                [type.value]: Number(e.target.value)
                              }
                            };
                            setSettings(next);
                          }}
                        >
                          <option value={0}>-- Chưa chọn --</option>
                          {warehouseStructure.warehouses.map((w) => (
                            <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
                          ))}
                        </select>
                      </label>
                    ))}
                    <button className="btn-primary" onClick={() => runAction(() => saveSettings(settings))}>
                      Lưu kho mặc định
                    </button>
                  </div>
                </ActionPanel>
              ) : null}

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Mã kho</th>
                      <th>Tên kho</th>
                      <th>Loại kho</th>
                      <th>Chi nhánh</th>
                      <th>Trạng thái</th>
                      <th>Số bin</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {warehouseStructure.warehouses.length === 0 ? (
                      <NoDataRow colSpan={8} message="Chưa có kho nào. Hãy tạo kho đầu tiên để bắt đầu cấu hình." />
                    ) : (
                      warehouseStructure.warehouses.map((w) => (
                        <tr key={w.id}>
                          <td className="td-id">#{w.id}</td>
                          <td>{w.code}</td>
                          <td>{w.name}</td>
                          <td>
                            <span className="badge badge-inactive">{friendlyType(w.type)}</span>
                          </td>
                          <td>{w.branchName}</td>
                          <td>
                            <span className={`badge ${w.isActive ? "badge-active" : "badge-deleted"}`}>
                              {w.isActive ? "Đang hoạt động" : "Đã khóa"}
                            </span>
                          </td>
                          <td>{warehouseStructure.bins.filter((b) => b.warehouseId === w.id).length}</td>
                          <td>
                            <button className="btn-icon" onClick={() => runAction(() => handleToggleWarehouse(w))}>
                              {w.isActive ? "Khóa kho" : "Bật kho"}
                            </button>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </>
          ) : null}

          {section === "catalog" ? (
            <>
              <StatCards
                items={[
                  { label: "Tổng sản phẩm", value: products.length },
                  { label: "Danh mục", value: categories.length },
                  { label: "Nhà cung cấp", value: suppliers.length }
                ]}
              />

              <ActionPanel
                title="Thêm sản phẩm mới"
                description="Khai báo nhanh sản phẩm để đưa vào danh mục dùng chung toàn hệ thống."
              >
                <div className="table-toolbar inventory-form-grid">
                  <label className="compact-field">
                    <span>Tên sản phẩm</span>
                    <input
                      className="search-input"
                      placeholder="Ví dụ: Lọc nhớt Honda"
                      value={form.productName}
                      onChange={(e) => updateForm("productName", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Mã SKU</span>
                    <input
                      className="search-input"
                      placeholder="Ví dụ: SKU-0001"
                      value={form.productSku}
                      onChange={(e) => updateForm("productSku", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Đơn vị tính</span>
                    <input
                      className="search-input"
                      placeholder="Ví dụ: Cái"
                      value={form.productUnit}
                      onChange={(e) => updateForm("productUnit", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Danh mục</span>
                    <select
                      className="search-input"
                      value={form.productCategoryId}
                      onChange={(e) => updateForm("productCategoryId", e.target.value)}
                    >
                      <option value="">Chọn danh mục</option>
                      {categories.map((c) => (
                        <option key={c.id} value={c.id}>{c.categoryName}</option>
                      ))}
                    </select>
                  </label>
                  <button className="btn-primary" onClick={() => runAction(handleCreateProduct)}>
                    + Tạo sản phẩm
                  </button>
                </div>
                <div className="table-toolbar inventory-form-actions">
                  <button className="btn-ghost" onClick={exportProductsCsv}>Xuất CSV danh mục</button>
                  <label className="btn-ghost file-label">
                    Nhập CSV danh mục
                    <input
                      type="file"
                      accept=".csv"
                      onChange={(e) => {
                        const file = e.target.files?.[0];
                        if (file) runAction(() => importProductsCsv(file));
                        e.target.value = "";
                      }}
                    />
                  </label>
                </div>
              </ActionPanel>

              <ActionPanel
                title="Danh mục và nhà cung cấp"
                description="Tạo nhanh nhóm danh mục và nhà cung cấp trước khi thêm sản phẩm hàng loạt."
              >
                <div className="table-toolbar inventory-form-grid inventory-form-grid--small">
                  <label className="compact-field">
                    <span>Tên danh mục</span>
                    <input
                      className="search-input"
                      placeholder="Ví dụ: Dầu nhớt"
                      value={form.categoryName}
                      onChange={(e) => updateForm("categoryName", e.target.value)}
                    />
                  </label>
                  <button className="btn-ghost" onClick={() => runAction(handleCreateCategory)}>
                    + Tạo danh mục
                  </button>
                  <label className="compact-field">
                    <span>Tên nhà cung cấp</span>
                    <input
                      className="search-input"
                      placeholder="Ví dụ: Nhà cung cấp A"
                      value={form.supplierName}
                      onChange={(e) => updateForm("supplierName", e.target.value)}
                    />
                  </label>
                  <button className="btn-ghost" onClick={() => runAction(handleCreateSupplier)}>
                    + Tạo nhà cung cấp
                  </button>
                </div>
              </ActionPanel>

              {settings ? (
                <ActionPanel
                  title="Kho mặc định theo nghiệp vụ"
                  description="Dùng để tự động chọn kho nguồn khi phát sinh từng loại nghiệp vụ."
                >
                  <div className="table-toolbar inventory-form-grid">
                    {OPERATION_OPTIONS.map((op) => (
                      <label key={op.value} className="compact-field">
                        <span>{friendlyOperation(op.value)}</span>
                        <select
                          className="search-input"
                          value={settings.defaultWarehouseByOperation?.[op.value] ?? 0}
                          onChange={(e) => setSettings((prev) => ({
                            ...prev,
                            defaultWarehouseByOperation: {
                              ...(prev.defaultWarehouseByOperation || {}),
                              [op.value]: Number(e.target.value)
                            }
                          }))}
                        >
                          <option value={0}>-- Chưa chọn --</option>
                          {(warehouseStructure.warehouses || []).map((w) => (
                            <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
                          ))}
                        </select>
                      </label>
                    ))}
                    <button className="btn-primary" onClick={() => runAction(() => saveSettings(settings))}>
                      Lưu thiết lập nghiệp vụ
                    </button>
                  </div>
                </ActionPanel>
              ) : null}

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>SKU</th>
                      <th>Tên sản phẩm</th>
                      <th>Đơn vị</th>
                      <th>Danh mục</th>
                      <th>Số ảnh</th>
                      <th>Trạng thái</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {products.length === 0 ? (
                      <NoDataRow colSpan={8} message="Chưa có sản phẩm trong danh mục kho." />
                    ) : (
                      products.map((p) => (
                        <tr key={p.id}>
                          <td className="td-id">#{p.id}</td>
                          <td>{p.sku}</td>
                          <td>{p.name}</td>
                          <td>{p.unit}</td>
                          <td>{categoryMap.get(p.categoryId) || "-"}</td>
                          <td>{(imagesByProduct.get(p.id) || []).length}</td>
                          <td>
                            <span className={`badge ${p.isActive ? "badge-active" : "badge-deleted"}`}>
                              {p.isActive ? "Đang kinh doanh" : "Ngừng kinh doanh"}
                            </span>
                          </td>
                          <td>
                            {p.isActive ? (
                              <button className="btn-icon" onClick={() => runAction(() => markDiscontinued(p))}>
                                Ngừng kinh doanh
                              </button>
                            ) : (
                              <span className="result-count">Đã ngừng</span>
                            )}
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </>
          ) : null}

          {section === "policy" && settings ? (
            <>
              <div className="policy-card">
                <div className="policy-card-head">
                  <h3>Thiết lập chính sách chung</h3>
                  <p>Áp dụng cho toàn hệ thống kho. Cấu hình này ảnh hưởng đến cảnh báo và phê duyệt.</p>
                </div>
                <div className="policy-grid">
                  <label className="compact-field policy-toggle">
                    <span>Cho phép âm kho</span>
                    <input
                      type="checkbox"
                      checked={!!settings.policyRules?.allowNegativeStock}
                      onChange={(e) => updatePolicyField("allowNegativeStock", e.target.checked)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Ngưỡng cảnh báo tồn thấp (%)</span>
                    <input
                      className="search-input"
                      type="number"
                      min={0}
                      max={100}
                      value={settings.policyRules?.lowStockThresholdPercent ?? 0}
                      onChange={(e) => updatePolicyField("lowStockThresholdPercent", Math.min(100, Math.max(0, toNumber(e.target.value, 0))))}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Quy tắc giữ hàng (Reserve)</span>
                    <select
                      className="search-input"
                      value={settings.policyRules?.reserveRule || "STRICT"}
                      onChange={(e) => updatePolicyField("reserveRule", e.target.value)}
                    >
                      {RESERVE_RULE_OPTIONS.map((opt) => (
                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                      ))}
                    </select>
                  </label>
                  <label className="compact-field policy-toggle">
                    <span>Cho phép xuất vượt định mức</span>
                    <input
                      type="checkbox"
                      checked={!!settings.policyRules?.allowOverIssue}
                      onChange={(e) => updatePolicyField("allowOverIssue", e.target.checked)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Quy tắc hoàn trả vật tư</span>
                    <select
                      className="search-input"
                      value={settings.policyRules?.materialReturnRule || "ALLOW_WITH_REASON"}
                      onChange={(e) => updatePolicyField("materialReturnRule", e.target.value)}
                    >
                      {RETURN_RULE_OPTIONS.map((opt) => (
                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                      ))}
                    </select>
                  </label>
                  <label className="compact-field">
                    <span>Quy tắc chuyển kho</span>
                    <select
                      className="search-input"
                      value={settings.policyRules?.transferRule || "REQUIRE_APPROVAL"}
                      onChange={(e) => updatePolicyField("transferRule", e.target.value)}
                    >
                      {TRANSFER_RULE_OPTIONS.map((opt) => (
                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                      ))}
                    </select>
                  </label>
                </div>
              </div>

              <div className="policy-overview">
                <div className="policy-overview-item"><span>Tổng chi nhánh</span><strong>{policyOverview.branchCount}</strong></div>
                <div className="policy-overview-item"><span>Chi nhánh hoạt động</span><strong>{policyOverview.activeBranchCount}</strong></div>
                <div className="policy-overview-item"><span>Tồn hiện tại</span><strong>{policyOverview.totalCurrent.toLocaleString()}</strong></div>
                <div className="policy-overview-item"><span>Đã giữ chỗ</span><strong>{policyOverview.totalReserved.toLocaleString()}</strong></div>
                <div className="policy-overview-item"><span>Tổng tồn an toàn</span><strong>{policyOverview.totalSafeStock.toLocaleString()}</strong></div>
              </div>

              <div className="policy-card">
                <div className="policy-card-head">
                  <h3>Cấu hình tồn an toàn theo chi nhánh</h3>
                  <p>Đặt mức tồn tối thiểu riêng cho từng chi nhánh để hệ thống cảnh báo sát thực tế vận hành.</p>
                </div>
                <div className="table-wrapper policy-table-wrapper">
                  <table className="admin-table">
                    <thead>
                      <tr>
                        <th>Chi nhánh</th>
                        <th>Trạng thái</th>
                        <th>Số kho</th>
                        <th>Tồn hiện tại</th>
                        <th>Đã giữ chỗ</th>
                        <th>Tồn an toàn</th>
                      </tr>
                    </thead>
                    <tbody>
                      {branchSummary.length === 0 ? (
                        <NoDataRow colSpan={6} message="Chưa có dữ liệu chi nhánh để cấu hình tồn an toàn." />
                      ) : (
                        branchSummary.map((b) => (
                          <tr key={b.branchId}>
                            <td>{b.branchName}</td>
                            <td>
                              <span className={`policy-status ${normalizeBranchStatusClass(b.branchStatus)}`}>
                                {normalizeBranchStatus(b.branchStatus)}
                              </span>
                            </td>
                            <td>{toNumber(b.warehouseCount).toLocaleString()}</td>
                            <td>{toNumber(b.totalQuantity).toLocaleString()}</td>
                            <td>{toNumber(b.reservedQuantity).toLocaleString()}</td>
                            <td>
                              <input
                                className="search-input policy-stock-input"
                                type="number"
                                min={0}
                                value={settings.policyRules?.safeStockByBranch?.[b.branchId] ?? 0}
                                onChange={(e) => updateSafeStockBranch(b.branchId, e.target.value)}
                              />
                            </td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </div>

              <div className="table-toolbar policy-savebar">
                <button className="btn-primary" onClick={() => runAction(() => saveSettings(settings))}>
                  Lưu chính sách
                </button>
              </div>
            </>
          ) : null}

          {section === "monitoring" && overview ? (
            <>
              <StatCards
                items={[
                  { label: "Tổng tồn", value: toNumber(overview.totalQuantity).toLocaleString() },
                  { label: "Đã giữ chỗ", value: toNumber(overview.reservedQuantity).toLocaleString() },
                  { label: "Sắp hết", value: toNumber(overview.lowStockCount).toLocaleString() },
                  { label: "Âm kho", value: toNumber(overview.negativeStockCount).toLocaleString() },
                  { label: "Kho lỗi", value: toNumber(overview.defectiveStockCount).toLocaleString() },
                  { label: "Giá trị tồn", value: toNumber(overview.totalInventoryValue).toLocaleString() }
                ]}
              />
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Chi nhánh</th>
                      <th>Tồn hiện tại</th>
                      <th>Đã giữ chỗ</th>
                      <th>Sắp hết</th>
                      <th>Âm kho</th>
                      <th>Giá trị tồn</th>
                    </tr>
                  </thead>
                  <tbody>
                    {branchSummary.length === 0 ? (
                      <NoDataRow colSpan={6} message="Chưa có dữ liệu giám sát tồn kho." />
                    ) : (
                      branchSummary.map((b) => (
                        <tr key={b.branchId}>
                          <td>{b.branchName}</td>
                          <td>{toNumber(b.totalQuantity).toLocaleString()}</td>
                          <td>{toNumber(b.reservedQuantity).toLocaleString()}</td>
                          <td>{toNumber(b.lowStockCount).toLocaleString()}</td>
                          <td>{toNumber(b.negativeStockCount).toLocaleString()}</td>
                          <td>{toNumber(b.totalValue).toLocaleString()}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </>
          ) : null}

          {section === "audits" ? (
            <div className="table-wrapper">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Ngày kiểm kê</th>
                    <th>Chi nhánh</th>
                    <th>Kho</th>
                    <th>Trạng thái</th>
                    <th>Tổng chênh lệch</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {audits.length === 0 ? (
                    <NoDataRow colSpan={7} message="Chưa có kỳ kiểm kê nào." />
                  ) : (
                    audits.map((a) => {
                      const status = resolveStatusMeta(a.status, AUDIT_STATUS_META);
                      const canAction = ["PENDING", "DRAFT"].includes(normalizeKey(a.status));
                      return (
                        <tr key={a.id}>
                          <td className="td-id">#{a.id}</td>
                          <td>{a.auditDate ? new Date(a.auditDate).toLocaleDateString() : "-"}</td>
                          <td>{a.branchName}</td>
                          <td>{a.warehouseName}</td>
                          <td><span className={`badge ${status.className}`}>{status.label}</span></td>
                          <td>{toNumber(a.differenceTotal).toLocaleString()}</td>
                          <td>
                            {canAction ? (
                              <div className="row-actions">
                                <button className="btn-icon" onClick={() => runAction(() => handleApproveAudit(a.id))}>Duyệt</button>
                                <button className="btn-icon" onClick={() => runAction(() => handleRejectAudit(a.id))}>Từ chối</button>
                              </div>
                            ) : (
                              <span className="result-count">Đã xử lý</span>
                            )}
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          ) : null}

          {section === "transactions" ? (
            <>
              <ActionPanel
                title="Bộ lọc truy vết giao dịch"
                description="Lọc theo chi nhánh, kho, loại giao dịch và khoảng thời gian để truy xuất nhanh."
              >
                <div className="table-toolbar inventory-form-grid inventory-form-grid--tx">
                  <label className="compact-field">
                    <span>Chi nhánh</span>
                    <select
                      className="search-input"
                      value={txParams.branchId}
                      onChange={(e) => {
                        handleTxFilterChange("branchId", e.target.value);
                        handleTxFilterChange("warehouseId", "");
                      }}
                    >
                      <option value="">Tất cả chi nhánh</option>
                      {(warehouseStructure.branches || []).map((b) => (
                        <option key={b.id} value={b.id}>{b.name}</option>
                      ))}
                    </select>
                  </label>
                  <label className="compact-field">
                    <span>Kho</span>
                    <select
                      className="search-input"
                      value={txParams.warehouseId}
                      onChange={(e) => handleTxFilterChange("warehouseId", e.target.value)}
                    >
                      <option value="">Tất cả kho</option>
                      {warehouseOptionsByBranch.map((w) => (
                        <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
                      ))}
                    </select>
                  </label>
                  <label className="compact-field">
                    <span>Loại giao dịch</span>
                    <input
                      className="search-input"
                      placeholder="Ví dụ: OUT, SERVICE_ISSUE, RETURN"
                      value={txParams.type}
                      onChange={(e) => handleTxFilterChange("type", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Từ ngày</span>
                    <input
                      className="search-input"
                      type="date"
                      value={txParams.from}
                      onChange={(e) => handleTxFilterChange("from", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Đến ngày</span>
                    <input
                      className="search-input"
                      type="date"
                      value={txParams.to}
                      onChange={(e) => handleTxFilterChange("to", e.target.value)}
                    />
                  </label>
                </div>
                <div className="table-toolbar inventory-form-actions">
                  <button className="btn-primary" onClick={fetchData}>Lọc dữ liệu</button>
                  <button className="btn-ghost" onClick={resetTxFilters}>Xóa bộ lọc</button>
                </div>
              </ActionPanel>

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Thời gian</th>
                      <th>Loại giao dịch</th>
                      <th>Chi nhánh</th>
                      <th>Kho</th>
                      <th>SKU</th>
                      <th>Số lượng</th>
                      <th>Nguồn phát sinh</th>
                      <th>Người thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(transactions.items || []).length === 0 ? (
                      <NoDataRow colSpan={9} message="Không có giao dịch nào khớp với bộ lọc hiện tại." />
                    ) : (
                      (transactions.items || []).map((t) => (
                        <tr key={t.id}>
                          <td className="td-id">#{t.id}</td>
                          <td>{t.createdAt ? new Date(t.createdAt).toLocaleString() : "-"}</td>
                          <td><span className="badge badge-inactive">{friendlyTransactionType(t.type)}</span></td>
                          <td>{t.branchName}</td>
                          <td>{t.warehouseName}</td>
                          <td>{t.productSku}</td>
                          <td>{toNumber(t.quantity).toLocaleString()}</td>
                          <td>{friendlySource(t.source)}</td>
                          <td>{t.createdByName || "-"}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>

              <div className="table-toolbar">
                <button className="btn-ghost" onClick={() => changePage(-1)} disabled={txParams.page <= 1}>
                  Trang trước
                </button>
                <span className="result-count">
                  Trang {txParams.page} / {txTotalPages} - Tổng {toNumber(transactions.total).toLocaleString()} giao dịch
                </span>
                <button className="btn-ghost" onClick={() => changePage(1)} disabled={txParams.page >= txTotalPages}>
                  Trang sau
                </button>
              </div>
            </>
          ) : null}

          {section === "approvals" ? (
            <div className="table-wrapper">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Loại yêu cầu</th>
                    <th>Trạng thái</th>
                    <th>Người gửi</th>
                    <th>Lý do</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {approvals.length === 0 ? (
                    <NoDataRow colSpan={6} message="Không có yêu cầu ngoại lệ nào." />
                  ) : (
                    approvals.map((a) => {
                      const status = resolveStatusMeta(a.status, APPROVAL_STATUS_META);
                      const canAction = normalizeKey(a.status) === "PENDING";
                      return (
                        <tr key={a.id}>
                          <td className="td-id">#{a.id}</td>
                          <td>{friendlyApprovalType(a.requestType)}</td>
                          <td><span className={`badge ${status.className}`}>{status.label}</span></td>
                          <td>{a.requestedByName}</td>
                          <td>{a.reason || "-"}</td>
                          <td>
                            {canAction ? (
                              <div className="row-actions">
                                <button className="btn-icon" onClick={() => runAction(() => handleApproveRequest(a.id))}>Duyệt</button>
                                <button className="btn-icon" onClick={() => runAction(() => handleRejectRequest(a.id))}>Từ chối</button>
                              </div>
                            ) : (
                              <span className="result-count">Đã xử lý</span>
                            )}
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          ) : null}

          {section === "alerts" ? (
            <div className="table-wrapper">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Mức độ</th>
                    <th>Loại cảnh báo</th>
                    <th>Nội dung</th>
                    <th>Chi nhánh</th>
                    <th>Kho</th>
                    <th>Thời gian</th>
                  </tr>
                </thead>
                <tbody>
                  {alerts.length === 0 ? (
                    <NoDataRow colSpan={6} message="Hiện chưa có cảnh báo tồn kho." />
                  ) : (
                    alerts.map((a, idx) => {
                      const severity = resolveStatusMeta(a.severity, ALERT_SEVERITY_META);
                      return (
                        <tr key={`${a.referenceType}-${a.referenceId}-${idx}`}>
                          <td><span className={`badge ${severity.className}`}>{severity.label}</span></td>
                          <td>{friendlyAlertType(a.type)}</td>
                          <td>{a.message}</td>
                          <td>{a.branchName || "-"}</td>
                          <td>{a.warehouseName || "-"}</td>
                          <td>{a.createdAt ? new Date(a.createdAt).toLocaleString() : "-"}</td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          ) : null}

          {section === "reports" && reports ? (
            <>
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Chi nhánh</th>
                      <th>Tổng tồn</th>
                      <th>Giá trị tồn</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(reports.stockByBranch || []).length === 0 ? (
                      <NoDataRow colSpan={3} message="Chưa có dữ liệu tồn kho theo chi nhánh." />
                    ) : (
                      (reports.stockByBranch || []).map((r) => (
                        <tr key={r.branchId}>
                          <td>{r.branchName}</td>
                          <td>{toNumber(r.totalQuantity).toLocaleString()}</td>
                          <td>{toNumber(r.totalValue).toLocaleString()}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Top sản phẩm xuất nhiều</th>
                      <th>Số lượng xuất</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(reports.topOutbound || []).length === 0 ? (
                      <NoDataRow colSpan={2} message="Chưa có dữ liệu top xuất kho." />
                    ) : (
                      (reports.topOutbound || []).map((r) => (
                        <tr key={r.productId}>
                          <td>{r.sku} - {r.productName}</td>
                          <td>{toNumber(r.totalOutboundQty).toLocaleString()}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Hành động</th>
                      <th>Bảng dữ liệu</th>
                      <th>Bản ghi</th>
                      <th>Thời gian</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(reports.auditLogs || []).length === 0 ? (
                      <NoDataRow colSpan={5} message="Chưa có audit log liên quan đến kho." />
                    ) : (
                      (reports.auditLogs || []).map((log) => (
                        <tr key={log.id}>
                          <td className="td-id">#{log.id}</td>
                          <td>{log.action}</td>
                          <td>{log.tableName}</td>
                          <td>{log.recordId}</td>
                          <td>{log.timestamp ? new Date(log.timestamp).toLocaleString() : "-"}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </>
          ) : null}
        </>
      )}
    </div>
  );
}
