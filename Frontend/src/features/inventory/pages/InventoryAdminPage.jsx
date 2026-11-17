import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { inventoryApi } from "@features/inventory/api/inventoryApi";
import StockAlertBanner from "@features/workshop/components/StockAlertBanner";
import "../../admin/pages/AdminPage.css";
import "./InventoryAdminPage.css";
import {
  EMPTY_CATEGORY_FORM,
  EMPTY_PRODUCT_FORM,
  csvEscape,
  normalizeKey,
  paginateItems,
  toNullableNumber,
  toNumber
} from "./inventoryAdminHelpers";
import { SectionHeader } from "./InventoryAdminSharedWidgets";
import {
  CatalogCategoriesSection,
  CatalogProductsSection,
  CatalogSuppliersSection,
  WarehouseStructureSection
} from "./InventoryCatalogSections";
import {
  AlertsSection,
  ApprovalsSection,
  AuditsSection,
  MonitoringSection,
  PolicySection,
  ReportsSection,
  TransactionsSection
} from "./InventoryOperationsSections";

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
  const [categoryOptions, setCategoryOptions] = useState([]);
  const [categoryOptionsReady, setCategoryOptionsReady] = useState(false);
  const [catalogSummary, setCatalogSummary] = useState({ productCount: 0, classified: 0, unclassified: 0, categoryCount: 0, supplierCount: 0 });
  const catalogRequestSequence = useRef(0);

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

  const runAction = async (work, fallbackMessage = "Không thể thực hiện thao tác.") => {
    setError("");
    try {
      await work();
    } catch (err) {
      setError(err?.message || fallbackMessage);
    }
  };

  const fetchData = useCallback(async () => {
    const sequence = ++catalogRequestSequence.current;
    setLoading(true);
    setError("");
    try {
      if (section === "warehouseStructure") {
        const [structure, branchData, cfg] = await Promise.all([
          inventoryApi.getWarehouseStructure({ page: warehousePage, pageSize: warehousePageSize }),
          inventoryApi.getBranches(),
          inventoryApi.getSettings()
        ]);
        setWarehouseStructure({
          warehouses: structure?.warehouses || [],
          bins: structure?.bins || [],
          branches: branchData || [],
          total: structure?.total ?? (structure?.warehouses || []).length
        });
        setSettings(cfg);
      }

      if (isCatalogSection) {
        const kind = isCatalogProductsSection ? "products" : isCatalogCategoriesSection ? "categories" : "suppliers";
        const page = kind === "products" ? productPage : kind === "categories" ? categoryPage : supplierPage;
        const size = kind === "products" ? productPageSize : kind === "categories" ? categoryPageSize : supplierPageSize;
        const result = await inventoryApi.getCatalogPage(kind, page, size);
        if (sequence !== catalogRequestSequence.current) return;
        const lastPage = Math.max(1, Math.ceil((result?.total || 0) / size));
        if (page > lastPage) {
          if (kind === "products") setProductPage(lastPage);
          else if (kind === "categories") setCategoryPage(lastPage);
          else setSupplierPage(lastPage);
          return;
        }
        setCatalogSummary(result?.summary || {});
        if (kind === "products") {
          setProducts((result?.items || []).map((row) => ({ ...row.item, _categoryName: row.categoryName })));
          setProductThumbById(Object.fromEntries((result?.items || []).filter((row) => row.thumbnail).map((row) => [row.item.id, row.thumbnail])));
        } else if (kind === "categories") {
          setCategories((result?.items || []).map((row) => ({ ...row.item, _parentName: row.parentName, _productCount: row.productCount })));
        } else {
          setSuppliers(result?.items || []);
        }
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
      if (sequence === catalogRequestSequence.current) setError(err?.message || "Không tải được dữ liệu quản trị kho.");
    } finally {
      if (sequence === catalogRequestSequence.current) setLoading(false);
    }
  }, [section, txParams, isCatalogSection, isCatalogProductsSection, isCatalogCategoriesSection, productPage, productPageSize, categoryPage, categoryPageSize, supplierPage, supplierPageSize, warehousePage, warehousePageSize]);

  useEffect(() => {
    fetchData();
    return () => { catalogRequestSequence.current += 1; };
  }, [fetchData]);

  useEffect(() => {
    if (!showCreateProductModal && !showCategoryModal) return undefined;
    let cancelled = false;
    setCategoryOptionsReady(false);
    setCategoryOptions([]);
    inventoryApi.getPartCategories().then((rows) => { if (!cancelled) { setCategoryOptions(rows || []); setCategoryOptionsReady(true); } })
      .catch((err) => { if (!cancelled) setError(err?.message || "Không tải được dữ liệu quản trị kho."); });
    return () => { cancelled = true; };
  }, [showCreateProductModal, showCategoryModal]);

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
  const pagedWarehouses = warehouseStructure.warehouses || [];
  const pagedProducts = products;
  const pagedCategories = categories;
  const pagedSuppliers = suppliers;
  const reportStockByBranchRows = reports?.stockByBranch || [];
  const pagedReportStockByBranch = useMemo(
    () => paginateItems(reportStockByBranchRows, reportBranchPage, reportBranchPageSize),
    [reportStockByBranchRows, reportBranchPage, reportBranchPageSize]
  );
  const reportTopOutboundRows = reports?.topOutbound || [];
  const pagedReportTopOutbound = useMemo(
    () => paginateItems(reportTopOutboundRows, reportTopOutboundPage, reportTopOutboundPageSize),
    [reportTopOutboundRows, reportTopOutboundPage, reportTopOutboundPageSize]
  );
  const reportAuditLogRows = reports?.auditLogs || [];
  const pagedReportAuditLogs = useMemo(
    () => paginateItems(reportAuditLogRows, reportAuditLogPage, reportAuditLogPageSize),
    [reportAuditLogRows, reportAuditLogPage, reportAuditLogPageSize]
  );

  useEffect(() => {
    const total = warehouseStructure.total ?? (warehouseStructure.warehouses || []).length;
    const maxPage = Math.max(1, Math.ceil(total / (warehousePageSize || 10)));
    if (warehousePage > maxPage) setWarehousePage(maxPage);
  }, [warehouseStructure.total, warehouseStructure.warehouses, warehousePage, warehousePageSize]);

  useEffect(() => {
    const maxPage = Math.max(1, Math.ceil(reportStockByBranchRows.length / (reportBranchPageSize || 10)));
    if (reportBranchPage > maxPage) setReportBranchPage(maxPage);
  }, [reportStockByBranchRows.length, reportBranchPage, reportBranchPageSize]);

  useEffect(() => {
    const maxPage = Math.max(1, Math.ceil(reportTopOutboundRows.length / (reportTopOutboundPageSize || 10)));
    if (reportTopOutboundPage > maxPage) setReportTopOutboundPage(maxPage);
  }, [reportTopOutboundRows.length, reportTopOutboundPage, reportTopOutboundPageSize]);

  useEffect(() => {
    const maxPage = Math.max(1, Math.ceil(reportAuditLogRows.length / (reportAuditLogPageSize || 10)));
    if (reportAuditLogPage > maxPage) setReportAuditLogPage(maxPage);
  }, [reportAuditLogRows.length, reportAuditLogPage, reportAuditLogPageSize]);

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
      setError("Chỉ chấp nhận ảnh JPEG, PNG hoặc WEBP.");
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError("Ảnh không được vượt quá 5MB.");
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
      setError("Chỉ chấp nhận ảnh JPEG, PNG hoặc WEBP.");
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError("Ảnh không được vượt quá 5MB.");
      return;
    }
    setError("");
    setGalleryBusy(true);
    try {
      await inventoryApi.appendProductGalleryImage(editingProduct.id, file);
      await refreshEditProductImages(editingProduct.id);
      await fetchData();
    } catch (err) {
      setError(err?.message || "Không tải được ảnh phụ lên.");
    } finally {
      setGalleryBusy(false);
    }
  };

  const onDeleteProductImageRow = async (imageRow) => {
    if (!editingProduct?.id || !imageRow?.id) return;
    const ok = window.confirm("Xóa ảnh này? Ảnh trên cloud vẫn có thể tồn tại nếu bạn cần dọn tay trên Azure.");
    if (!ok) return;
    try {
      await inventoryApi.deleteProductImage(imageRow.id);
      await refreshEditProductImages(editingProduct.id);
      await fetchData();
    } catch (err) {
      setError(err?.message || "Không xóa được ảnh.");
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
    if (!categoryName) throw new Error("Tên danh mục là bắt buộc.");

    let parentId = null;
    const rawParentId = String(categoryForm.parentId || "").trim();
    if (rawParentId) {
      const parsedParentId = Number(rawParentId);
      if (!Number.isFinite(parsedParentId) || !Number.isInteger(parsedParentId)) {
        throw new Error("Danh mục cha không hợp lệ.");
      }
      const exists = categoryOptions.some((item) => Number(item.id) === parsedParentId);
      if (!exists) throw new Error("Danh mục cha không tồn tại.");
      if (editingCategory?.id && Number(editingCategory.id) === parsedParentId) {
        throw new Error("Danh mục cha không thể là chính danh mục hiện tại.");
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
    if (!editingCategory?.id) throw new Error("Không tìm thấy danh mục cần cập nhật.");

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
      throw new Error("SKU, tên sản phẩm và đơn vị tính là bắt buộc.");
    }

    const parseOptionalInt = (rawValue, label) => {
      const parsed = toNullableNumber(rawValue);
      if (parsed == null) return null;
      if (!Number.isInteger(parsed)) {
        throw new Error(`${label} phải là số nguyên.`);
      }
      return parsed;
    };

    const parseOptionalDecimal = (rawValue) => {
      const parsed = toNullableNumber(rawValue);
      if (parsed == null) return null;
      return parsed;
    };

    const categoryId = productForm.categoryId ? Number(productForm.categoryId) : null;
    if (categoryId != null && !categoryOptions.some((item) => Number(item.id) === categoryId)) {
      throw new Error("Danh mục không hợp lệ.");
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
      warrantyMonths: parseOptionalInt(productForm.warrantyMonths, "Bảo hành"),
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
    if (!editingProduct?.id) throw new Error("Không tìm thấy sản phẩm cần cập nhật.");

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
    const ok = window.confirm(`Xóa sản phẩm "${product?.name || "-"}" (SKU: ${product?.sku || "-"})?`);
    if (!ok) return;
    await inventoryApi.deleteProduct(product.id);
    await fetchData();
  };

  const handleDeleteCategory = async (category) => {
    const ok = window.confirm(`Xóa danh mục "${category?.categoryName || "-"}"?`);
    if (!ok) return;
    await inventoryApi.deletePartCategory(category.id);
    await fetchData();
  };

  const handleEditSupplier = async (supplier) => {
    const currentName = supplier?.supplierName || supplier?.name || "";
    const nextNameInput = window.prompt("Tên nhà cung cấp", currentName);
    if (nextNameInput == null) return;
    const nextName = nextNameInput.trim();
    if (!nextName) {
      setError("Tên nhà cung cấp không được để trống.");
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
    const ok = window.confirm(`Xóa nhà cung cấp "${supplierName}"?`);
    if (!ok) return;
    await inventoryApi.deleteSupplier(supplier.id);
    await fetchData();
  };

  const exportProductsCsv = async () => {
    const blob = await inventoryApi.exportCatalogProducts();
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `danh-muc-kho-${new Date().toISOString().substring(0, 10)}.csv`;
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
      <StockAlertBanner maxItems={6} />
      {error ? <div className="alert alert-error">{error}</div> : null}

      {loading ? (
        <div className="loading-state">
          <div className="spinner" />
          <p>Đang tải dữ liệu kho...</p>
        </div>
      ) : (
        <>
          {section === "warehouseStructure" ? (
            <WarehouseStructureSection
              warehouseStructure={warehouseStructure}
              form={form}
              updateForm={updateForm}
              runAction={runAction}
              handleCreateWarehouse={handleCreateWarehouse}
              settings={settings}
              setSettings={setSettings}
              saveSettings={saveSettings}
              pagedWarehouses={pagedWarehouses}
              handleToggleWarehouse={handleToggleWarehouse}
              warehousePage={warehousePage}
              warehousePageSize={warehousePageSize}
              setWarehousePage={setWarehousePage}
              setWarehousePageSize={setWarehousePageSize}
            />
          ) : null}

          {isCatalogProductsSection ? (
            <CatalogProductsSection
              catalogSummary={catalogSummary}
              openCreateProductModal={openCreateProductModal}
              runAction={runAction}
              exportProductsCsv={exportProductsCsv}
              importProductsCsv={importProductsCsv}
              showCreateProductModal={showCreateProductModal}
              productModalMode={productModalMode}
              closeCreateProductModal={closeCreateProductModal}
              productForm={productForm}
              updateProductForm={updateProductForm}
              categoryOptions={categoryOptions}
              productMainImagePreview={productMainImagePreview}
              editProductImages={editProductImages}
              productMainImageFile={productMainImageFile}
              onMainProductImageChange={onMainProductImageChange}
              clearMainImagePick={clearMainImagePick}
              editingProduct={editingProduct}
              galleryBusy={galleryBusy}
              onGalleryImageSelected={onGalleryImageSelected}
              onDeleteProductImageRow={onDeleteProductImageRow}
              savingProduct={savingProduct}
              categoryOptionsReady={categoryOptionsReady}
              handleUpdateProduct={handleUpdateProduct}
              handleCreateProduct={handleCreateProduct}
              products={products}
              pagedProducts={pagedProducts}
              productThumbById={productThumbById}
              openEditProductModal={openEditProductModal}
              handleDeleteProduct={handleDeleteProduct}
              markDiscontinued={markDiscontinued}
              productPage={productPage}
              productPageSize={productPageSize}
              setProductPage={setProductPage}
              setProductPageSize={setProductPageSize}
            />
          ) : null}

          {isCatalogCategoriesSection ? (
            <CatalogCategoriesSection
              catalogSummary={catalogSummary}
              openCreateCategoryModal={openCreateCategoryModal}
              showCategoryModal={showCategoryModal}
              categoryModalMode={categoryModalMode}
              closeCategoryModal={closeCategoryModal}
              categoryForm={categoryForm}
              updateCategoryForm={updateCategoryForm}
              categoryOptions={categoryOptions}
              editingCategory={editingCategory}
              savingCategory={savingCategory}
              categoryOptionsReady={categoryOptionsReady}
              runAction={runAction}
              handleUpdateCategory={handleUpdateCategory}
              handleCreateCategory={handleCreateCategory}
              categories={categories}
              pagedCategories={pagedCategories}
              openEditCategoryModal={openEditCategoryModal}
              handleDeleteCategory={handleDeleteCategory}
              categoryPage={categoryPage}
              categoryPageSize={categoryPageSize}
              setCategoryPage={setCategoryPage}
              setCategoryPageSize={setCategoryPageSize}
            />
          ) : null}

          {isCatalogSuppliersSection ? (
            <CatalogSuppliersSection
              catalogSummary={catalogSummary}
              form={form}
              updateForm={updateForm}
              runAction={runAction}
              handleCreateSupplier={handleCreateSupplier}
              suppliers={suppliers}
              pagedSuppliers={pagedSuppliers}
              handleEditSupplier={handleEditSupplier}
              handleDeleteSupplier={handleDeleteSupplier}
              supplierPage={supplierPage}
              supplierPageSize={supplierPageSize}
              setSupplierPage={setSupplierPage}
              setSupplierPageSize={setSupplierPageSize}
            />
          ) : null}

          {section === "policy" && settings ? (
            <PolicySection
              settings={settings}
              updatePolicyField={updatePolicyField}
              policyOverview={policyOverview}
              branchSummary={branchSummary}
              updateSafeStockBranch={updateSafeStockBranch}
              runAction={runAction}
              saveSettings={saveSettings}
            />
          ) : null}

          {section === "monitoring" && overview ? (
            <MonitoringSection overview={overview} branchSummary={branchSummary} />
          ) : null}

          {section === "audits" ? (
            <AuditsSection
              audits={audits}
              runAction={runAction}
              handleApproveAudit={handleApproveAudit}
              handleRejectAudit={handleRejectAudit}
            />
          ) : null}

          {section === "transactions" ? (
            <TransactionsSection
              txParams={txParams}
              handleTxFilterChange={handleTxFilterChange}
              warehouseStructure={warehouseStructure}
              warehouseOptionsByBranch={warehouseOptionsByBranch}
              fetchData={fetchData}
              resetTxFilters={resetTxFilters}
              transactions={transactions}
              changePage={changePage}
              txTotalPages={txTotalPages}
            />
          ) : null}

          {section === "approvals" ? (
            <ApprovalsSection
              approvals={approvals}
              runAction={runAction}
              handleApproveRequest={handleApproveRequest}
              handleRejectRequest={handleRejectRequest}
            />
          ) : null}

          {section === "alerts" ? <AlertsSection alerts={alerts} /> : null}

          {section === "reports" && reports ? (
            <ReportsSection
              reports={reports}
              pagedReportStockByBranch={pagedReportStockByBranch}
              reportStockByBranchRows={reportStockByBranchRows}
              reportBranchPage={reportBranchPage}
              reportBranchPageSize={reportBranchPageSize}
              setReportBranchPage={setReportBranchPage}
              setReportBranchPageSize={setReportBranchPageSize}
              pagedReportTopOutbound={pagedReportTopOutbound}
              reportTopOutboundRows={reportTopOutboundRows}
              reportTopOutboundPage={reportTopOutboundPage}
              reportTopOutboundPageSize={reportTopOutboundPageSize}
              setReportTopOutboundPage={setReportTopOutboundPage}
              setReportTopOutboundPageSize={setReportTopOutboundPageSize}
              pagedReportAuditLogs={pagedReportAuditLogs}
              reportAuditLogRows={reportAuditLogRows}
              reportAuditLogPage={reportAuditLogPage}
              reportAuditLogPageSize={reportAuditLogPageSize}
              setReportAuditLogPage={setReportAuditLogPage}
              setReportAuditLogPageSize={setReportAuditLogPageSize}
            />
          ) : null}
        </>
      )}
    </div>
  );
}










