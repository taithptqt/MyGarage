
import { useCallback, useEffect, useMemo, useState } from "react";
import { inventoryApi } from "@features/inventory/api/inventoryApi";
import { Modal } from "@features/admin/components/AdminModals";
import TablePagination from "@shared/components/TablePagination";
import StockAlertBanner from "@features/workshop/components/StockAlertBanner";
import "../../admin/pages/AdminPage.css";
import "./InventoryAdminPage.css";

const SECTION_META = {
  warehouseStructure: {
    title: "Quản lý c?u tr�c kho",
    subtitle: "T?o/s?a/kh�a kho, c?u h�nh lo?i kho, bin/location v� kho m?c d?nh",
    hint: "B?t d?u bảng vi?c t?o kho theo tổng chi nh?nh, sau d? g?n kho m?c d?nh cho tổng lo?i h?ng."
  },
  catalogProducts: {
    title: "Quản lý s?n ph?m kho",
    subtitle: "T?o/s?a s?n ph?m, qu?n l? SKU, don v? t?nh, h?nh ?nh v? tr?ng th?i kinh doanh",
    hint: "Chu?n h?a m? SKU v? danh m?c tru?c khi nh?p kho d? tr?nh tr?ng l?p dữ liệu."
  },
  catalogCategories: {
    title: "Quản lý danh m?c s?n ph?m",
    subtitle: "T?o/s?a nh?m h?ng d? ph?n lo?i vật tư r? r?ng v? d?ng nh?t to?n hệ thống",
    hint: "N�n t?o c?u tr�c danh m?c ng?n g?n, d? hi?u d? ngu?i m?i t�m h�ng nhanh hon."
  },
  catalogSuppliers: {
    title: "Quản lý nh� cung c?p",
    subtitle: "Luu th?ng tin nh? cung c?p ph?c v? truy xu?t ngu?n h?ng v? d?i so?t mua v?o",
    hint: "Chu?n t?n nh? cung c?p ngay t? d?u d? tr?nh tr?ng v? d? truy v?n l?ch s? giao d?ch."
  },
  catalog: {
    title: "Quản lý s?n ph?m kho",
    subtitle: "T?o/s?a s?n ph?m, qu?n l? SKU, don v? t?nh, h?nh ?nh v? tr?ng th?i kinh doanh",
    hint: "Chu?n h?a m? SKU v? danh m?c tru?c khi nh?p kho d? tr?nh tr?ng l?p dữ liệu."
  },
  policy: {
    title: "C?u h?nh chọnh s?ch t?n kho",
    subtitle: "Min/Max, ngu?ng cầnh b?o, ?m kho, reserve, vu?t d?nh m?c, ho?n tr?, chuy?n kho",
    hint: "Thi?t l?p chọnh s?ch chung tru?c, sau d? di?u chọnh t?n an to?n ri?ng cho tổng chi nh?nh."
  },
  monitoring: {
    title: "Gi?m s?t t?n kho to?n hệ thống",
    subtitle: "Theo chi nh?nh, kho, s?n ph?m, h?ng s?p h?t, t?n ch?m, reserved, gi? tr? t?n",
    hint: "Theo d?i c?c ch? s? tổng quan h?ng ng?y d? ph?t hi?n s?m thi?u h?ng ho?c l?ch t?n."
  },
  audits: {
    title: "Quản lý v� duy?t ki?m k�",
    subtitle: "Theo d�i k? ki?m k�, ch�nh l?ch v� duy?t di?u chọnh sau ki?m k�",
    hint: "Uu ti?n x? l? c?c phi?u c? chọnh l?ch l?n tru?c d? gi?m r?i ro sai l?ch t?n kho."
  },
  transactions: {
    title: "L?ch s? nh?p xu?t v� truy v?t",
    subtitle: "Truy v?t d?y d? theo chi nh?nh/kho/s?n ph?m/lo?i giao dịch vụ chọng t? ngu?n",
    hint: "D?ng b? l?c theo lo?i giao d?ch d? ki?m tra nhanh c?c lu?ng nh?p/xu?t b?t thu?ng."
  },
  approvals: {
    title: "Ph� duy?t ngo?i l? kho",
    subtitle: "Duy?t c?c case xu?t vu?t, di?u chọnh t?n, ho?n tr? b?t thu?ng, kho l?i",
    hint: "Ch? duy?t ngo?i l? khi c? d? chọng t? v? ghi ch? r? nguy?n nh?n."
  },
  alerts: {
    title: "Cầnh b?o v? x? l? block",
    subtitle: "Trung tìm cầnh b?o t?n th?p, ?m kho, chọng t? treo, giao d?ch b?t thu?ng",
    hint: "X? l? cầnh b?o m?c cao tru?c d? tr?nh ?nh hu?ng d?n ho?n t?t s?a ch?a v? xu?t h?ng."
  },
  reports: {
    title: "B?o c?o qu?n tr? kho",
    subtitle: "B?o c?o t?n kho, gi? tr?, lu?n chuy?n, top xu?t/ho?n, chọnh l?ch ki?m k? v? audit log",
    hint: "C? th? d?ng b?o c?o n?y d? d?i so?t n?i b? v? chu?n b? h? so ki?m to?n."
  }
};

const ITEM_TYPE_OPTIONS = [
  { value: "SELLABLE", label: "B?n du?c" },
  { value: "DEFECTIVE", label: "L?i/H?ng" },
  { value: "CONSUMABLE", label: "Ti�u hao" },
  { value: "TRANSIT", label: "Trung chuy?n" }
];

const EMPTY_PRODUCT_FORM = {
  sku: "",
  name: "",
  unit: "Cái",
  categoryId: "",
  costPrice: "",
  retailPrice: "",
  servicePrice: "",
  taxRate: "",
  warrantyMonths: "",
  isStrictCapacityControl: false,
  isActive: true
};

const EMPTY_CATEGORY_FORM = {
  categoryName: "",
  parentId: "",
  description: "",
  isDepositRequired: false
};

const RESERVE_RULE_OPTIONS = [
  { value: "STRICT", label: "Nghi�m ng?t (gi? d�ng s? lu?ng d? reserve)" },
  { value: "FLEXIBLE", label: "Linh ho?t (cho ph�p di?u ph?i khi thi?u)" }
];

const RETURN_RULE_OPTIONS = [
  { value: "ALLOW_WITH_REASON", label: "Cho ph�p, b?t bu?c ghi l� do" },
  { value: "REQUIRE_APPROVAL", label: "Ph?i duy?t tru?c khi ho�n" },
  { value: "BLOCK_IF_MISSING_INFO", label: "Chọn n?u thi?u thông tin" }
];

const TRANSFER_RULE_OPTIONS = [
  { value: "REQUIRE_APPROVAL", label: "Ph?i duy?t khi chuy?n kho" },
  { value: "AUTO_APPROVE_INTERNAL", label: "T? duy?t trong n?i b? chi nh?nh" },
  { value: "BLOCK_NEGATIVE", label: "Chọn n?u g?y ?m kho" }
];

const APPROVAL_STATUS_META = {
  PENDING: { label: "Ch? duy?t", className: "badge-suspended" },
  APPROVED: { label: "Đã duy?t", className: "badge-active" },
  REJECTED: { label: "T? ch?i", className: "badge-deleted" }
};

const AUDIT_STATUS_META = {
  DRAFT: { label: "Nháp", className: "badge-inactive" },
  PENDING: { label: "Ch? duy?t", className: "badge-suspended" },
  APPROVED: { label: "Đã duy?t", className: "badge-active" },
  REJECTED: { label: "T? ch?i", className: "badge-deleted" },
  CLOSED: { label: "Đã ch?t", className: "badge-active" }
};

const ALERT_SEVERITY_META = {
  CRITICAL: { label: "Nghi�m tr?ng", className: "badge-deleted" },
  HIGH: { label: "Cao", className: "badge-suspended" },
  MEDIUM: { label: "Trung b�nh", className: "badge-inactive" },
  LOW: { label: "Th?p", className: "badge-inactive" }
};

const SOURCE_LABELS = {
  PurchaseOrder: "Đơn mua h�ng",
  RetailOrder: "?on b?n l?",
  RetailReturn: "Tr? h?ng b?n l?",
  MaterialRequest: "Phi?u yêu cầu vật tư",
  MaterialReturn: "Phi?u ho�n vật tư",
  Unknown: "Không r� ngu?n"
};

