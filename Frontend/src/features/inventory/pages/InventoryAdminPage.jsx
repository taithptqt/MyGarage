
import { useCallback, useEffect, useMemo, useState } from "react";
import { inventoryApi } from "@features/inventory/api/inventoryApi";
import { Modal } from "@features/admin/components/AdminModals";
import TablePagination from "@shared/components/TablePagination";
import StockAlertBanner from "@features/workshop/components/StockAlertBanner";
import "../../admin/pages/AdminPage.css";
import "./InventoryAdminPage.css";

const SECTION_META = {
  warehouseStructure: {
    title: "Quáº£n lÃ½ cáº¥u trÃºc kho",
    subtitle: "Táº¡o/sá»­a/khÃ³a kho, cáº¥u hÃ¬nh loáº¡i kho, bin/location vÃ  kho máº·c Ä‘á»‹nh",
    hint: "Báº¯t Ä‘áº§u báº±ng viá»‡c táº¡o kho theo tá»«ng chi nhÃ¡nh, sau Ä‘Ã³ gÃ¡n kho máº·c Ä‘á»‹nh cho tá»«ng loáº¡i hÃ ng."
  },
  catalogProducts: {
    title: "Quáº£n lÃ½ sáº£n pháº©m kho",
    subtitle: "Táº¡o/sá»­a sáº£n pháº©m, quáº£n lÃ½ SKU, Ä‘Æ¡n vá»‹ tÃ­nh, hÃ¬nh áº£nh vÃ  tráº¡ng thÃ¡i kinh doanh",
    hint: "Chuáº©n hÃ³a mÃ£ SKU vÃ  danh má»¥c trÆ°á»›c khi nháº­p kho Ä‘á»ƒ trÃ¡nh trÃ¹ng láº·p dá»¯ liá»‡u."
  },
  catalogCategories: {
    title: "Quáº£n lÃ½ danh má»¥c sáº£n pháº©m",
    subtitle: "Táº¡o/sá»­a nhÃ³m hÃ ng Ä‘á»ƒ phÃ¢n loáº¡i váº­t tÆ° rÃµ rÃ ng vÃ  Ä‘á»“ng nháº¥t toÃ n há»‡ thá»‘ng",
    hint: "NÃªn táº¡o cáº¥u trÃºc danh má»¥c ngáº¯n gá»n, dá»… hiá»ƒu Ä‘á»ƒ ngÆ°á»i má»›i tÃ¬m hÃ ng nhanh hÆ¡n."
  },
  catalogSuppliers: {
    title: "Quáº£n lÃ½ nhÃ  cung cáº¥p",
    subtitle: "LÆ°u thÃ´ng tin nhÃ  cung cáº¥p phá»¥c vá»¥ truy xuáº¥t nguá»“n hÃ ng vÃ  Ä‘á»‘i soÃ¡t mua vÃ o",
    hint: "Chuáº©n tÃªn nhÃ  cung cáº¥p ngay tá»« Ä‘áº§u Ä‘á»ƒ trÃ¡nh trÃ¹ng vÃ  dá»… truy váº¥n lá»‹ch sá»­ giao dá»‹ch."
  },
  catalog: {
    title: "Quáº£n lÃ½ sáº£n pháº©m kho",
    subtitle: "Táº¡o/sá»­a sáº£n pháº©m, quáº£n lÃ½ SKU, Ä‘Æ¡n vá»‹ tÃ­nh, hÃ¬nh áº£nh vÃ  tráº¡ng thÃ¡i kinh doanh",
    hint: "Chuáº©n hÃ³a mÃ£ SKU vÃ  danh má»¥c trÆ°á»›c khi nháº­p kho Ä‘á»ƒ trÃ¡nh trÃ¹ng láº·p dá»¯ liá»‡u."
  },
  policy: {
    title: "Cáº¥u hÃ¬nh chÃ­nh sÃ¡ch tá»“n kho",
    subtitle: "Min/Max, ngÆ°á»¡ng cáº£nh bÃ¡o, Ã¢m kho, reserve, vÆ°á»£t Ä‘á»‹nh má»©c, hoÃ n tráº£, chuyá»ƒn kho",
    hint: "Thiáº¿t láº­p chÃ­nh sÃ¡ch chung trÆ°á»›c, sau Ä‘Ã³ Ä‘iá»u chá»‰nh tá»“n an toÃ n riÃªng cho tá»«ng chi nhÃ¡nh."
  },
  monitoring: {
    title: "GiÃ¡m sÃ¡t tá»“n kho toÃ n há»‡ thá»‘ng",
    subtitle: "Theo chi nhÃ¡nh, kho, sáº£n pháº©m, hÃ ng sáº¯p háº¿t, tá»“n cháº­m, reserved, giÃ¡ trá»‹ tá»“n",
    hint: "Theo dÃµi cÃ¡c chá»‰ sá»‘ tá»•ng quan hÃ ng ngÃ y Ä‘á»ƒ phÃ¡t hiá»‡n sá»›m thiáº¿u hÃ ng hoáº·c lá»‡ch tá»“n."
  },
  audits: {
    title: "Quáº£n lÃ½ vÃ  duyá»‡t kiá»ƒm kÃª",
    subtitle: "Theo dÃµi ká»³ kiá»ƒm kÃª, chÃªnh lá»‡ch vÃ  duyá»‡t Ä‘iá»u chá»‰nh sau kiá»ƒm kÃª",
    hint: "Æ¯u tiÃªn xá»­ lÃ½ cÃ¡c phiáº¿u cÃ³ chÃªnh lá»‡ch lá»›n trÆ°á»›c Ä‘á»ƒ giáº£m rá»§i ro sai lá»‡ch tá»“n kho."
  },
  transactions: {
    title: "Lá»‹ch sá»­ nháº­p xuáº¥t vÃ  truy váº¿t",
    subtitle: "Truy váº¿t Ä‘áº§y Ä‘á»§ theo chi nhÃ¡nh/kho/sáº£n pháº©m/loáº¡i giao dá»‹ch vÃ  chá»©ng tá»« nguá»“n",
    hint: "DÃ¹ng bá»™ lá»c theo loáº¡i giao dá»‹ch Ä‘á»ƒ kiá»ƒm tra nhanh cÃ¡c luá»“ng nháº­p/xuáº¥t báº¥t thÆ°á»ng."
  },
  approvals: {
    title: "PhÃª duyá»‡t ngoáº¡i lá»‡ kho",
    subtitle: "Duyá»‡t cÃ¡c case xuáº¥t vÆ°á»£t, Ä‘iá»u chá»‰nh tá»“n, hoÃ n tráº£ báº¥t thÆ°á»ng, kho lá»—i",
    hint: "Chá»‰ duyá»‡t ngoáº¡i lá»‡ khi cÃ³ Ä‘á»§ chá»©ng tá»« vÃ  ghi chÃº rÃµ nguyÃªn nhÃ¢n."
  },
  alerts: {
    title: "Cáº£nh bÃ¡o vÃ  xá»­ lÃ½ block",
    subtitle: "Trung tÃ¢m cáº£nh bÃ¡o tá»“n tháº¥p, Ã¢m kho, chá»©ng tá»« treo, giao dá»‹ch báº¥t thÆ°á»ng",
    hint: "Xá»­ lÃ½ cáº£nh bÃ¡o má»©c cao trÆ°á»›c Ä‘á»ƒ trÃ¡nh áº£nh hÆ°á»Ÿng Ä‘áº¿n hoÃ n táº¥t sá»­a chá»¯a vÃ  xuáº¥t hÃ ng."
  },
  reports: {
    title: "BÃ¡o cÃ¡o quáº£n trá»‹ kho",
    subtitle: "BÃ¡o cÃ¡o tá»“n kho, giÃ¡ trá»‹, luÃ¢n chuyá»ƒn, top xuáº¥t/hoÃ n, chÃªnh lá»‡ch kiá»ƒm kÃª vÃ  audit log",
    hint: "CÃ³ thá»ƒ dÃ¹ng bÃ¡o cÃ¡o nÃ y Ä‘á»ƒ Ä‘á»‘i soÃ¡t ná»™i bá»™ vÃ  chuáº©n bá»‹ há»“ sÆ¡ kiá»ƒm toÃ¡n."
  }
};