const TRANSACTION_TYPE_LABELS = {
  PURCHASE_RECEIVE: "Nh?p mua h�ng",
  MATERIAL_RETURN: "Ho�n tr? vật tư",
  SERVICE_ISSUE: "Xu?t cho l?nh s?a ch?a",
  RETAIL_ISSUE: "Xu?t b?n l?",
  TRANSFER: "Điều chuy?n kho",
  IN: "Nh?p kho",
  OUT: "Xu?t kho",
  RETURN: "Ho�n tr?"
};

const APPROVAL_TYPE_LABELS = {
  OVER_ISSUE: "Xu?t vu?t d?nh m?c",
  OVER_SERVICE_ORDER_QTY: "Xu?t vu?t s? lu?ng l?nh s?a ch?a",
  AUDIT_ADJUSTMENT: "Điều chọnh t?n sau ki?m k�",
  DEFECTIVE_RETURN: "Nh?p l?i h�ng l?i",
  DEFECTIVE_TRANSFER: "Chuy?n h�ng sang kho l?i",
  DAMAGE_BY_STAFF: "H?ng h?ng do nh?n vi?n",
  ABNORMAL_RETURN: "Ho�n tr? b?t thu?ng",
  UNBLOCK_CASE: "M? kh�a case b? block"
};

const ALERT_TYPE_LABELS = {
  LOW_STOCK: "S?p h?t h�ng",
  NEGATIVE_STOCK: "?m kho",
  TX_NO_SOURCE: "Giao d?ch thi?u chọng t?",
  RETURN_PENDING: "Phi?u ho�n tr? treo",
  ISSUE_BLOCKED: "Phi?u xu?t b? treo",
  AUDIT_GAP: "Ch�nh l?ch ki?m k�",
  DEFECTIVE_MISMATCH: "Kho l?i chua d?ng b?"
};

const ITEM_TYPE_LABEL_MAP = Object.fromEntries(ITEM_TYPE_OPTIONS.map((opt) => [opt.value, opt.label]));
function normalizeKey(value) {
  return String(value || "").trim().toUpperCase();
}