const ITEM_TYPE_OPTIONS = [
  { value: "SELLABLE", label: "BÃ¡n Ä‘Æ°á»£c" },
  { value: "DEFECTIVE", label: "Lá»—i/Há»ng" },
  { value: "CONSUMABLE", label: "TiÃªu hao" },
  { value: "TRANSIT", label: "Trung chuyá»ƒn" }
];

const EMPTY_PRODUCT_FORM = {
  sku: "",
  name: "",
  unit: "CÃ¡i",
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
  { value: "STRICT", label: "NghiÃªm ngáº·t (giá»¯ Ä‘Ãºng sá»‘ lÆ°á»£ng Ä‘á»ƒ reserve)" },
  { value: "FLEXIBLE", label: "Linh hoáº¡t (cho phÃ©p Ä‘iá»u phá»‘i khi thiáº¿u)" }
];

const RETURN_RULE_OPTIONS = [
  { value: "ALLOW_WITH_REASON", label: "Cho phÃ©p, báº¯t buá»™c ghi lÃ½ do" },
  { value: "REQUIRE_APPROVAL", label: "Pháº£i duyá»‡t trÆ°á»›c khi hoÃ n" },
  { value: "BLOCK_IF_MISSING_INFO", label: "Cháº·n náº¿u thiáº¿u thÃ´ng tin" }
];

const TRANSFER_RULE_OPTIONS = [
  { value: "REQUIRE_APPROVAL", label: "Pháº£i duyá»‡t khi chuyá»ƒn kho" },
  { value: "AUTO_APPROVE_INTERNAL", label: "Tá»± duyá»‡t trong ná»™i bá»™ chi nhÃ¡nh" },
  { value: "BLOCK_NEGATIVE", label: "Cháº·n náº¿u gÃ¢y Ã¢m kho" }
];

const APPROVAL_STATUS_META = {
  PENDING: { label: "Chá» duyá»‡t", className: "badge-suspended" },
  APPROVED: { label: "ÄÃ£ duyá»‡t", className: "badge-active" },
  REJECTED: { label: "Tá»« chá»‘i", className: "badge-deleted" }
};

const AUDIT_STATUS_META = {
  DRAFT: { label: "NhÃ¡p", className: "badge-inactive" },
  PENDING: { label: "Chá» duyá»‡t", className: "badge-suspended" },
  APPROVED: { label: "ÄÃ£ duyá»‡t", className: "badge-active" },
  REJECTED: { label: "Tá»« chá»‘i", className: "badge-deleted" },
  CLOSED: { label: "ÄÃ£ chá»‘t", className: "badge-active" }
};

const ALERT_SEVERITY_META = {
  CRITICAL: { label: "NghiÃªm trá»ng", className: "badge-deleted" },
  HIGH: { label: "Cao", className: "badge-suspended" },
  MEDIUM: { label: "Trung bÃ¬nh", className: "badge-inactive" },
  LOW: { label: "Tháº¥p", className: "badge-inactive" }
};

const SOURCE_LABELS = {
  PurchaseOrder: "ÄÆ¡n mua hÃ ng",
  RetailOrder: "ÄÆ¡n bÃ¡n láº»",
  RetailReturn: "Tráº£ hÃ ng bÃ¡n láº»",
  MaterialRequest: "Phiáº¿u yÃªu cáº§u váº­t tÆ°",
  MaterialReturn: "Phiáº¿u hoÃ n váº­t tÆ°",
  Unknown: "KhÃ´ng rÃµ nguá»“n"
};

const TRANSACTION_TYPE_LABELS = {
  PURCHASE_RECEIVE: "Nháº­p mua hÃ ng",
  MATERIAL_RETURN: "HoÃ n tráº£ váº­t tÆ°",
  SERVICE_ISSUE: "Xuáº¥t cho lá»‡nh sá»­a chá»¯a",
  RETAIL_ISSUE: "Xuáº¥t bÃ¡n láº»",
  TRANSFER: "Äiá»u chuyá»ƒn kho",
  IN: "Nháº­p kho",
  OUT: "Xuáº¥t kho",
  RETURN: "HoÃ n tráº£"
};

const APPROVAL_TYPE_LABELS = {
  OVER_ISSUE: "Xuáº¥t vÆ°á»£t Ä‘á»‹nh má»©c",
  OVER_SERVICE_ORDER_QTY: "Xuáº¥t vÆ°á»£t sá»‘ lÆ°á»£ng lá»‡nh sá»­a chá»¯a",
  AUDIT_ADJUSTMENT: "Äiá»u chá»‰nh tá»“n sau kiá»ƒm kÃª",
  DEFECTIVE_RETURN: "Nháº­p láº¡i hÃ ng lá»—i",
  DEFECTIVE_TRANSFER: "Chuyá»ƒn hÃ ng sang kho lá»—i",
  DAMAGE_BY_STAFF: "Há»ng hÃ ng do nhÃ¢n viÃªn",
  ABNORMAL_RETURN: "HoÃ n tráº£ báº¥t thÆ°á»ng",
  UNBLOCK_CASE: "Má»Ÿ khÃ³a case bá»‹ block"
};

const ALERT_TYPE_LABELS = {
  LOW_STOCK: "Sáº¯p háº¿t hÃ ng",
  NEGATIVE_STOCK: "Âm kho",
  TX_NO_SOURCE: "Giao dá»‹ch thiáº¿u chá»©ng tá»«",
  RETURN_PENDING: "Phiáº¿u hoÃ n tráº£ treo",
  ISSUE_BLOCKED: "Phiáº¿u xuáº¥t bá»‹ treo",
  AUDIT_GAP: "ChÃªnh lá»‡ch kiá»ƒm kÃª",
  DEFECTIVE_MISMATCH: "Kho lá»—i chÆ°a Ä‘á»“ng bá»™"
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
  if (["ACTIVE", "ENABLED", "OPEN", "HOAT_DONG", "HOáº T_Äá»˜NG"].includes(normalized)) return "Hoáº¡t Ä‘á»™ng";
  if (["INACTIVE", "DISABLED", "CLOSED", "DONG", "ÄÃ“NG"].includes(normalized)) return "Ngá»«ng hoáº¡t Ä‘á»™ng";
  return status || "ChÆ°a cáº¥u hÃ¬nh";
}

function normalizeBranchStatusClass(status) {
  const normalized = normalizeKey(status);
  if (["ACTIVE", "ENABLED", "OPEN", "HOAT_DONG", "HOáº T_Äá»˜NG"].includes(normalized)) return "is-active";
  if (["INACTIVE", "DISABLED", "CLOSED", "DONG", "ÄÃ“NG"].includes(normalized)) return "is-inactive";
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
  return { label: status || "ChÆ°a rÃµ", className: "badge-inactive" };
}