function toNumber(value, fallback = 0) {
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

function toNullableNumber(value) {
  const raw = String(value ?? "").trim();
  if (!raw) return null;
  const parsed = Number(raw);
  if (!Number.isFinite(parsed)) return null;
  return parsed;
}

function csvEscape(value) {
  const str = String(value ?? "");
  if (!str.includes(",") && !str.includes("\"") && !str.includes("\n")) return str;
  return `"${str.replace(/"/g, '""')}"`;
}

function normalizeBranchStatus(status) {
  const normalized = normalizeKey(status);
  if (["ACTIVE", "ENABLED", "OPEN", "HOAT_DONG", "HO?T_ĐộNG"].includes(normalized)) return "Ho?t d?ng";
  if (["INACTIVE", "DISABLED", "CLOSED", "DONG", "ĐãNG"].includes(normalized)) return "Ng?ng ho?t d?ng";
  return status || "Chua c?u h�nh";
}

function normalizeBranchStatusClass(status) {
  const normalized = normalizeKey(status);
  if (["ACTIVE", "ENABLED", "OPEN", "HOAT_DONG", "HO?T_ĐộNG"].includes(normalized)) return "is-active";
  if (["INACTIVE", "DISABLED", "CLOSED", "DONG", "ĐãNG"].includes(normalized)) return "is-inactive";
  return "is-unknown";
}

function friendlyType(type) {
  return ITEM_TYPE_LABEL_MAP[normalizeKey(type)] || type || "-";
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
  return { label: status || "Chua r�", className: "badge-inactive" };
}

function SectionHeader({ section }) {
  const meta = SECTION_META[section] || {
    title: "Qu?n tr? kho",
    subtitle: "Theo d�i v� c?u h�nh nghi?p v? kho",
    hint: "Chọn menu b?n tr?i d? thao t?c."
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
  const [warehousePage, setWarehousePage] = useState(1);
  const [warehousePageSize, setWarehousePageSize] = useState(10);
  const [productPage, setProductPage] = useState(1);
  const [productPageSize, setProductPageSize] = useState(10);
  const [categoryPage, setCategoryPage] = useState(1);
  const [categoryPageSize, setCategoryPageSize] = useState(10);
  const [supplierPage, setSupplierPage] = useState(1);
  const [supplierPageSize, setSupplierPageSize] = useState(10);
  const [reportBranchPage, setReportBranchPage] = useState(1);
  const [reportBranchPageSize, setReportBranchPageSize] = useState(10);
  const [reportTopOutboundPage, setReportTopOutboundPage] = useState(1);
  const [reportTopOutboundPageSize, setReportTopOutboundPageSize] = useState(10);
  const [reportAuditLogPage, setReportAuditLogPage] = useState(1);
  const [reportAuditLogPageSize, setReportAuditLogPageSize] = useState(10);

  const [form, setForm] = useState({
    newWarehouseCode: "",
    newWarehouseName: "",
    newWarehouseType: "SELLABLE",
    newWarehouseBranchId: "",
    supplierName: ""
  });
  const [showCreateProductModal, setShowCreateProductModal] = useState(false);
  const [productModalMode, setProductModalMode] = useState("create");
  const [editingProduct, setEditingProduct] = useState(null);
  const [productForm, setProductForm] = useState(EMPTY_PRODUCT_FORM);
  const [savingProduct, setSavingProduct] = useState(false);
  const [productMainImageFile, setProductMainImageFile] = useState(null);
  const [productMainImagePreview, setProductMainImagePreview] = useState("");
  const [editProductImages, setEditProductImages] = useState([]);
  const [galleryBusy, setGalleryBusy] = useState(false);
  const [productThumbById, setProductThumbById] = useState({});
  const [showCategoryModal, setShowCategoryModal] = useState(false);
  const [categoryModalMode, setCategoryModalMode] = useState("create");
  const [editingCategory, setEditingCategory] = useState(null);
  const [categoryForm, setCategoryForm] = useState(EMPTY_CATEGORY_FORM);
  const [savingCategory, setSavingCategory] = useState(false);

  const isCatalogProductsSection = section === "catalogProducts" || section === "catalog";
  const isCatalogCategoriesSection = section === "catalogCategories";
  const isCatalogSuppliersSection = section === "catalogSuppliers";
  const isCatalogSection = isCatalogProductsSection || isCatalogCategoriesSection || isCatalogSuppliersSection;

  const runAction = async (work, fallbackMessage = "Kh?ng th? th?c hi?n thao t?c.") => {
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

      if (isCatalogSection) {
        const [prods, cats, sups, cfg, structure, branchData, allProductImages] = await Promise.all([
          inventoryApi.getProducts(),
          inventoryApi.getPartCategories(),
          inventoryApi.getSuppliers(),
          inventoryApi.getSettings(),
          inventoryApi.getWarehouseStructure(),
          inventoryApi.getBranches(),
          inventoryApi.getProductImages()
        ]);
        setProducts(prods || []);
        setCategories(cats || []);
        setSuppliers(sups || []);
        setSettings(cfg);
        setWarehouseStructure({
          warehouses: structure?.warehouses || [],
          bins: structure?.bins || [],
          branches: branchData || []
        });
        const thumbs = {};
        (allProductImages || []).forEach((img) => {
          const pid = img?.productId;
          const url = img?.imageUrl;
          if (pid == null || !url) return;
          const prefer = img?.isMain === true;
          const prevUrl = thumbs[pid];
          if (!prevUrl || prefer) thumbs[pid] = url;
        });
        setProductThumbById(thumbs);
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
      setError(err?.message || "Không t?i du?c dữ liệu qu?n tr? kho.");
    } finally {
      setLoading(false);
    }
  }, [section, txParams, isCatalogSection]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const clearMainImagePick = useCallback(() => {
    setProductMainImagePreview((prev) => {
      if (prev && String(prev).startsWith("blob:")) {
        URL.revokeObjectURL(prev);
      }
      return "";
    });
    setProductMainImageFile(null);
  }, []);

  const refreshEditProductImages = useCallback(async (productId) => {
    if (productId == null) {
      setEditProductImages([]);
      return;
    }
    try {
      const rows = await inventoryApi.getProductImages({ productId });
      const list = Array.isArray(rows) ? [...rows] : [];
      list.sort((a, b) => {
        if (a.isMain === true && b.isMain !== true) return -1;
        if (b.isMain === true && a.isMain !== true) return 1;
        return (a.sortOrder ?? 0) - (b.sortOrder ?? 0);
      });
      setEditProductImages(list);
    } catch {
      setEditProductImages([]);
    }
  }, []);

  useEffect(() => {
    if (!showCreateProductModal || !editingProduct?.id) {
      setEditProductImages([]);
      return;
    }
    refreshEditProductImages(editingProduct.id);
  }, [showCreateProductModal, editingProduct?.id, refreshEditProductImages]);

  const categoryMap = useMemo(() => {
    const map = new Map();
    (categories || []).forEach((item) => map.set(item.id, item.categoryName));
    return map;
  }, [categories]);

  const productCountByCategory = useMemo(() => {
    const counts = new Map();
    (products || []).forEach((product) => {
      const categoryId = product?.categoryId;
      if (!categoryId) return;
      const key = String(categoryId);
      counts.set(key, (counts.get(key) || 0) + 1);
    });
    return counts;
  }, [products]);

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
  const pagedWarehouses = useMemo(
    () =>
      (warehouseStructure.warehouses || []).slice(
        (warehousePage - 1) * warehousePageSize,
        warehousePage * warehousePageSize
      ),
    [warehouseStructure.warehouses, warehousePage, warehousePageSize]
  );
  const pagedProducts = useMemo(
    () => products.slice((productPage - 1) * productPageSize, productPage * productPageSize),
    [products, productPage, productPageSize]
  );
  const pagedCategories = useMemo(
    () => categories.slice((categoryPage - 1) * categoryPageSize, categoryPage * categoryPageSize),
    [categories, categoryPage, categoryPageSize]
  );
  const pagedSuppliers = useMemo(
    () => suppliers.slice((supplierPage - 1) * supplierPageSize, supplierPage * supplierPageSize),
    [suppliers, supplierPage, supplierPageSize]
  );
  const reportStockByBranchRows = reports?.stockByBranch || [];
  const pagedReportStockByBranch = useMemo(
    () =>
      reportStockByBranchRows.slice(
        (reportBranchPage - 1) * reportBranchPageSize,
        reportBranchPage * reportBranchPageSize
      ),
    [reportStockByBranchRows, reportBranchPage, reportBranchPageSize]
  );
  const reportTopOutboundRows = reports?.topOutbound || [];
  const pagedReportTopOutbound = useMemo(
    () =>
      reportTopOutboundRows.slice(
        (reportTopOutboundPage - 1) * reportTopOutboundPageSize,
        reportTopOutboundPage * reportTopOutboundPageSize
      ),
    [reportTopOutboundRows, reportTopOutboundPage, reportTopOutboundPageSize]
  );
  const reportAuditLogRows = reports?.auditLogs || [];
  const pagedReportAuditLogs = useMemo(
    () =>
      reportAuditLogRows.slice(
        (reportAuditLogPage - 1) * reportAuditLogPageSize,
        reportAuditLogPage * reportAuditLogPageSize
      ),
    [reportAuditLogRows, reportAuditLogPage, reportAuditLogPageSize]
  );

  useEffect(() => {
    setWarehousePage(1);
    setProductPage(1);
    setCategoryPage(1);
    setSupplierPage(1);
    setReportBranchPage(1);
    setReportTopOutboundPage(1);
    setReportAuditLogPage(1);
  }, [section]);

  const warehouseOptionsByBranch = useMemo(() => {
    if (!txParams.branchId) return warehouseStructure.warehouses || [];
    return (warehouseStructure.warehouses || []).filter((w) => String(w.branchId) === String(txParams.branchId));
  }, [warehouseStructure.warehouses, txParams.branchId]);

  const updateForm = (name, value) => setForm((prev) => ({ ...prev, [name]: value }));
  const updateProductForm = (name, value) => setProductForm((prev) => ({ ...prev, [name]: value }));

  const onMainProductImageChange = (event) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    const okTypes = ["image/jpeg", "image/png", "image/webp"];
    if (!okTypes.includes(file.type)) {
      setError("Ch? ch?p nh?n ?nh JPEG, PNG ho?c WEBP.");
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError("?nh kh?ng du?c vu?t qu? 5MB.");
      return;
    }
    setError("");
    setProductMainImagePreview((prev) => {
      if (prev && String(prev).startsWith("blob:")) {
        URL.revokeObjectURL(prev);
      }
      return URL.createObjectURL(file);
    });
    setProductMainImageFile(file);
  };

  const onGalleryImageSelected = async (event) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || !editingProduct?.id) return;
    const okTypes = ["image/jpeg", "image/png", "image/webp"];
    if (!okTypes.includes(file.type)) {
      setError("Ch? ch?p nh?n ?nh JPEG, PNG ho?c WEBP.");
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError("?nh kh?ng du?c vu?t qu? 5MB.");
      return;
    }
    setError("");
    setGalleryBusy(true);
    try {
      await inventoryApi.appendProductGalleryImage(editingProduct.id, file);
      await refreshEditProductImages(editingProduct.id);
      await fetchData();
    } catch (err) {
      setError(err?.message || "Không t?i du?c ?nh ph? l�n.");
    } finally {
      setGalleryBusy(false);
    }
  };

  const onDeleteProductImageRow = async (imageRow) => {
    if (!editingProduct?.id || !imageRow?.id) return;
    const ok = window.confirm("X�a ?nh n�y? ?nh tr�n cloud v?n c� th? t?n t?i n?u b?n cần d?n tay tr�n Azure.");
    if (!ok) return;
    try {
      await inventoryApi.deleteProductImage(imageRow.id);
      await refreshEditProductImages(editingProduct.id);
      await fetchData();
    } catch (err) {
      setError(err?.message || "Không x�a du?c ?nh.");
    }
  };
  const updateCategoryForm = (name, value) => setCategoryForm((prev) => ({ ...prev, [name]: value }));

  const openCreateProductModal = () => {
    setProductModalMode("create");
    setEditingProduct(null);
    setProductForm({ ...EMPTY_PRODUCT_FORM });
    clearMainImagePick();
    setShowCreateProductModal(true);
  };

  const openEditProductModal = (product) => {
    setProductModalMode("edit");
    clearMainImagePick();
    setEditingProduct(product);
    setProductForm({
      sku: product?.sku || "",
      name: product?.name || "",
      unit: product?.unit || "Cái",
      categoryId: product?.categoryId == null ? "" : String(product.categoryId),
      costPrice: product?.costPrice == null ? "" : String(product.costPrice),
      retailPrice: product?.retailPrice == null ? "" : String(product.retailPrice),
      servicePrice: product?.servicePrice == null ? "" : String(product.servicePrice),
      taxRate: product?.taxRate == null ? "" : String(product.taxRate),
      warrantyMonths: product?.warrantyMonths == null ? "" : String(product.warrantyMonths),
      isStrictCapacityControl: !!product?.isStrictCapacityControl,
      isActive: product?.isActive !== false
    });
    setShowCreateProductModal(true);
  };

  const closeCreateProductModal = () => {
    if (savingProduct) return;
    clearMainImagePick();
    setEditingProduct(null);
    setShowCreateProductModal(false);
  };

  const openCreateCategoryModal = () => {
    setCategoryModalMode("create");
    setEditingCategory(null);
    setCategoryForm({ ...EMPTY_CATEGORY_FORM });
    setShowCategoryModal(true);
  };

  const openEditCategoryModal = (category) => {
    setCategoryModalMode("edit");
    setEditingCategory(category);
    setCategoryForm({
      categoryName: category?.categoryName || "",
      parentId: category?.parentId == null ? "" : String(category.parentId),
      description: category?.description || "",
      isDepositRequired: !!category?.isDepositRequired
    });
    setShowCategoryModal(true);
  };

  const closeCategoryModal = () => {
    if (savingCategory) return;
    setEditingCategory(null);
    setShowCategoryModal(false);
  };

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

  const buildCategoryPayloadFromForm = () => {
    const categoryName = categoryForm.categoryName.trim();
    if (!categoryName) throw new Error("T�n danh m?c l� b?t bu?c.");

    let parentId = null;
    const rawParentId = String(categoryForm.parentId || "").trim();
    if (rawParentId) {
      const parsedParentId = Number(rawParentId);
      if (!Number.isFinite(parsedParentId) || !Number.isInteger(parsedParentId)) {
        throw new Error("Danh m?c cha không h?p l?.");
      }
      const exists = (categories || []).some((item) => Number(item.id) === parsedParentId);
      if (!exists) throw new Error("Danh m?c cha không t?n t?i.");
      if (editingCategory?.id && Number(editingCategory.id) === parsedParentId) {
        throw new Error("Danh m?c cha không th? l� ch�nh danh m?c hi?n t?i.");
      }
      parentId = parsedParentId;
    }

    return {
      categoryName,
      parentId,
      description: categoryForm.description.trim() || null,
      isDepositRequired: !!categoryForm.isDepositRequired,
      isDeleted: false
    };
  };

  const handleCreateCategory = async () => {
    setSavingCategory(true);
    try {
      const payload = buildCategoryPayloadFromForm();
      await inventoryApi.createPartCategory(payload);
      setShowCategoryModal(false);
      setEditingCategory(null);
      setCategoryForm({ ...EMPTY_CATEGORY_FORM });
      await fetchData();
    } finally {
      setSavingCategory(false);
    }
  };

  const handleUpdateCategory = async () => {
    if (!editingCategory?.id) throw new Error("Không t�m th?y danh m?c cần c?p nh?t.");

    setSavingCategory(true);
    try {
      const payload = buildCategoryPayloadFromForm();
      await inventoryApi.updatePartCategory(editingCategory.id, {
        ...editingCategory,
        ...payload
      });
      setShowCategoryModal(false);
      setEditingCategory(null);
      setCategoryForm({ ...EMPTY_CATEGORY_FORM });
      await fetchData();
    } finally {
      setSavingCategory(false);
    }
  };

  const handleCreateSupplier = async () => {
    const supplierName = form.supplierName.trim();
    if (!supplierName) return;
    await inventoryApi.createSupplier({ supplierName, currentDebt: 0 });
    setForm((prev) => ({ ...prev, supplierName: "" }));
    await fetchData();
  };

  const buildProductPayloadFromForm = () => {
    const sku = productForm.sku.trim();
    const name = productForm.name.trim();
    const unit = productForm.unit.trim();
    if (!sku || !name || !unit) {
      throw new Error("SKU, t�n s?n ph?m v� don v? t�nh l� b?t bu?c.");
    }

    const parseOptionalInt = (rawValue, label) => {
      const parsed = toNullableNumber(rawValue);
      if (parsed == null) return null;
      if (!Number.isInteger(parsed)) {
        throw new Error(`${label} ph?i l� s? nguy�n.`);
      }
      return parsed;
    };

    const parseOptionalDecimal = (rawValue) => {
      const parsed = toNullableNumber(rawValue);
      if (parsed == null) return null;
      return parsed;
    };

    const categoryId = productForm.categoryId ? Number(productForm.categoryId) : null;
    if (categoryId != null && !(categories || []).some((item) => Number(item.id) === categoryId)) {
      throw new Error("Danh m?c không h?p l?.");
    }

    return {
      sku,
      name,
      unit,
      categoryId,
      costPrice: parseOptionalDecimal(productForm.costPrice),
      retailPrice: parseOptionalDecimal(productForm.retailPrice),
      servicePrice: parseOptionalDecimal(productForm.servicePrice),
      taxRate: parseOptionalDecimal(productForm.taxRate),
      warrantyMonths: parseOptionalInt(productForm.warrantyMonths, "B?o h�nh"),
      isStrictCapacityControl: !!productForm.isStrictCapacityControl,
      requireEmptyReturn: false,
      isActive: !!productForm.isActive,
      isDeleted: false
    };
  };

  const handleCreateProduct = async () => {
    setSavingProduct(true);
    try {
      const payload = buildProductPayloadFromForm();
      const created = await inventoryApi.createProduct(payload);
      if (productMainImageFile && created?.id != null) {
        await inventoryApi.uploadProductImage(created.id, productMainImageFile);
      }
      clearMainImagePick();
      setShowCreateProductModal(false);
      setEditingProduct(null);
      setProductForm({ ...EMPTY_PRODUCT_FORM });
      await fetchData();
    } finally {
      setSavingProduct(false);
    }
  };

  const handleUpdateProduct = async () => {
    if (!editingProduct?.id) throw new Error("Không t�m th?y s?n ph?m cần c?p nh?t.");

    setSavingProduct(true);
    try {
      const payload = buildProductPayloadFromForm();
      await inventoryApi.updateProduct(editingProduct.id, {
        ...editingProduct,
        ...payload
      });
      if (productMainImageFile) {
        await inventoryApi.uploadProductImage(editingProduct.id, productMainImageFile);
      }
      clearMainImagePick();
      setShowCreateProductModal(false);
      setEditingProduct(null);
      setProductForm({ ...EMPTY_PRODUCT_FORM });
      await fetchData();
    } finally {
      setSavingProduct(false);
    }
  };

  const markDiscontinued = async (product) => {
    await inventoryApi.updateProduct(product.id, { ...product, isActive: false });
    await fetchData();
  };

  const handleDeleteProduct = async (product) => {
    const ok = window.confirm(`X�a s?n ph?m "${product?.name || "-"}" (SKU: ${product?.sku || "-"})?`);
    if (!ok) return;
    await inventoryApi.deleteProduct(product.id);
    await fetchData();
  };

  const handleDeleteCategory = async (category) => {
    const ok = window.confirm(`X�a danh m?c "${category?.categoryName || "-"}"?`);
    if (!ok) return;
    await inventoryApi.deletePartCategory(category.id);
    await fetchData();
  };

  const handleEditSupplier = async (supplier) => {
    const currentName = supplier?.supplierName || supplier?.name || "";
    const nextNameInput = window.prompt("T�n nh� cung c?p", currentName);
    if (nextNameInput == null) return;
    const nextName = nextNameInput.trim();
    if (!nextName) {
      setError("T�n nh� cung c?p không du?c d? tr?ng.");
      return;
    }
    await inventoryApi.updateSupplier(supplier.id, {
      ...supplier,
      supplierName: nextName
    });
    await fetchData();
  };

  const handleDeleteSupplier = async (supplier) => {
    const supplierName = supplier?.supplierName || supplier?.name || "-";
    const ok = window.confirm(`X�a nh� cung c?p "${supplierName}"?`);
    if (!ok) return;
    await inventoryApi.deleteSupplier(supplier.id);
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
    await inventoryApi.rejectAudit(id, { note: "T? ch?i b?i qu?n tr? vi�n" });
    await fetchData();
  };

  const handleApproveRequest = async (id) => {
    await inventoryApi.approveRequest(id, {});
    await fetchData();
  };

  const handleRejectRequest = async (id) => {
    await inventoryApi.rejectRequest(id, { note: "T? ch?i b?i qu?n tr? vi�n" });
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
      <StockAlertBanner maxItems={6} />
      {error ? <div className="alert alert-error">{error}</div> : null}

      {loading ? (
        <div className="loading-state">
          <div className="spinner" />
          <p>Đang t?i dữ liệu kho...</p>
        </div>
      ) : (
        <>
          {section === "warehouseStructure" ? (
            <>
              <StatCards
                items={[
                  { label: "Tổng s? kho", value: warehouseStructure.warehouses.length },
                  { label: "S? bin", value: warehouseStructure.bins.length },
                  { label: "Kho dang ho?t d?ng", value: warehouseStructure.warehouses.filter((w) => w.isActive).length }
                ]}
              />
              <ActionPanel
                title="T?o kho m?i"
                description="Nh?p th?ng tin co b?n d? th?m kho m?i theo tổng chi nh?nh."
              >
                <div className="table-toolbar inventory-form-grid">
                  <label className="compact-field">
                    <span>M� kho</span>
                    <input
                      className="search-input"
                      placeholder="V� d?: MAIN-WH"
                      value={form.newWarehouseCode}
                      onChange={(e) => updateForm("newWarehouseCode", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>T�n kho</span>
                    <input
                      className="search-input"
                      placeholder="V� d?: Kho phụ tùng ch�nh"
                      value={form.newWarehouseName}
                      onChange={(e) => updateForm("newWarehouseName", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Lo?i kho</span>
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
                      <option value="">Chọn chi nh?nh</option>
                      {(warehouseStructure.branches || []).map((b) => (
                        <option key={b.id} value={b.id}>{b.name}</option>
                      ))}
                    </select>
                  </label>
                  <button className="btn-primary" onClick={() => runAction(handleCreateWarehouse)}>
                    + T?o kho
                  </button>
                </div>
              </ActionPanel>

              {settings ? (
                <ActionPanel
                  title="G?n kho m?c d?nh theo lo?i h?ng"
                  description="Hệ thống s? t? d?ng uu ti?n kho n?y khi ph?t sinh nghi?p v? tuong ?ng."
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
                          <option value={0}>-- Chua chọn --</option>
                          {warehouseStructure.warehouses.map((w) => (
                            <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
                          ))}
                        </select>
                      </label>
                    ))}
                    <button className="btn-primary" onClick={() => runAction(() => saveSettings(settings))}>
                      Luu kho m?c d?nh
                    </button>
                  </div>
                </ActionPanel>
              ) : null}

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>M� kho</th>
                      <th>T�n kho</th>
                      <th>Lo?i kho</th>
                      <th>Chi nhánh</th>
                      <th>Tr?ng th?i</th>
                      <th>S? bin</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {warehouseStructure.warehouses.length === 0 ? (
                      <NoDataRow colSpan={8} message="Chua c� kho n�o. H�y t?o kho d?u ti�n d? b?t d?u c?u h�nh." />
                    ) : (
                      pagedWarehouses.map((w) => (
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
                              {w.isActive ? "Đang ho?t d?ng" : "Đã kh�a"}
                            </span>
                          </td>
                          <td>{warehouseStructure.bins.filter((b) => b.warehouseId === w.id).length}</td>
                          <td>
                            <button className="btn-icon" onClick={() => runAction(() => handleToggleWarehouse(w))}>
                              {w.isActive ? "Kh�a kho" : "B?t kho"}
                            </button>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <TablePagination
                totalItems={(warehouseStructure.warehouses || []).length}
                currentPage={warehousePage}
                pageSize={warehousePageSize}
                onPageChange={setWarehousePage}
                onPageSizeChange={(size) => {
                  setWarehousePageSize(size);
                  setWarehousePage(1);
                }}
              />
            </>
          ) : null}

          {isCatalogProductsSection ? (
            <>
              <StatCards
                items={[
                  { label: "Tổng s?n ph?m", value: products.length },
                  { label: "Danh m?c", value: categories.length },
                  { label: "Nh� cung c?p", value: suppliers.length }
                ]}
              />

              <ActionPanel
                title="Quản lý danh m?c s?n ph?m"
                description="Th�m m?i s?n ph?m bảng popup chi ti?t theo Product model, d?ng th?i h? tr? import/export CSV."
              >
                <div className="table-toolbar inventory-form-actions">
                  <button className="btn-primary" onClick={openCreateProductModal}>
                    + Th�m s?n ph?m
                  </button>
                </div>
                <div className="table-toolbar inventory-form-actions">
                  <button className="btn-ghost" onClick={exportProductsCsv}>Xu?t CSV danh m?c</button>
                  <label className="btn-ghost file-label">
                    Nh?p CSV danh m?c
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

              {showCreateProductModal ? (
                <Modal title={productModalMode === "edit" ? "S?a s?n ph?m" : "Th�m s?n ph?m m?i"} onClose={closeCreateProductModal}>
                  <div className="inventory-product-modal">
                    <p className="inventory-product-modal-desc">
                      �i?n thông tin s?n ph?m. ?nh t?i l�n Azure Blob (c?u h�nh{" "}
                      <code className="inventory-code-inline">AzureStorage</code>); hệ thống ch? luu URL trong{" "}
                      <code className="inventory-code-inline">ProductImages</code>. Độnh d?ng JPEG/PNG/WEBP, t?i da 5MB.
                    </p>

                    <div className="inventory-product-modal-section">
                      <h4>Thông tin chung</h4>
                      <div className="inventory-product-modal-grid inventory-product-modal-grid--basic">
                        <label className="compact-field">
                          <span>SKU *</span>
                          <input
                            className="search-input"
                            value={productForm.sku}
                            onChange={(e) => updateProductForm("sku", e.target.value)}
                            placeholder="V� d?: SKU-0001"
                          />
                        </label>
                        <label className="compact-field">
                          <span>T�n s?n ph?m *</span>
                          <input
                            className="search-input"
                            value={productForm.name}
                            onChange={(e) => updateProductForm("name", e.target.value)}
                            placeholder="V� d?: L?c nhot Honda"
                          />
                        </label>
                        <label className="compact-field">
                          <span>Đơn v? t�nh *</span>
                          <input
                            className="search-input"
                            value={productForm.unit}
                            onChange={(e) => updateProductForm("unit", e.target.value)}
                            placeholder="Cái"
                          />
                        </label>
                        <label className="compact-field">
                          <span>Danh m?c</span>
                          <select
                            className="search-input"
                            value={productForm.categoryId}
                            onChange={(e) => updateProductForm("categoryId", e.target.value)}
                          >
                            <option value="">-- Chua g?n --</option>
                            {categories.map((category) => (
                              <option key={category.id} value={category.id}>
                                {category.categoryName}
                              </option>
                            ))}
                          </select>
                        </label>
                      </div>
                    </div>

                    <div className="inventory-product-modal-section">
                      <h4>?nh Part Shop (cloud)</h4>
                      <p className="inventory-product-image-hint">
                        ?nh d?i di?n d�ng cho catalog / gi? h�ng. Nh?n <strong>Luu s?n ph?m</strong> d? g?i file l�n Azure v� c?p nh?t
                        URL trong CSDL. Khi s?a, chọn ?nh m?i r?i luu d? thay ?nh ch�nh (ghi d� b?n ghi d?i di?n).
                      </p>
                      <div className="inventory-product-image-row">
                        {productMainImagePreview ||
                        (productModalMode === "edit" &&
                          (editProductImages.find((i) => i.isMain)?.imageUrl || editProductImages[0]?.imageUrl)) ? (
                          <img
                            className="inventory-product-image-preview"
                            src={
                              productMainImagePreview ||
                              editProductImages.find((i) => i.isMain)?.imageUrl ||
                              editProductImages[0]?.imageUrl ||
                              ""
                            }
                            alt=""
                          />
                        ) : (
                          <div className="inventory-product-image-placeholder">Chua c� ?nh</div>
                        )}
                        <div className="inventory-product-image-actions">
                          <label className="btn-ghost inventory-product-image-file">
                            Chọn ?nh d?i di?n
                            <input
                              type="file"
                              accept="image/jpeg,image/png,image/webp"
                              className="inventory-file-input-hidden"
                              onChange={onMainProductImageChange}
                            />
                          </label>
                          {productMainImageFile ? (
                            <button type="button" className="btn-ghost" onClick={clearMainImagePick}>
                              B? chọn file
                            </button>
                          ) : null}
                        </div>
                      </div>

                      {productModalMode === "edit" && editingProduct?.id ? (
                        <div className="inventory-product-gallery-block">
                          <div className="inventory-product-gallery-toolbar">
                            <span className="inventory-product-gallery-title">Gallery (trang chi ti?t)</span>
                            <label
                              className={`btn-ghost inventory-product-image-file ${galleryBusy ? "is-busy" : ""}`}
                            >
                              {galleryBusy ? "Đang t?i..." : "+ Th�m ?nh ph?"}
                              <input
                                type="file"
                                accept="image/jpeg,image/png,image/webp"
                                className="inventory-file-input-hidden"
                                disabled={galleryBusy}
                                onChange={onGalleryImageSelected}
                              />
                            </label>
                          </div>
                          {editProductImages.length === 0 ? (
                            <p className="inventory-product-gallery-empty">Chua c� ?nh trong CSDL.</p>
                          ) : (
                            <ul className="inventory-product-gallery-list">
                              {editProductImages.map((img) => (
                                <li key={img.id} className="inventory-product-gallery-item">
                                  <img src={img.imageUrl} alt="" className="inventory-product-gallery-thumb" />
                                  <div className="inventory-product-gallery-meta">
                                    {img.isMain ? (
                                      <span className="badge badge-active">Đội di?n</span>
                                    ) : (
                                      <span className="badge">Ph?</span>
                                    )}
                                    <button
                                      type="button"
                                      className="btn-icon"
                                      onClick={() => onDeleteProductImageRow(img)}
                                      disabled={galleryBusy}
                                    >
                                      X�a
                                    </button>
                                  </div>
                                </li>
                              ))}
                            </ul>
                          )}
                        </div>
                      ) : null}
                    </div>

                    <div className="inventory-product-modal-section">
                      <h4>Gi? v? chọnh s?ch</h4>
                      <div className="inventory-product-modal-grid inventory-product-modal-grid--price">
                        <label className="compact-field">
                          <span>Gi? vdn</span>
                          <input
                            className="search-input"
                            type="number"
                            value={productForm.costPrice}
                            onChange={(e) => updateProductForm("costPrice", e.target.value)}
                            placeholder="0"
                          />
                        </label>
                        <label className="compact-field">
                          <span>Gi? b?n l?</span>
                          <input
                            className="search-input"
                            type="number"
                            value={productForm.retailPrice}
                            onChange={(e) => updateProductForm("retailPrice", e.target.value)}
                            placeholder="0"
                          />
                        </label>
                        <label className="compact-field">
                          <span>Gi? dịch vụ</span>
                          <input
                            className="search-input"
                            type="number"
                            value={productForm.servicePrice}
                            onChange={(e) => updateProductForm("servicePrice", e.target.value)}
                            placeholder="0"
                          />
                        </label>
                        <label className="compact-field">
                          <span>Thu? (%)</span>
                          <input
                            className="search-input"
                            type="number"
                            value={productForm.taxRate}
                            onChange={(e) => updateProductForm("taxRate", e.target.value)}
                            placeholder="0"
                          />
                        </label>
                        <label className="compact-field">
                          <span>B?o h?nh (th?ng)</span>
                          <input
                            className="search-input"
                            type="number"
                            value={productForm.warrantyMonths}
                            onChange={(e) => updateProductForm("warrantyMonths", e.target.value)}
                            placeholder="0"
                          />
                        </label>
                      </div>
                    </div>

                    <div className="inventory-product-modal-toggles">
                      <label className="inventory-product-toggle">
                        <input
                          type="checkbox"
                          checked={!!productForm.isStrictCapacityControl}
                          onChange={(e) => updateProductForm("isStrictCapacityControl", e.target.checked)}
                        />
                        <span>Ki?m so?t s?c ch?a nghi?m ng?t</span>
                      </label>
                      <label className="inventory-product-toggle">
                        <input
                          type="checkbox"
                          checked={!!productForm.isActive}
                          onChange={(e) => updateProductForm("isActive", e.target.checked)}
                        />
                        <span>Đang ho?t d?ng</span>
                      </label>
                    </div>

                    <div className="form-actions">
                      <button type="button" className="btn-ghost" onClick={closeCreateProductModal} disabled={savingProduct}>
                        H?y
                      </button>
                      <button
                        type="button"
                        className="btn-primary"
                        onClick={() => runAction(productModalMode === "edit" ? handleUpdateProduct : handleCreateProduct)}
                        disabled={savingProduct}
                      >
                        {savingProduct ? "Đang luu..." : (productModalMode === "edit" ? "C?p nh?t s?n ph?m" : "Luu s?n ph?m")}
                      </button>
                    </div>
                  </div>
                </Modal>
              ) : null}

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>?nh</th>
                      <th>SKU</th>
                      <th>T�n s?n ph?m</th>
                      <th>Đơn v?</th>
                      <th>Danh m?c</th>
                      <th>Gi? vdn</th>
                      <th>Gi? b?n l?</th>
                      <th>Gi? dịch vụ</th>
                      <th>Thu? (%)</th>
                      <th>B?o h?nh (th?ng)</th>
                      <th>K.S s?c ch?a</th>
                      <th>Tr?ng th?i</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {products.length === 0 ? (
                      <NoDataRow colSpan={14} message="Chua c� s?n ph?m trong danh m?c kho." />
                    ) : (
                      pagedProducts.map((p) => (
                        <tr key={p.id}>
                          <td className="td-id">#{p.id}</td>
                          <td className="td-thumb">
                            {productThumbById[p.id] ? (
                              <img
                                src={productThumbById[p.id]}
                                alt=""
                                className="inventory-product-table-thumb"
                                loading="lazy"
                              />
                            ) : (
                              <span className="inventory-product-table-thumb-fallback">�</span>
                            )}
                          </td>
                          <td>{p.sku}</td>
                          <td>{p.name}</td>
                          <td>{p.unit}</td>
                          <td>{categoryMap.get(p.categoryId) || "-"}</td>
                          <td>{toNumber(p.costPrice, 0).toLocaleString()}</td>
                          <td>{toNumber(p.retailPrice, 0).toLocaleString()}</td>
                          <td>{toNumber(p.servicePrice, 0).toLocaleString()}</td>
                          <td>{toNumber(p.taxRate, 0)}</td>
                          <td>{toNumber(p.warrantyMonths, 0)}</td>
                          <td>{p.isStrictCapacityControl ? "C�" : "Không"}</td>
                          <td>
                            <span className={`badge ${p.isActive ? "badge-active" : "badge-deleted"}`}>
                              {p.isActive ? "Đang kinh doanh" : "Ng?ng kinh doanh"}
                            </span>
                          </td>
                          <td>
                            <div className="row-actions">
                              <button className="btn-icon" onClick={() => openEditProductModal(p)}>
                                S?a
                              </button>
                              <button className="btn-icon" onClick={() => runAction(() => handleDeleteProduct(p))}>
                                X�a
                              </button>
                              {p.isActive ? (
                                <button className="btn-icon" onClick={() => runAction(() => markDiscontinued(p))}>
                                  Ng?ng KD
                                </button>
                              ) : (
                                <span className="result-count">Đã ng?ng</span>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <TablePagination
                totalItems={(products || []).length}
                currentPage={productPage}
                pageSize={productPageSize}
                onPageChange={setProductPage}
                onPageSizeChange={(size) => {
                  setProductPageSize(size);
                  setProductPage(1);
                }}
              />
            </>
          ) : null}

          {isCatalogCategoriesSection ? (
            <>
              <StatCards
                items={[
                  { label: "Tổng danh m?c", value: categories.length },
                  { label: "S?n ph?m d? ph?n lo?i", value: products.filter((p) => !!p.categoryId).length },
                  { label: "S?n ph?m chua g?n danh m?c", value: products.filter((p) => !p.categoryId).length }
                ]}
              />

              <ActionPanel
                title="Quản lý danh m?c"
                description="Th?m v? chọnh s?a danh m?c bảng popup d? chu?n h?a dữ liệu ph?n lo?i vật tư."
              >
                <div className="table-toolbar inventory-form-actions">
                  <button className="btn-primary" onClick={openCreateCategoryModal}>
                    + T?o danh m?c
                  </button>
                </div>
              </ActionPanel>

              {showCategoryModal ? (
                <Modal title={categoryModalMode === "edit" ? "S?a danh m?c" : "Th�m danh m?c m?i"} onClose={closeCategoryModal}>
                  <div className="inventory-product-modal">
                    <p className="inventory-product-modal-desc">
                      Qu?n l? nh?m danh m?c d? chu?n h?a ph?n lo?i s?n ph?m trong kho.
                    </p>

                    <div className="inventory-product-modal-section">
                      <h4>Thông tin danh m?c</h4>
                      <div className="inventory-product-modal-grid inventory-product-modal-grid--basic">
                        <label className="compact-field">
                          <span>T�n danh m?c *</span>
                          <input
                            className="search-input"
                            value={categoryForm.categoryName}
                            onChange={(e) => updateCategoryForm("categoryName", e.target.value)}
                            placeholder="V� d?: D?u nhot"
                          />
                        </label>
                        <label className="compact-field">
                          <span>Danh m?c cha</span>
                          <select
                            className="search-input"
                            value={categoryForm.parentId}
                            onChange={(e) => updateCategoryForm("parentId", e.target.value)}
                          >
                            <option value="">-- Không c� --</option>
                            {categories
                              .filter((item) => !editingCategory || Number(item.id) !== Number(editingCategory.id))
                              .map((item) => (
                                <option key={item.id} value={item.id}>
                                  {item.categoryName}
                                </option>
                              ))}
                          </select>
                        </label>
                        <label className="compact-field inventory-span-2">
                          <span>M� t?</span>
                          <input
                            className="search-input"
                            value={categoryForm.description}
                            onChange={(e) => updateCategoryForm("description", e.target.value)}
                            placeholder="M� t? ng?n v? nh�m danh m?c"
                          />
                        </label>
                      </div>
                    </div>

                    <div className="inventory-product-modal-toggles">
                      <label className="inventory-product-toggle">
                        <input
                          type="checkbox"
                          checked={!!categoryForm.isDepositRequired}
                          onChange={(e) => updateCategoryForm("isDepositRequired", e.target.checked)}
                        />
                        <span>Yêu cầu d?t c?c</span>
                      </label>
                    </div>

                    <div className="form-actions">
                      <button className="btn-ghost" onClick={closeCategoryModal} disabled={savingCategory}>
                        H?y
                      </button>
                      <button
                        className="btn-primary"
                        onClick={() => runAction(categoryModalMode === "edit" ? handleUpdateCategory : handleCreateCategory)}
                        disabled={savingCategory}
                      >
                        {savingCategory ? "Đang luu..." : (categoryModalMode === "edit" ? "C?p nh?t danh m?c" : "Luu danh m?c")}
                      </button>
                    </div>
                  </div>
                </Modal>
              ) : null}

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>T�n danh m?c</th>
                      <th>Danh m?c cha</th>
                      <th>M� t?</th>
                      <th>Đặt c?c</th>
                      <th>S? s?n ph?m</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {categories.length === 0 ? (
                      <NoDataRow colSpan={7} message="Chua c� danh m?c n�o trong kho." />
                    ) : (
                      pagedCategories.map((category) => (
                        <tr key={category.id}>
                          <td className="td-id">#{category.id}</td>
                          <td>{category.categoryName || "-"}</td>
                          <td>{category.parentId ? (categoryMap.get(category.parentId) || `#${category.parentId}`) : "-"}</td>
                          <td>{category.description || "-"}</td>
                          <td>{category.isDepositRequired ? "C�" : "Không"}</td>
                          <td>{toNumber(productCountByCategory.get(String(category.id)), 0)}</td>
                          <td>
                            <div className="row-actions">
                              <button className="btn-icon" onClick={() => openEditCategoryModal(category)}>
                                S?a
                              </button>
                              <button className="btn-icon" onClick={() => runAction(() => handleDeleteCategory(category))}>
                                X�a
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <TablePagination
                totalItems={(categories || []).length}
                currentPage={categoryPage}
                pageSize={categoryPageSize}
                onPageChange={setCategoryPage}
                onPageSizeChange={(size) => {
                  setCategoryPageSize(size);
                  setCategoryPage(1);
                }}
              />
            </>
          ) : null}

          {isCatalogSuppliersSection ? (
            <>
              <StatCards
                items={[
                  { label: "Tổng nh� cung c?p", value: suppliers.length },
                  { label: "S?n ph?m hi?n c�", value: products.length },
                  { label: "Danh m?c hi?n c�", value: categories.length }
                ]}
              />

              <ActionPanel
                title="T?o nh� cung c?p m?i"
                description="Qu?n l? danh s?ch nh? cung c?p d? ph?c v? truy xu?t ngu?n g?c v? mua h?ng."
              >
                <div className="table-toolbar inventory-form-grid inventory-form-grid--small">
                  <label className="compact-field">
                    <span>T�n nh� cung c?p</span>
                    <input
                      className="search-input"
                      placeholder="V� d?: Nh� cung c?p A"
                      value={form.supplierName}
                      onChange={(e) => updateForm("supplierName", e.target.value)}
                    />
                  </label>
                  <button className="btn-primary" onClick={() => runAction(handleCreateSupplier)}>
                    + T?o nh� cung c?p
                  </button>
                </div>
              </ActionPanel>

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Nh� cung c?p</th>
                      <th>C�ng n? hi?n t?i</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {suppliers.length === 0 ? (
                      <NoDataRow colSpan={4} message="Chua c� nh� cung c?p n�o trong hệ thống." />
                    ) : (
                      pagedSuppliers.map((supplier) => (
                        <tr key={supplier.id}>
                          <td className="td-id">#{supplier.id}</td>
                          <td>{supplier.supplierName || supplier.name || "-"}</td>
                          <td>{toNumber(supplier.currentDebt, 0).toLocaleString()}</td>
                          <td>
                            <div className="row-actions">
                              <button className="btn-icon" onClick={() => runAction(() => handleEditSupplier(supplier))}>
                                S?a
                              </button>
                              <button className="btn-icon" onClick={() => runAction(() => handleDeleteSupplier(supplier))}>
                                X�a
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <TablePagination
                totalItems={(suppliers || []).length}
                currentPage={supplierPage}
                pageSize={supplierPageSize}
                onPageChange={setSupplierPage}
                onPageSizeChange={(size) => {
                  setSupplierPageSize(size);
                  setSupplierPage(1);
                }}
              />
            </>
          ) : null}

          {section === "policy" && settings ? (
            <>
              <div className="policy-card">
                <div className="policy-card-head">
                  <h3>Thi?t l?p chọnh s?ch chung</h3>
                  <p>?p d?ng cho to?n hệ thống kho. C?u h?nh n?y ?nh hu?ng d?n cầnh b?o v? ph? duy?t.</p>
                </div>
                <div className="policy-grid">
                  <label className="compact-field policy-toggle">
                    <span>Cho ph?p ?m kho</span>
                    <input
                      type="checkbox"
                      checked={!!settings.policyRules?.allowNegativeStock}
                      onChange={(e) => updatePolicyField("allowNegativeStock", e.target.checked)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Ngu?ng cầnh b?o t?n th?p (%)</span>
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
                    <span>Quy t?c gi? h�ng (Reserve)</span>
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
                    <span>Cho ph�p xu?t vu?t d?nh m?c</span>
                    <input
                      type="checkbox"
                      checked={!!settings.policyRules?.allowOverIssue}
                      onChange={(e) => updatePolicyField("allowOverIssue", e.target.checked)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Quy t?c ho�n tr? vật tư</span>
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
                    <span>Quy t?c chuy?n kho</span>
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
                <div className="policy-overview-item"><span>Tổng chi nh?nh</span><strong>{policyOverview.branchCount}</strong></div>
                <div className="policy-overview-item"><span>Chi nh?nh ho?t d?ng</span><strong>{policyOverview.activeBranchCount}</strong></div>
                <div className="policy-overview-item"><span>T?n hi?n t?i</span><strong>{policyOverview.totalCurrent.toLocaleString()}</strong></div>
                <div className="policy-overview-item"><span>Đã gi? ch?</span><strong>{policyOverview.totalReserved.toLocaleString()}</strong></div>
                <div className="policy-overview-item"><span>Tổng t?n an to�n</span><strong>{policyOverview.totalSafeStock.toLocaleString()}</strong></div>
              </div>

              <div className="policy-card">
                <div className="policy-card-head">
                  <h3>C?u h?nh t?n an to?n theo chi nh?nh</h3>
                  <p>??t m?c t?n t?i thi?u ri?ng cho tổng chi nh?nh d? hệ thống cầnh b?o s?t th?c t? v?n h?nh.</p>
                </div>
                <div className="table-wrapper policy-table-wrapper">
                  <table className="admin-table">
                    <thead>
                      <tr>
                        <th>Chi nhánh</th>
                        <th>Tr?ng th?i</th>
                        <th>S? kho</th>
                        <th>T?n hi?n t?i</th>
                        <th>Đã gi? ch?</th>
                        <th>T?n an to�n</th>
                      </tr>
                    </thead>
                    <tbody>
                      {branchSummary.length === 0 ? (
                        <NoDataRow colSpan={6} message="Chua c? dữ liệu chi nh?nh d? c?u h?nh t?n an to?n." />
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
                  Luu chọnh s?ch
                </button>
              </div>
            </>
          ) : null}

          {section === "monitoring" && overview ? (
            <>
              <StatCards
                items={[
                  { label: "Tổng t?n", value: toNumber(overview.totalQuantity).toLocaleString() },
                  { label: "Đã gi? ch?", value: toNumber(overview.reservedQuantity).toLocaleString() },
                  { label: "S?p h?t", value: toNumber(overview.lowStockCount).toLocaleString() },
                  { label: "?m kho", value: toNumber(overview.negativeStockCount).toLocaleString() },
                  { label: "Kho l?i", value: toNumber(overview.defectiveStockCount).toLocaleString() },
                  { label: "Gi? tr? t?n", value: toNumber(overview.totalInventoryValue).toLocaleString() }
                ]}
              />
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Chi nhánh</th>
                      <th>T?n hi?n t?i</th>
                      <th>Đã gi? ch?</th>
                      <th>S?p h?t</th>
                      <th>?m kho</th>
                      <th>Gi? tr? t?n</th>
                    </tr>
                  </thead>
                  <tbody>
                    {branchSummary.length === 0 ? (
                      <NoDataRow colSpan={6} message="Chua c? dữ liệu gi?m s?t t?n kho." />
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
                    <th>Ngày ki?m k�</th>
                    <th>Chi nhánh</th>
                    <th>Kho</th>
                    <th>Tr?ng th?i</th>
                    <th>Tổng ch�nh l?ch</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {audits.length === 0 ? (
                    <NoDataRow colSpan={7} message="Chua c� k? ki?m k� n�o." />
                  ) : (
                    audits.map((a) => {
                      const status = resolveStatusMeta(a.status, AUDIT_STATUS_META);
                      const canAction = ["PENDING", "DRAFT"].includes(normalizeKey(a.status));
                      return (
                        <tr key={a.id}>
                          <td className="td-id">#{a.id}</td>
                          <td>{a.auditDate ? new Date(a.auditDate).toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
                          <td>{a.branchName}</td>
                          <td>{a.warehouseName}</td>
                          <td><span className={`badge ${status.className}`}>{status.label}</span></td>
                          <td>{toNumber(a.differenceTotal).toLocaleString()}</td>
                          <td>
                            {canAction ? (
                              <div className="row-actions">
                                <button className="btn-icon" onClick={() => runAction(() => handleApproveAudit(a.id))}>Duy?t</button>
                                <button className="btn-icon" onClick={() => runAction(() => handleRejectAudit(a.id))}>T? ch?i</button>
                              </div>
                            ) : (
                              <span className="result-count">Đã x? l�</span>
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
                title="B? l?c truy v?t giao d?ch"
                description="L?c theo chi nh?nh, kho, lo?i giao dịch vụ kho?ng thời gian d? truy xu?t nhanh."
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
                      <option value="">T?t c? chi nh?nh</option>
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
                      <option value="">T?t c? kho</option>
                      {warehouseOptionsByBranch.map((w) => (
                        <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
                      ))}
                    </select>
                  </label>
                  <label className="compact-field">
                    <span>Lo?i giao d?ch</span>
                    <input
                      className="search-input"
                      placeholder="V� d?: OUT, SERVICE_ISSUE, RETURN"
                      value={txParams.type}
                      onChange={(e) => handleTxFilterChange("type", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>T? ngày</span>
                    <input
                      className="search-input"
                      type="date"
                      value={txParams.from}
                      onChange={(e) => handleTxFilterChange("from", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Độn ngày</span>
                    <input
                      className="search-input"
                      type="date"
                      value={txParams.to}
                      onChange={(e) => handleTxFilterChange("to", e.target.value)}
                    />
                  </label>
                </div>
                <div className="table-toolbar inventory-form-actions">
                  <button className="btn-primary" onClick={fetchData}>L?c dữ liệu</button>
                  <button className="btn-ghost" onClick={resetTxFilters}>X�a b? l?c</button>
                </div>
              </ActionPanel>

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Thời gian</th>
                      <th>Lo?i giao d?ch</th>
                      <th>Chi nhánh</th>
                      <th>Kho</th>
                      <th>SKU</th>
                      <th>S? lu?ng</th>
                      <th>Ngu?n ph?t sinh</th>
                      <th>Ngu?i thao t?c</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(transactions.items || []).length === 0 ? (
                      <NoDataRow colSpan={9} message="Không c� giao d?ch n�o kh?p v?i b? l?c hi?n t?i." />
                    ) : (
                      (transactions.items || []).map((t) => (
                        <tr key={t.id}>
                          <td className="td-id">#{t.id}</td>
                          <td>{t.createdAt ? new Date(t.createdAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
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
                  Trang tru?c
                </button>
                <span className="result-count">
                  Trang {txParams.page} / {txTotalPages} - Tổng {toNumber(transactions.total).toLocaleString()} giao d?ch
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
                    <th>Lo?i yêu cầu</th>
                    <th>Tr?ng th?i</th>
                    <th>Ngu?i g?i</th>
                    <th>L� do</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {approvals.length === 0 ? (
                    <NoDataRow colSpan={6} message="Không c� yêu cầu ngo?i l? n�o." />
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
                                <button className="btn-icon" onClick={() => runAction(() => handleApproveRequest(a.id))}>Duy?t</button>
                                <button className="btn-icon" onClick={() => runAction(() => handleRejectRequest(a.id))}>T? ch?i</button>
                              </div>
                            ) : (
                              <span className="result-count">Đã x? l�</span>
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
                    <th>M?c d?</th>
                    <th>Lo?i cầnh b?o</th>
                    <th>N?i dung</th>
                    <th>Chi nhánh</th>
                    <th>Kho</th>
                    <th>Thời gian</th>
                  </tr>
                </thead>
                <tbody>
                  {alerts.length === 0 ? (
                    <NoDataRow colSpan={6} message="Hi?n chua c? cầnh b?o t?n kho." />
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
                          <td>{a.createdAt ? new Date(a.createdAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
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
                      <th>Tổng t?n</th>
                      <th>Gi? tr? t?n</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(reports.stockByBranch || []).length === 0 ? (
                      <NoDataRow colSpan={3} message="Chua c? dữ liệu t?n kho theo chi nh?nh." />
                    ) : (
                      pagedReportStockByBranch.map((r) => (
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
              <TablePagination
                totalItems={reportStockByBranchRows.length}
                currentPage={reportBranchPage}
                pageSize={reportBranchPageSize}
                onPageChange={setReportBranchPage}
                onPageSizeChange={(size) => {
                  setReportBranchPageSize(size);
                  setReportBranchPage(1);
                }}
              />

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Top s?n ph?m xu?t nhi?u</th>
                      <th>S? lu?ng xu?t</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(reports.topOutbound || []).length === 0 ? (
                      <NoDataRow colSpan={2} message="Chua c� dữ liệu top xu?t kho." />
                    ) : (
                      pagedReportTopOutbound.map((r) => (
                        <tr key={r.productId}>
                          <td>{r.sku} - {r.productName}</td>
                          <td>{toNumber(r.totalOutboundQty).toLocaleString()}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <TablePagination
                totalItems={reportTopOutboundRows.length}
                currentPage={reportTopOutboundPage}
                pageSize={reportTopOutboundPageSize}
                onPageChange={setReportTopOutboundPage}
                onPageSizeChange={(size) => {
                  setReportTopOutboundPageSize(size);
                  setReportTopOutboundPage(1);
                }}
              />

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>H�nh d?ng</th>
                      <th>Bảng dữ liệu</th>
                      <th>B?n ghi</th>
                      <th>Thời gian</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(reports.auditLogs || []).length === 0 ? (
                      <NoDataRow colSpan={5} message="Chua c� audit log li�n quan d?n kho." />
                    ) : (
                      pagedReportAuditLogs.map((log) => (
                        <tr key={log.id}>
                          <td className="td-id">#{log.id}</td>
                          <td>{log.action}</td>
                          <td>{log.tableName}</td>
                          <td>{log.recordId}</td>
                          <td>{log.timestamp ? new Date(log.timestamp).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
              <TablePagination
                totalItems={reportAuditLogRows.length}
                currentPage={reportAuditLogPage}
                pageSize={reportAuditLogPageSize}
                onPageChange={setReportAuditLogPage}
                onPageSizeChange={(size) => {
                  setReportAuditLogPageSize(size);
                  setReportAuditLogPage(1);
                }}
              />
            </>
          ) : null}
        </>
      )}
    </div>
  );
}