function SectionHeader({ section }) {
  const meta = SECTION_META[section] || {
    title: "Quáº£n trá»‹ kho",
    subtitle: "Theo dÃµi vÃ  cáº¥u hÃ¬nh nghiá»‡p vá»¥ kho",
    hint: "Chá»n menu bÃªn trÃ¡i Ä‘á»ƒ thao tÃ¡c."
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

  const runAction = async (work, fallbackMessage = "KhÃ´ng thá»ƒ thá»±c hiá»‡n thao tÃ¡c.") => {
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
      setError(err?.message || "KhÃ´ng táº£i Ä‘Æ°á»£c dá»¯ liá»‡u quáº£n trá»‹ kho.");
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
      setError("Chá»‰ cháº¥p nháº­n áº£nh JPEG, PNG hoáº·c WEBP.");
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError("áº¢nh khÃ´ng Ä‘Æ°á»£c vÆ°á»£t quÃ¡ 5MB.");
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
      setError("Chá»‰ cháº¥p nháº­n áº£nh JPEG, PNG hoáº·c WEBP.");
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError("áº¢nh khÃ´ng Ä‘Æ°á»£c vÆ°á»£t quÃ¡ 5MB.");
      return;
    }
    setError("");
    setGalleryBusy(true);
    try {
      await inventoryApi.appendProductGalleryImage(editingProduct.id, file);
      await refreshEditProductImages(editingProduct.id);
      await fetchData();
    } catch (err) {
      setError(err?.message || "KhÃ´ng táº£i Ä‘Æ°á»£c áº£nh phá»¥ lÃªn.");
    } finally {
      setGalleryBusy(false);
    }
  };

  const onDeleteProductImageRow = async (imageRow) => {
    if (!editingProduct?.id || !imageRow?.id) return;
    const ok = window.confirm("XÃ³a áº£nh nÃ y? áº¢nh trÃªn cloud váº«n cÃ³ thá»ƒ tá»“n táº¡i náº¿u báº¡n cáº§n dá»n tay trÃªn Azure.");
    if (!ok) return;
    try {
      await inventoryApi.deleteProductImage(imageRow.id);
      await refreshEditProductImages(editingProduct.id);
      await fetchData();
    } catch (err) {
      setError(err?.message || "KhÃ´ng xÃ³a Ä‘Æ°á»£c áº£nh.");
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
      unit: product?.unit || "CÃ¡i",
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
    if (!categoryName) throw new Error("TÃªn danh má»¥c lÃ  báº¯t buá»™c.");

    let parentId = null;
    const rawParentId = String(categoryForm.parentId || "").trim();
    if (rawParentId) {
      const parsedParentId = Number(rawParentId);
      if (!Number.isFinite(parsedParentId) || !Number.isInteger(parsedParentId)) {
        throw new Error("Danh má»¥c cha khÃ´ng há»£p lá»‡.");
      }
      const exists = (categories || []).some((item) => Number(item.id) === parsedParentId);
      if (!exists) throw new Error("Danh má»¥c cha khÃ´ng tá»“n táº¡i.");
      if (editingCategory?.id && Number(editingCategory.id) === parsedParentId) {
        throw new Error("Danh má»¥c cha khÃ´ng thá»ƒ lÃ  chÃ­nh danh má»¥c hiá»‡n táº¡i.");
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
    if (!editingCategory?.id) throw new Error("KhÃ´ng tÃ¬m tháº¥y danh má»¥c cáº§n cáº­p nháº­t.");

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
      throw new Error("SKU, tÃªn sáº£n pháº©m vÃ  Ä‘Æ¡n vá»‹ tÃ­nh lÃ  báº¯t buá»™c.");
    }

    const parseOptionalInt = (rawValue, label) => {
      const parsed = toNullableNumber(rawValue);
      if (parsed == null) return null;
      if (!Number.isInteger(parsed)) {
        throw new Error(`${label} pháº£i lÃ  sá»‘ nguyÃªn.`);
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
      throw new Error("Danh má»¥c khÃ´ng há»£p lá»‡.");
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
      warrantyMonths: parseOptionalInt(productForm.warrantyMonths, "Báº£o hÃ nh"),
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
    if (!editingProduct?.id) throw new Error("KhÃ´ng tÃ¬m tháº¥y sáº£n pháº©m cáº§n cáº­p nháº­t.");

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
    const ok = window.confirm(`XÃ³a sáº£n pháº©m "${product?.name || "-"}" (SKU: ${product?.sku || "-"})?`);
    if (!ok) return;
    await inventoryApi.deleteProduct(product.id);
    await fetchData();
  };

  const handleDeleteCategory = async (category) => {
    const ok = window.confirm(`XÃ³a danh má»¥c "${category?.categoryName || "-"}"?`);
    if (!ok) return;
    await inventoryApi.deletePartCategory(category.id);
    await fetchData();
  };

  const handleEditSupplier = async (supplier) => {
    const currentName = supplier?.supplierName || supplier?.name || "";
    const nextNameInput = window.prompt("TÃªn nhÃ  cung cáº¥p", currentName);
    if (nextNameInput == null) return;
    const nextName = nextNameInput.trim();
    if (!nextName) {
      setError("TÃªn nhÃ  cung cáº¥p khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");
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
    const ok = window.confirm(`XÃ³a nhÃ  cung cáº¥p "${supplierName}"?`);
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
        unit: unit || "CÃ¡i",
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
    await inventoryApi.rejectAudit(id, { note: "Tá»« chá»‘i bá»Ÿi quáº£n trá»‹ viÃªn" });
    await fetchData();
  };

  const handleApproveRequest = async (id) => {
    await inventoryApi.approveRequest(id, {});
    await fetchData();
  };

  const handleRejectRequest = async (id) => {
    await inventoryApi.rejectRequest(id, { note: "Tá»« chá»‘i bá»Ÿi quáº£n trá»‹ viÃªn" });
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
          <p>Äang táº£i dá»¯ liá»‡u kho...</p>
        </div>
      ) : (
        <>
          {section === "warehouseStructure" ? (
            <>
              <StatCards
                items={[
                  { label: "Tá»•ng sá»‘ kho", value: warehouseStructure.warehouses.length },
                  { label: "Sá»‘ bin", value: warehouseStructure.bins.length },
                  { label: "Kho Ä‘ang hoáº¡t Ä‘á»™ng", value: warehouseStructure.warehouses.filter((w) => w.isActive).length }
                ]}
              />
              <ActionPanel
                title="Táº¡o kho má»›i"
                description="Nháº­p thÃ´ng tin cÆ¡ báº£n Ä‘á»ƒ thÃªm kho má»›i theo tá»«ng chi nhÃ¡nh."
              >
                <div className="table-toolbar inventory-form-grid">
                  <label className="compact-field">
                    <span>MÃ£ kho</span>
                    <input
                      className="search-input"
                      placeholder="VÃ­ dá»¥: MAIN-WH"
                      value={form.newWarehouseCode}
                      onChange={(e) => updateForm("newWarehouseCode", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>TÃªn kho</span>
                    <input
                      className="search-input"
                      placeholder="VÃ­ dá»¥: Kho phá»¥ tÃ¹ng chÃ­nh"
                      value={form.newWarehouseName}
                      onChange={(e) => updateForm("newWarehouseName", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Loáº¡i kho</span>
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
                    <span>Chi nhÃ¡nh</span>
                    <select
                      className="search-input"
                      value={form.newWarehouseBranchId}
                      onChange={(e) => updateForm("newWarehouseBranchId", e.target.value)}
                    >
                      <option value="">Chá»n chi nhÃ¡nh</option>
                      {(warehouseStructure.branches || []).map((b) => (
                        <option key={b.id} value={b.id}>{b.name}</option>
                      ))}
                    </select>
                  </label>
                  <button className="btn-primary" onClick={() => runAction(handleCreateWarehouse)}>
                    + Táº¡o kho
                  </button>
                </div>
              </ActionPanel>

              {settings ? (
                <ActionPanel
                  title="GÃ¡n kho máº·c Ä‘á»‹nh theo loáº¡i hÃ ng"
                  description="Há»‡ thá»‘ng sáº½ tá»± Ä‘á»™ng Æ°u tiÃªn kho nÃ y khi phÃ¡t sinh nghiá»‡p vá»¥ tÆ°Æ¡ng á»©ng."
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
                          <option value={0}>-- ChÆ°a chá»n --</option>
                          {warehouseStructure.warehouses.map((w) => (
                            <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
                          ))}
                        </select>
                      </label>
                    ))}
                    <button className="btn-primary" onClick={() => runAction(() => saveSettings(settings))}>
                      LÆ°u kho máº·c Ä‘á»‹nh
                    </button>
                  </div>
                </ActionPanel>
              ) : null}

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>MÃ£ kho</th>
                      <th>TÃªn kho</th>
                      <th>Loáº¡i kho</th>
                      <th>Chi nhÃ¡nh</th>
                      <th>Tráº¡ng thÃ¡i</th>
                      <th>Sá»‘ bin</th>
                      <th>Thao tÃ¡c</th>
                    </tr>
                  </thead>
                  <tbody>
                    {warehouseStructure.warehouses.length === 0 ? (
                      <NoDataRow colSpan={8} message="ChÆ°a cÃ³ kho nÃ o. HÃ£y táº¡o kho Ä‘áº§u tiÃªn Ä‘á»ƒ báº¯t Ä‘áº§u cáº¥u hÃ¬nh." />
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
                              {w.isActive ? "Äang hoáº¡t Ä‘á»™ng" : "ÄÃ£ khÃ³a"}
                            </span>
                          </td>
                          <td>{warehouseStructure.bins.filter((b) => b.warehouseId === w.id).length}</td>
                          <td>
                            <button className="btn-icon" onClick={() => runAction(() => handleToggleWarehouse(w))}>
                              {w.isActive ? "KhÃ³a kho" : "Báº­t kho"}
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
                  { label: "Tá»•ng sáº£n pháº©m", value: products.length },
                  { label: "Danh má»¥c", value: categories.length },
                  { label: "NhÃ  cung cáº¥p", value: suppliers.length }
                ]}
              />

              <ActionPanel
                title="Quáº£n lÃ½ danh má»¥c sáº£n pháº©m"
                description="ThÃªm má»›i sáº£n pháº©m báº±ng popup chi tiáº¿t theo Product model, Ä‘á»“ng thá»i há»— trá»£ import/export CSV."
              >
                <div className="table-toolbar inventory-form-actions">
                  <button className="btn-primary" onClick={openCreateProductModal}>
                    + ThÃªm sáº£n pháº©m
                  </button>
                </div>
                <div className="table-toolbar inventory-form-actions">
                  <button className="btn-ghost" onClick={exportProductsCsv}>Xuáº¥t CSV danh má»¥c</button>
                  <label className="btn-ghost file-label">
                    Nháº­p CSV danh má»¥c
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
                <Modal title={productModalMode === "edit" ? "Sá»­a sáº£n pháº©m" : "ThÃªm sáº£n pháº©m má»›i"} onClose={closeCreateProductModal}>
                  <div className="inventory-product-modal">
                    <p className="inventory-product-modal-desc">
                      Äiá»n thÃ´ng tin sáº£n pháº©m. áº¢nh táº£i lÃªn Azure Blob (cáº¥u hÃ¬nh{" "}
                      <code className="inventory-code-inline">AzureStorage</code>); há»‡ thá»‘ng chá»‰ lÆ°u URL trong{" "}
                      <code className="inventory-code-inline">ProductImages</code>. Äá»‹nh dáº¡ng JPEG/PNG/WEBP, tá»‘i Ä‘a 5MB.
                    </p>

                    <div className="inventory-product-modal-section">
                      <h4>ThÃ´ng tin chung</h4>
                      <div className="inventory-product-modal-grid inventory-product-modal-grid--basic">
                        <label className="compact-field">
                          <span>SKU *</span>
                          <input
                            className="search-input"
                            value={productForm.sku}
                            onChange={(e) => updateProductForm("sku", e.target.value)}
                            placeholder="VÃ­ dá»¥: SKU-0001"
                          />
                        </label>
                        <label className="compact-field">
                          <span>TÃªn sáº£n pháº©m *</span>
                          <input
                            className="search-input"
                            value={productForm.name}
                            onChange={(e) => updateProductForm("name", e.target.value)}
                            placeholder="VÃ­ dá»¥: Lá»c nhÆ¡t Honda"
                          />
                        </label>
                        <label className="compact-field">
                          <span>ÄÆ¡n vá»‹ tÃ­nh *</span>
                          <input
                            className="search-input"
                            value={productForm.unit}
                            onChange={(e) => updateProductForm("unit", e.target.value)}
                            placeholder="CÃ¡i"
                          />
                        </label>
                        <label className="compact-field">
                          <span>Danh má»¥c</span>
                          <select
                            className="search-input"
                            value={productForm.categoryId}
                            onChange={(e) => updateProductForm("categoryId", e.target.value)}
                          >
                            <option value="">-- ChÆ°a gÃ¡n --</option>
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
                      <h4>áº¢nh Part Shop (cloud)</h4>
                      <p className="inventory-product-image-hint">
                        áº¢nh Ä‘áº¡i diá»‡n dÃ¹ng cho catalog / giá» hÃ ng. Nháº¥n <strong>LÆ°u sáº£n pháº©m</strong> Ä‘á»ƒ gá»­i file lÃªn Azure vÃ  cáº­p nháº­t
                        URL trong CSDL. Khi sá»­a, chá»n áº£nh má»›i rá»“i lÆ°u Ä‘á»ƒ thay áº£nh chÃ­nh (ghi Ä‘Ã¨ báº£n ghi Ä‘áº¡i diá»‡n).
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
                          <div className="inventory-product-image-placeholder">ChÆ°a cÃ³ áº£nh</div>
                        )}
                        <div className="inventory-product-image-actions">
                          <label className="btn-ghost inventory-product-image-file">
                            Chá»n áº£nh Ä‘áº¡i diá»‡n
                            <input
                              type="file"
                              accept="image/jpeg,image/png,image/webp"
                              className="inventory-file-input-hidden"
                              onChange={onMainProductImageChange}
                            />
                          </label>
                          {productMainImageFile ? (
                            <button type="button" className="btn-ghost" onClick={clearMainImagePick}>
                              Bá» chá»n file
                            </button>
                          ) : null}
                        </div>
                      </div>

                      {productModalMode === "edit" && editingProduct?.id ? (
                        <div className="inventory-product-gallery-block">
                          <div className="inventory-product-gallery-toolbar">
                            <span className="inventory-product-gallery-title">Gallery (trang chi tiáº¿t)</span>
                            <label
                              className={`btn-ghost inventory-product-image-file ${galleryBusy ? "is-busy" : ""}`}
                            >
                              {galleryBusy ? "Äang táº£i..." : "+ ThÃªm áº£nh phá»¥"}
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
                            <p className="inventory-product-gallery-empty">ChÆ°a cÃ³ áº£nh trong CSDL.</p>
                          ) : (
                            <ul className="inventory-product-gallery-list">
                              {editProductImages.map((img) => (
                                <li key={img.id} className="inventory-product-gallery-item">
                                  <img src={img.imageUrl} alt="" className="inventory-product-gallery-thumb" />
                                  <div className="inventory-product-gallery-meta">
                                    {img.isMain ? (
                                      <span className="badge badge-active">Äáº¡i diá»‡n</span>
                                    ) : (
                                      <span className="badge">Phá»¥</span>
                                    )}
                                    <button
                                      type="button"
                                      className="btn-icon"
                                      onClick={() => onDeleteProductImageRow(img)}
                                      disabled={galleryBusy}
                                    >
                                      XÃ³a
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
                      <h4>GiÃ¡ vÃ  chÃ­nh sÃ¡ch</h4>
                      <div className="inventory-product-modal-grid inventory-product-modal-grid--price">
                        <label className="compact-field">
                          <span>GiÃ¡ vÄ‘n</span>
                          <input
                            className="search-input"
                            type="number"
                            value={productForm.costPrice}
                            onChange={(e) => updateProductForm("costPrice", e.target.value)}
                            placeholder="0"
                          />
                        </label>
                        <label className="compact-field">
                          <span>GiÃ¡ bÃ¡n láº»</span>
                          <input
                            className="search-input"
                            type="number"
                            value={productForm.retailPrice}
                            onChange={(e) => updateProductForm("retailPrice", e.target.value)}
                            placeholder="0"
                          />
                        </label>
                        <label className="compact-field">
                          <span>GiÃ¡ dá»‹ch vá»¥</span>
                          <input
                            className="search-input"
                            type="number"
                            value={productForm.servicePrice}
                            onChange={(e) => updateProductForm("servicePrice", e.target.value)}
                            placeholder="0"
                          />
                        </label>
                        <label className="compact-field">
                          <span>Thuáº¿ (%)</span>
                          <input
                            className="search-input"
                            type="number"
                            value={productForm.taxRate}
                            onChange={(e) => updateProductForm("taxRate", e.target.value)}
                            placeholder="0"
                          />
                        </label>
                        <label className="compact-field">
                          <span>Báº£o hÃ nh (thÃ¡ng)</span>
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
                        <span>Kiá»ƒm soÃ¡t sá»©c chá»©a nghiÃªm ngáº·t</span>
                      </label>
                      <label className="inventory-product-toggle">
                        <input
                          type="checkbox"
                          checked={!!productForm.isActive}
                          onChange={(e) => updateProductForm("isActive", e.target.checked)}
                        />
                        <span>Äang hoáº¡t Ä‘á»™ng</span>
                      </label>
                    </div>

                    <div className="form-actions">
                      <button type="button" className="btn-ghost" onClick={closeCreateProductModal} disabled={savingProduct}>
                        Há»§y
                      </button>
                      <button
                        type="button"
                        className="btn-primary"
                        onClick={() => runAction(productModalMode === "edit" ? handleUpdateProduct : handleCreateProduct)}
                        disabled={savingProduct}
                      >
                        {savingProduct ? "Äang lÆ°u..." : (productModalMode === "edit" ? "Cáº­p nháº­t sáº£n pháº©m" : "LÆ°u sáº£n pháº©m")}
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
                      <th>áº¢nh</th>
                      <th>SKU</th>
                      <th>TÃªn sáº£n pháº©m</th>
                      <th>ÄÆ¡n vá»‹</th>
                      <th>Danh má»¥c</th>
                      <th>GiÃ¡ vÄ‘n</th>
                      <th>GiÃ¡ bÃ¡n láº»</th>
                      <th>GiÃ¡ dá»‹ch vá»¥</th>
                      <th>Thuáº¿ (%)</th>
                      <th>Báº£o hÃ nh (thÃ¡ng)</th>
                      <th>K.S sá»©c chá»©a</th>
                      <th>Tráº¡ng thÃ¡i</th>
                      <th>Thao tÃ¡c</th>
                    </tr>
                  </thead>
                  <tbody>
                    {products.length === 0 ? (
                      <NoDataRow colSpan={14} message="ChÆ°a cÃ³ sáº£n pháº©m trong danh má»¥c kho." />
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
                              <span className="inventory-product-table-thumb-fallback">â€”</span>
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
                          <td>{p.isStrictCapacityControl ? "CÃ³" : "KhÃ´ng"}</td>
                          <td>
                            <span className={`badge ${p.isActive ? "badge-active" : "badge-deleted"}`}>
                              {p.isActive ? "Äang kinh doanh" : "Ngá»«ng kinh doanh"}
                            </span>
                          </td>
                          <td>
                            <div className="row-actions">
                              <button className="btn-icon" onClick={() => openEditProductModal(p)}>
                                Sá»­a
                              </button>
                              <button className="btn-icon" onClick={() => runAction(() => handleDeleteProduct(p))}>
                                XÃ³a
                              </button>
                              {p.isActive ? (
                                <button className="btn-icon" onClick={() => runAction(() => markDiscontinued(p))}>
                                  Ngá»«ng KD
                                </button>
                              ) : (
                                <span className="result-count">ÄÃ£ ngá»«ng</span>
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
                  { label: "Tá»•ng danh má»¥c", value: categories.length },
                  { label: "Sáº£n pháº©m Ä‘Ã£ phÃ¢n loáº¡i", value: products.filter((p) => !!p.categoryId).length },
                  { label: "Sáº£n pháº©m chÆ°a gÃ¡n danh má»¥c", value: products.filter((p) => !p.categoryId).length }
                ]}
              />

              <ActionPanel
                title="Quáº£n lÃ½ danh má»¥c"
                description="ThÃªm vÃ  chá»‰nh sá»­a danh má»¥c báº±ng popup Ä‘á»ƒ chuáº©n hÃ³a dá»¯ liá»‡u phÃ¢n loáº¡i váº­t tÆ°."
              >
                <div className="table-toolbar inventory-form-actions">
                  <button className="btn-primary" onClick={openCreateCategoryModal}>
                    + Táº¡o danh má»¥c
                  </button>
                </div>
              </ActionPanel>

              {showCategoryModal ? (
                <Modal title={categoryModalMode === "edit" ? "Sá»­a danh má»¥c" : "ThÃªm danh má»¥c má»›i"} onClose={closeCategoryModal}>
                  <div className="inventory-product-modal">
                    <p className="inventory-product-modal-desc">
                      Quáº£n lÃ½ nhÃ³m danh má»¥c Ä‘á»ƒ chuáº©n hÃ³a phÃ¢n loáº¡i sáº£n pháº©m trong kho.
                    </p>

                    <div className="inventory-product-modal-section">
                      <h4>ThÃ´ng tin danh má»¥c</h4>
                      <div className="inventory-product-modal-grid inventory-product-modal-grid--basic">
                        <label className="compact-field">
                          <span>TÃªn danh má»¥c *</span>
                          <input
                            className="search-input"
                            value={categoryForm.categoryName}
                            onChange={(e) => updateCategoryForm("categoryName", e.target.value)}
                            placeholder="VÃ­ dá»¥: Dáº§u nhÆ¡t"
                          />
                        </label>
                        <label className="compact-field">
                          <span>Danh má»¥c cha</span>
                          <select
                            className="search-input"
                            value={categoryForm.parentId}
                            onChange={(e) => updateCategoryForm("parentId", e.target.value)}
                          >
                            <option value="">-- KhÃ´ng cÃ³ --</option>
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
                          <span>MÃ´ táº£</span>
                          <input
                            className="search-input"
                            value={categoryForm.description}
                            onChange={(e) => updateCategoryForm("description", e.target.value)}
                            placeholder="MÃ´ táº£ ngáº¯n vá» nhÃ³m danh má»¥c"
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
                        <span>YÃªu cáº§u Ä‘áº·t cá»c</span>
                      </label>
                    </div>

                    <div className="form-actions">
                      <button className="btn-ghost" onClick={closeCategoryModal} disabled={savingCategory}>
                        Há»§y
                      </button>
                      <button
                        className="btn-primary"
                        onClick={() => runAction(categoryModalMode === "edit" ? handleUpdateCategory : handleCreateCategory)}
                        disabled={savingCategory}
                      >
                        {savingCategory ? "Äang lÆ°u..." : (categoryModalMode === "edit" ? "Cáº­p nháº­t danh má»¥c" : "LÆ°u danh má»¥c")}
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
                      <th>TÃªn danh má»¥c</th>
                      <th>Danh má»¥c cha</th>
                      <th>MÃ´ táº£</th>
                      <th>Äáº·t cá»c</th>
                      <th>Sá»‘ sáº£n pháº©m</th>
                      <th>Thao tÃ¡c</th>
                    </tr>
                  </thead>
                  <tbody>
                    {categories.length === 0 ? (
                      <NoDataRow colSpan={7} message="ChÆ°a cÃ³ danh má»¥c nÃ o trong kho." />
                    ) : (
                      pagedCategories.map((category) => (
                        <tr key={category.id}>
                          <td className="td-id">#{category.id}</td>
                          <td>{category.categoryName || "-"}</td>
                          <td>{category.parentId ? (categoryMap.get(category.parentId) || `#${category.parentId}`) : "-"}</td>
                          <td>{category.description || "-"}</td>
                          <td>{category.isDepositRequired ? "CÃ³" : "KhÃ´ng"}</td>
                          <td>{toNumber(productCountByCategory.get(String(category.id)), 0)}</td>
                          <td>
                            <div className="row-actions">
                              <button className="btn-icon" onClick={() => openEditCategoryModal(category)}>
                                Sá»­a
                              </button>
                              <button className="btn-icon" onClick={() => runAction(() => handleDeleteCategory(category))}>
                                XÃ³a
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
                  { label: "Tá»•ng nhÃ  cung cáº¥p", value: suppliers.length },
                  { label: "Sáº£n pháº©m hiá»‡n cÃ³", value: products.length },
                  { label: "Danh má»¥c hiá»‡n cÃ³", value: categories.length }
                ]}
              />

              <ActionPanel
                title="Táº¡o nhÃ  cung cáº¥p má»›i"
                description="Quáº£n lÃ½ danh sÃ¡ch nhÃ  cung cáº¥p Ä‘á»ƒ phá»¥c vá»¥ truy xuáº¥t nguá»“n gá»‘c vÃ  mua hÃ ng."
              >
                <div className="table-toolbar inventory-form-grid inventory-form-grid--small">
                  <label className="compact-field">
                    <span>TÃªn nhÃ  cung cáº¥p</span>
                    <input
                      className="search-input"
                      placeholder="VÃ­ dá»¥: NhÃ  cung cáº¥p A"
                      value={form.supplierName}
                      onChange={(e) => updateForm("supplierName", e.target.value)}
                    />
                  </label>
                  <button className="btn-primary" onClick={() => runAction(handleCreateSupplier)}>
                    + Táº¡o nhÃ  cung cáº¥p
                  </button>
                </div>
              </ActionPanel>

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>NhÃ  cung cáº¥p</th>
                      <th>CÃ´ng ná»£ hiá»‡n táº¡i</th>
                      <th>Thao tÃ¡c</th>
                    </tr>
                  </thead>
                  <tbody>
                    {suppliers.length === 0 ? (
                      <NoDataRow colSpan={4} message="ChÆ°a cÃ³ nhÃ  cung cáº¥p nÃ o trong há»‡ thá»‘ng." />
                    ) : (
                      pagedSuppliers.map((supplier) => (
                        <tr key={supplier.id}>
                          <td className="td-id">#{supplier.id}</td>
                          <td>{supplier.supplierName || supplier.name || "-"}</td>
                          <td>{toNumber(supplier.currentDebt, 0).toLocaleString()}</td>
                          <td>
                            <div className="row-actions">
                              <button className="btn-icon" onClick={() => runAction(() => handleEditSupplier(supplier))}>
                                Sá»­a
                              </button>
                              <button className="btn-icon" onClick={() => runAction(() => handleDeleteSupplier(supplier))}>
                                XÃ³a
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
                  <h3>Thiáº¿t láº­p chÃ­nh sÃ¡ch chung</h3>
                  <p>Ãp dá»¥ng cho toÃ n há»‡ thá»‘ng kho. Cáº¥u hÃ¬nh nÃ y áº£nh hÆ°á»Ÿng Ä‘áº¿n cáº£nh bÃ¡o vÃ  phÃª duyá»‡t.</p>
                </div>
                <div className="policy-grid">
                  <label className="compact-field policy-toggle">
                    <span>Cho phÃ©p Ã¢m kho</span>
                    <input
                      type="checkbox"
                      checked={!!settings.policyRules?.allowNegativeStock}
                      onChange={(e) => updatePolicyField("allowNegativeStock", e.target.checked)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>NgÆ°á»¡ng cáº£nh bÃ¡o tá»“n tháº¥p (%)</span>
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
                    <span>Quy táº¯c giá»¯ hÃ ng (Reserve)</span>
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
                    <span>Cho phÃ©p xuáº¥t vÆ°á»£t Ä‘á»‹nh má»©c</span>
                    <input
                      type="checkbox"
                      checked={!!settings.policyRules?.allowOverIssue}
                      onChange={(e) => updatePolicyField("allowOverIssue", e.target.checked)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Quy táº¯c hoÃ n tráº£ váº­t tÆ°</span>
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
                    <span>Quy táº¯c chuyá»ƒn kho</span>
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
                <div className="policy-overview-item"><span>Tá»•ng chi nhÃ¡nh</span><strong>{policyOverview.branchCount}</strong></div>
                <div className="policy-overview-item"><span>Chi nhÃ¡nh hoáº¡t Ä‘á»™ng</span><strong>{policyOverview.activeBranchCount}</strong></div>
                <div className="policy-overview-item"><span>Tá»“n hiá»‡n táº¡i</span><strong>{policyOverview.totalCurrent.toLocaleString()}</strong></div>
                <div className="policy-overview-item"><span>ÄÃ£ giá»¯ chá»—</span><strong>{policyOverview.totalReserved.toLocaleString()}</strong></div>
                <div className="policy-overview-item"><span>Tá»•ng tá»“n an toÃ n</span><strong>{policyOverview.totalSafeStock.toLocaleString()}</strong></div>
              </div>

              <div className="policy-card">
                <div className="policy-card-head">
                  <h3>Cáº¥u hÃ¬nh tá»“n an toÃ n theo chi nhÃ¡nh</h3>
                  <p>Äáº·t má»©c tá»“n tá»‘i thiá»ƒu riÃªng cho tá»«ng chi nhÃ¡nh Ä‘á»ƒ há»‡ thá»‘ng cáº£nh bÃ¡o sÃ¡t thá»±c táº¿ váº­n hÃ nh.</p>
                </div>
                <div className="table-wrapper policy-table-wrapper">
                  <table className="admin-table">
                    <thead>
                      <tr>
                        <th>Chi nhÃ¡nh</th>
                        <th>Tráº¡ng thÃ¡i</th>
                        <th>Sá»‘ kho</th>
                        <th>Tá»“n hiá»‡n táº¡i</th>
                        <th>ÄÃ£ giá»¯ chá»—</th>
                        <th>Tá»“n an toÃ n</th>
                      </tr>
                    </thead>
                    <tbody>
                      {branchSummary.length === 0 ? (
                        <NoDataRow colSpan={6} message="ChÆ°a cÃ³ dá»¯ liá»‡u chi nhÃ¡nh Ä‘á»ƒ cáº¥u hÃ¬nh tá»“n an toÃ n." />
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
                  LÆ°u chÃ­nh sÃ¡ch
                </button>
              </div>
            </>
          ) : null}

          {section === "monitoring" && overview ? (
            <>
              <StatCards
                items={[
                  { label: "Tá»•ng tá»“n", value: toNumber(overview.totalQuantity).toLocaleString() },
                  { label: "ÄÃ£ giá»¯ chá»—", value: toNumber(overview.reservedQuantity).toLocaleString() },
                  { label: "Sáº¯p háº¿t", value: toNumber(overview.lowStockCount).toLocaleString() },
                  { label: "Âm kho", value: toNumber(overview.negativeStockCount).toLocaleString() },
                  { label: "Kho lá»—i", value: toNumber(overview.defectiveStockCount).toLocaleString() },
                  { label: "GiÃ¡ trá»‹ tá»“n", value: toNumber(overview.totalInventoryValue).toLocaleString() }
                ]}
              />
              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Chi nhÃ¡nh</th>
                      <th>Tá»“n hiá»‡n táº¡i</th>
                      <th>ÄÃ£ giá»¯ chá»—</th>
                      <th>Sáº¯p háº¿t</th>
                      <th>Âm kho</th>
                      <th>GiÃ¡ trá»‹ tá»“n</th>
                    </tr>
                  </thead>
                  <tbody>
                    {branchSummary.length === 0 ? (
                      <NoDataRow colSpan={6} message="ChÆ°a cÃ³ dá»¯ liá»‡u giÃ¡m sÃ¡t tá»“n kho." />
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
                    <th>NgÃ y kiá»ƒm kÃª</th>
                    <th>Chi nhÃ¡nh</th>
                    <th>Kho</th>
                    <th>Tráº¡ng thÃ¡i</th>
                    <th>Tá»•ng chÃªnh lá»‡ch</th>
                    <th>Thao tÃ¡c</th>
                  </tr>
                </thead>
                <tbody>
                  {audits.length === 0 ? (
                    <NoDataRow colSpan={7} message="ChÆ°a cÃ³ ká»³ kiá»ƒm kÃª nÃ o." />
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
                                <button className="btn-icon" onClick={() => runAction(() => handleApproveAudit(a.id))}>Duyá»‡t</button>
                                <button className="btn-icon" onClick={() => runAction(() => handleRejectAudit(a.id))}>Tá»« chá»‘i</button>
                              </div>
                            ) : (
                              <span className="result-count">ÄÃ£ xá»­ lÃ½</span>
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
                title="Bá»™ lá»c truy váº¿t giao dá»‹ch"
                description="Lá»c theo chi nhÃ¡nh, kho, loáº¡i giao dá»‹ch vÃ  khoáº£ng thá»i gian Ä‘á»ƒ truy xuáº¥t nhanh."
              >
                <div className="table-toolbar inventory-form-grid inventory-form-grid--tx">
                  <label className="compact-field">
                    <span>Chi nhÃ¡nh</span>
                    <select
                      className="search-input"
                      value={txParams.branchId}
                      onChange={(e) => {
                        handleTxFilterChange("branchId", e.target.value);
                        handleTxFilterChange("warehouseId", "");
                      }}
                    >
                      <option value="">Táº¥t cáº£ chi nhÃ¡nh</option>
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
                      <option value="">Táº¥t cáº£ kho</option>
                      {warehouseOptionsByBranch.map((w) => (
                        <option key={w.id} value={w.id}>{w.code} - {w.name}</option>
                      ))}
                    </select>
                  </label>
                  <label className="compact-field">
                    <span>Loáº¡i giao dá»‹ch</span>
                    <input
                      className="search-input"
                      placeholder="VÃ­ dá»¥: OUT, SERVICE_ISSUE, RETURN"
                      value={txParams.type}
                      onChange={(e) => handleTxFilterChange("type", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Tá»« ngÃ y</span>
                    <input
                      className="search-input"
                      type="date"
                      value={txParams.from}
                      onChange={(e) => handleTxFilterChange("from", e.target.value)}
                    />
                  </label>
                  <label className="compact-field">
                    <span>Äáº¿n ngÃ y</span>
                    <input
                      className="search-input"
                      type="date"
                      value={txParams.to}
                      onChange={(e) => handleTxFilterChange("to", e.target.value)}
                    />
                  </label>
                </div>
                <div className="table-toolbar inventory-form-actions">
                  <button className="btn-primary" onClick={fetchData}>Lá»c dá»¯ liá»‡u</button>
                  <button className="btn-ghost" onClick={resetTxFilters}>XÃ³a bá»™ lá»c</button>
                </div>
              </ActionPanel>

              <div className="table-wrapper">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Thá»i gian</th>
                      <th>Loáº¡i giao dá»‹ch</th>
                      <th>Chi nhÃ¡nh</th>
                      <th>Kho</th>
                      <th>SKU</th>
                      <th>Sá»‘ lÆ°á»£ng</th>
                      <th>Nguá»“n phÃ¡t sinh</th>
                      <th>NgÆ°á»i thao tÃ¡c</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(transactions.items || []).length === 0 ? (
                      <NoDataRow colSpan={9} message="KhÃ´ng cÃ³ giao dá»‹ch nÃ o khá»›p vá»›i bá»™ lá»c hiá»‡n táº¡i." />
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
                  Trang trÆ°á»›c
                </button>
                <span className="result-count">
                  Trang {txParams.page} / {txTotalPages} - Tá»•ng {toNumber(transactions.total).toLocaleString()} giao dá»‹ch
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
                    <th>Loáº¡i yÃªu cáº§u</th>
                    <th>Tráº¡ng thÃ¡i</th>
                    <th>NgÆ°á»i gá»­i</th>
                    <th>LÃ½ do</th>
                    <th>Thao tÃ¡c</th>
                  </tr>
                </thead>
                <tbody>
                  {approvals.length === 0 ? (
                    <NoDataRow colSpan={6} message="KhÃ´ng cÃ³ yÃªu cáº§u ngoáº¡i lá»‡ nÃ o." />
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
                                <button className="btn-icon" onClick={() => runAction(() => handleApproveRequest(a.id))}>Duyá»‡t</button>
                                <button className="btn-icon" onClick={() => runAction(() => handleRejectRequest(a.id))}>Tá»« chá»‘i</button>
                              </div>
                            ) : (
                              <span className="result-count">ÄÃ£ xá»­ lÃ½</span>
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
                    <th>Má»©c Ä‘á»™</th>
                    <th>Loáº¡i cáº£nh bÃ¡o</th>
                    <th>Ná»™i dung</th>
                    <th>Chi nhÃ¡nh</th>
                    <th>Kho</th>
                    <th>Thá»i gian</th>
                  </tr>
                </thead>
                <tbody>
                  {alerts.length === 0 ? (
                    <NoDataRow colSpan={6} message="Hiá»‡n chÆ°a cÃ³ cáº£nh bÃ¡o tá»“n kho." />
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
                      <th>Chi nhÃ¡nh</th>
                      <th>Tá»•ng tá»“n</th>
                      <th>GiÃ¡ trá»‹ tá»“n</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(reports.stockByBranch || []).length === 0 ? (
                      <NoDataRow colSpan={3} message="ChÆ°a cÃ³ dá»¯ liá»‡u tá»“n kho theo chi nhÃ¡nh." />
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
                      <th>Top sáº£n pháº©m xuáº¥t nhiá»u</th>
                      <th>Sá»‘ lÆ°á»£ng xuáº¥t</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(reports.topOutbound || []).length === 0 ? (
                      <NoDataRow colSpan={2} message="ChÆ°a cÃ³ dá»¯ liá»‡u top xuáº¥t kho." />
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
                      <th>HÃ nh Ä‘á»™ng</th>
                      <th>Báº£ng dá»¯ liá»‡u</th>
                      <th>Báº£n ghi</th>
                      <th>Thá»i gian</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(reports.auditLogs || []).length === 0 ? (
                      <NoDataRow colSpan={5} message="ChÆ°a cÃ³ audit log liÃªn quan Ä‘áº¿n kho." />
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






