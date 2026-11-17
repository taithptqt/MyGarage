import { Navigate, useNavigate } from "react-router-dom";
import { useAuth } from "@shared/core/context/AuthContext";
import {
  createStaff,
  getAllRoles,
  getStaffPage,
  updateStaff
} from "@features/admin/api/staffApi";
import {
  createCustomerVehicle,
  createCustomer,
  createVehicle,
  getAllCustomerTiers,
  getCustomerPage,
  updateVehicle,
  updateCustomerVehicle,
  updateCustomer
} from "@features/admin/api/customerApi";
import {
  createVehicleBrand,
  createVehicleModel,
  deleteVehicleBrand,
  deleteVehicleModel,
  getAllVehicleBrands,
  getVehicleBrandPage,
  getVehicleModelPage,
  getAllVehicleModels,
  updateVehicleBrand,
  updateVehicleModel
} from "@features/admin/api/vehicleCatalogApi";
import {
  createStandardService,
  deleteStandardService,
  getAllServiceCategories,
  getStandardServicePage,
  updateStandardService
} from "@features/admin/api/serviceApi";
import {
  createBranch,
  getAllBranches,
  getBranchPage,
  updateBranch
} from "@features/admin/api/branchApi";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearch } from "@shared/core/context/SearchContext";
import { useBranch } from "@shared/core/context/BranchContext";
import { isOptionalEmailValid, isValidVietnamesePhone } from "@shared/utils/contactValidation";
import { isValidVietnameseLicensePlate } from "@shared/utils/licensePlateValidation";
import { formatLicensePlateByFieldName } from "@shared/utils/licensePlateInputFormatter";
import TablePagination from "@shared/components/TablePagination";
import "./AdminPage.css";
import {
  EMPTY_BRANCH_FORM,
  EMPTY_BRAND_FORM,
  EMPTY_CUSTOMER_FORM,
  EMPTY_MODEL_FORM,
  EMPTY_SERVICE_FORM,
  EMPTY_STAFF_FORM,
  SECTION_META,
  getStatusBadge,
  getStatusLabel,
  isTargetBusinessRole,
  normalizeText
} from "./adminPageHelpers";
import {
  BranchForm,
  ConfirmDialog,
  CustomerForm,
  Modal,
  ServiceForm,
  StaffForm,
  VehicleBrandForm,
  VehicleModelForm
} from "./AdminPageForms";



export default function AdminPage({ embedded = false, initialSection = "staff" }) {
  const { user, isAuthenticated, loading: authLoading, logout } = useAuth();
  const navigate = useNavigate();
  const {
    selectedBranchId,
    selectedBranch,
    branches: globalBranches,
    refreshBranches: refreshGlobalBranches
  } = useBranch();
  const [section, setSection] = useState(initialSection);
  const [branches, setBranches] = useState([]);
  const [staff, setStaff] = useState([]);
  const [staffTotal, setStaffTotal] = useState(0);
  const [staffStats, setStaffStats] = useState({ totalStaff: 0, activeStaff: 0, rolesUsed: 0 });
  const [staffLoading, setStaffLoading] = useState(false);
  const [staffError, setStaffError] = useState("");
  const staffLoadSequence = useRef(0);
  const [customers, setCustomers] = useState([]);
  const [customerTotal, setCustomerTotal] = useState(0);
  const [customerStats, setCustomerStats] = useState({ totalCustomers: 0, activeCustomers: 0 });
  const [customerLoading, setCustomerLoading] = useState(false);
  const [customerError, setCustomerError] = useState("");
  const customerLoadSequence = useRef(0);
  const [customerTiers, setCustomerTiers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [brands, setBrands] = useState([]);
  const [brandRows, setBrandRows] = useState([]);
  const referenceLoadSequence = useRef(0);
  const [brandTotal, setBrandTotal] = useState(0);
  const [brandStats, setBrandStats] = useState({ totalBrands: 0, activeBrands: 0, inactiveBrands: 0 });
  const [brandLoading, setBrandLoading] = useState(false);
  const [brandError, setBrandError] = useState("");
  const brandLoadSequence = useRef(0);
  const [models, setModels] = useState([]);
  const [branchRows, setBranchRows] = useState([]);
  const [branchTotal, setBranchTotal] = useState(0);
  const [branchStats, setBranchStats] = useState({ totalBranches: 0, activeBranches: 0 });
  const [branchLoading, setBranchLoading] = useState(false);
  const [branchError, setBranchError] = useState("");
  const branchLoadSequence = useRef(0);
  const [modelRows, setModelRows] = useState([]);
  const [modelTotal, setModelTotal] = useState(0);
  const [modelStats, setModelStats] = useState({ totalModels: 0, brandCount: 0, activeModels: 0 });
  const [modelLoading, setModelLoading] = useState(false);
  const [modelError, setModelError] = useState("");
  const modelLoadSequence = useRef(0);
  const [serviceRows, setServiceRows] = useState([]);
  const [serviceTotal, setServiceTotal] = useState(0);
  const [serviceStats, setServiceStats] = useState({ totalServices: 0, activeServices: 0 });
  const [serviceLoading, setServiceLoading] = useState(false);
  const [serviceError, setServiceError] = useState("");
  const serviceLoadSequence = useRef(0);

  const [serviceCategories, setServiceCategories] = useState([]);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [formError, setFormError] = useState("");

  const { searchQuery } = useSearch();
  const [staffSearch, setStaffSearch] = useState("");
  const [modal, setModal] = useState(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  useEffect(() => {
    const timer = window.setTimeout(() => setStaffSearch(searchQuery), 250);
    return () => window.clearTimeout(timer);
  }, [searchQuery]);

  useEffect(() => {
    setSection(initialSection || "staff");
    setModal(null);
    setFormError("");
  }, [initialSection]);

  useEffect(() => {
    if (!globalBranches?.length) return;
    setBranches(globalBranches);
  }, [globalBranches]);

  const fetchBranches = useCallback(async () => setBranches((await getAllBranches()) || []), []);
  const fetchStaff = useCallback(async () => {
    const sequence = ++staffLoadSequence.current;
    setStaffLoading(true);
    setStaffError("");
    try {
      const result = await getStaffPage({ branchId: selectedBranchId || undefined, search: staffSearch || undefined, page, pageSize });
      if (sequence !== staffLoadSequence.current) return;
      setStaff(Array.isArray(result?.items) ? result.items : []);
      setStaffTotal(Number(result?.total) || 0);
      setStaffStats({ totalStaff: result?.totalStaff || 0, activeStaff: result?.activeStaff || 0, rolesUsed: result?.rolesUsed || 0 });
    } catch (apiError) {
      if (sequence === staffLoadSequence.current) setStaffError(apiError.message || "Failed to load admin data.");
    } finally {
      if (sequence === staffLoadSequence.current) setStaffLoading(false);
    }
  }, [selectedBranchId, staffSearch, page, pageSize]);
  const fetchCustomers = useCallback(async () => {
    const sequence = ++customerLoadSequence.current;
    setCustomerLoading(true);
    setCustomerError("");
    try {
      const result = await getCustomerPage({ branchId: selectedBranchId || undefined, search: staffSearch || undefined, page, pageSize });
      if (sequence !== customerLoadSequence.current) return;
      setCustomers((result?.items || []).map(({ customer, ...details }) => ({ ...customer, ...details })));
      setCustomerTotal(Number(result?.total) || 0);
      setCustomerStats({ totalCustomers: result?.totalCustomers || 0, activeCustomers: result?.activeCustomers || 0 });
    } catch (apiError) {
      if (sequence === customerLoadSequence.current) setCustomerError(apiError.message || "Failed to load admin data.");
    } finally {
      if (sequence === customerLoadSequence.current) setCustomerLoading(false);
    }
  }, [selectedBranchId, staffSearch, page, pageSize]);
  const fetchCustomerTiers = useCallback(async () => setCustomerTiers((await getAllCustomerTiers()) || []), []);
  const fetchRoles = useCallback(async () => setRoles((await getAllRoles()) || []), []);
  const fetchBrands = useCallback(async () => setBrands((await getAllVehicleBrands()) || []), []);
  const fetchBrandPage = useCallback(async () => {
    const sequence = ++brandLoadSequence.current;
    setBrandLoading(true);
    setBrandError("");
    try {
      const result = await getVehicleBrandPage({ search: staffSearch || undefined, page, pageSize });
      if (sequence !== brandLoadSequence.current) return;
      setBrandRows(Array.isArray(result?.items) ? result.items : []);
      setBrandTotal(Number(result?.total) || 0);
      setBrandStats({ totalBrands: result?.totalBrands || 0, activeBrands: result?.activeBrands || 0, inactiveBrands: result?.inactiveBrands || 0 });
    } catch (apiError) {
      if (sequence === brandLoadSequence.current) setBrandError(apiError.message || "Failed to load admin data.");
    } finally {
      if (sequence === brandLoadSequence.current) setBrandLoading(false);
    }
  }, [staffSearch, page, pageSize]);
  const fetchModels = useCallback(async () => setModels((await getAllVehicleModels()) || []), []);
  const fetchServiceCategories = useCallback(
    async () => setServiceCategories((await getAllServiceCategories()) || []),
    []
  );
  const fetchBranchPage = useCallback(async () => {
    const sequence = ++branchLoadSequence.current;
    setBranchLoading(true); setBranchError("");
    try {
      const result = await getBranchPage({ search: staffSearch || undefined, page, pageSize });
      if (sequence !== branchLoadSequence.current) return;
      setBranchRows(Array.isArray(result?.items) ? result.items : []);
      setBranchTotal(Number(result?.total) || 0);
      setBranchStats({ totalBranches: result?.totalBranches || 0, activeBranches: result?.activeBranches || 0 });
    } catch (apiError) {
      if (sequence === branchLoadSequence.current) setBranchError(apiError.message || "Failed to load admin data.");
    } finally { if (sequence === branchLoadSequence.current) setBranchLoading(false); }
  }, [staffSearch, page, pageSize]);
  const fetchModelPage = useCallback(async () => {
    const sequence = ++modelLoadSequence.current;
    setModelLoading(true); setModelError("");
    try {
      const result = await getVehicleModelPage({ search: staffSearch || undefined, page, pageSize });
      if (sequence !== modelLoadSequence.current) return;
      setModelRows(Array.isArray(result?.items) ? result.items : []);
      setModelTotal(Number(result?.total) || 0);
      setModelStats({ totalModels: result?.totalModels || 0, brandCount: result?.brandCount || 0, activeModels: result?.activeModels || 0 });
    } catch (apiError) {
      if (sequence === modelLoadSequence.current) setModelError(apiError.message || "Failed to load admin data.");
    } finally { if (sequence === modelLoadSequence.current) setModelLoading(false); }
  }, [staffSearch, page, pageSize]);
  const fetchServicePage = useCallback(async () => {
    const sequence = ++serviceLoadSequence.current;
    setServiceLoading(true); setServiceError("");
    try {
      const result = await getStandardServicePage({ branchId: selectedBranchId || undefined, search: staffSearch || undefined, page, pageSize });
      if (sequence !== serviceLoadSequence.current) return;
      setServiceRows(Array.isArray(result?.items) ? result.items : []);
      setServiceTotal(Number(result?.total) || 0);
      setServiceStats({ totalServices: result?.totalServices || 0, activeServices: result?.activeServices || 0 });
    } catch (apiError) {
      if (sequence === serviceLoadSequence.current) setServiceError(apiError.message || "Failed to load admin data.");
    } finally { if (sequence === serviceLoadSequence.current) setServiceLoading(false); }
  }, [selectedBranchId, staffSearch, page, pageSize]);

  const fetchAll = useCallback(async () => {
    const sequence = ++referenceLoadSequence.current;
    setLoading(true);
    setError("");
    try {
      if (section === "staff") await Promise.all([fetchBranches(), fetchRoles()]);
      else if (section === "customers") await Promise.all([fetchBranches(), fetchCustomerTiers(), fetchBrands(), fetchModels()]);
      else if (section === "models") await fetchBrands();
      else if (section === "services") await fetchServiceCategories();
    } catch (apiError) { if (sequence === referenceLoadSequence.current) setError(apiError.message || "Failed to load admin data."); }
    finally { if (sequence === referenceLoadSequence.current) setLoading(false); }
  }, [section, fetchBranches, fetchBrands, fetchCustomerTiers, fetchModels, fetchServiceCategories, fetchRoles]);

  useEffect(() => {
    if (isAuthenticated) fetchAll();
    return () => { referenceLoadSequence.current += 1; };
  }, [fetchAll, isAuthenticated]);

  useEffect(() => {
    if (isAuthenticated && section === "staff") fetchStaff();
    return () => { staffLoadSequence.current += 1; };
  }, [fetchStaff, isAuthenticated, section]);

  useEffect(() => {
    if (isAuthenticated && section === "customers") fetchCustomers();
    return () => { customerLoadSequence.current += 1; };
  }, [fetchCustomers, isAuthenticated, section]);

  useEffect(() => {
    if (isAuthenticated && section === "brands") fetchBrandPage();
    return () => { brandLoadSequence.current += 1; };
  }, [fetchBrandPage, isAuthenticated, section]);

  useEffect(() => {
    if (isAuthenticated && section === "branches") fetchBranchPage();
    return () => { branchLoadSequence.current += 1; };
  }, [fetchBranchPage, isAuthenticated, section]);
  useEffect(() => {
    if (isAuthenticated && section === "models") fetchModelPage();
    return () => { modelLoadSequence.current += 1; };
  }, [fetchModelPage, isAuthenticated, section]);
  useEffect(() => {
    if (isAuthenticated && section === "services") fetchServicePage();
    return () => { serviceLoadSequence.current += 1; };
  }, [fetchServicePage, isAuthenticated, section]);

  // NOTE: early returns will come AFTER all hooks below

  const roleNameMap = useMemo(() => {
    const map = new Map();
    (roles || []).forEach((role) => map.set(role.id, role.roleName || role.roleCode || `Vai trò #${role.id}`));
    return map;
  }, [roles]);

  const customerTierOptions = useMemo(() => {
    return (customerTiers || []).filter((tier) => tier?.isDeleted !== true);
  }, [customerTiers]);

  const serviceCategoryOptions = useMemo(() => {
    return (serviceCategories || []).filter((item) => item?.isDeleted !== true && item?.isActive !== false);
  }, [serviceCategories]);

  const staffRoleOptions = useMemo(() => {
    const activeRoles = (roles || []).filter((role) => role?.isDeleted !== true && role?.isActive !== false);
    const selectedRoles = activeRoles.filter(isTargetBusinessRole);
    return selectedRoles.length > 0 ? selectedRoles : activeRoles;
  }, [roles]);

  const brandNameMap = useMemo(() => {
    const map = new Map();
    (brands || []).forEach((brand) => map.set(brand.id, brand.name || brand.code || `Hãng #${brand.id}`));
    return map;
  }, [brands]);

  const serviceCategoryNameMap = useMemo(() => {
    const map = new Map();
    (serviceCategories || []).forEach((item) => {
      map.set(item.id, item.name || item.code || `Nhóm #${item.id}`);
    });
    return map;
  }, [serviceCategories]);

  const visibleRows = section === "branches" ? branchRows : section === "staff" ? staff : section === "customers" ? customers : section === "brands" ? brandRows : section === "models" ? modelRows : serviceRows;
  const visibleTotal = section === "branches" ? branchTotal : section === "staff" ? staffTotal : section === "customers" ? customerTotal : section === "brands" ? brandTotal : section === "models" ? modelTotal : serviceTotal;
  const totalPages = Math.max(1, Math.ceil(visibleTotal / pageSize));
  const pagedRows = visibleRows;

  useEffect(() => {
    setPage(1);
  }, [section, searchQuery, pageSize, selectedBranchId]);

  useEffect(() => {
    if (page > totalPages) {
      setPage(totalPages);
    }
  }, [page, totalPages]);

  if (authLoading) return <div className="loading-state"><div className="spinner" /><p>Đang xác thực...</p></div>;
  if (!isAuthenticated) return <Navigate to="/login" replace />;

  const openModal = (entity, mode, item = null) => {
    setFormError("");
    setModal({ entity, mode, item });
  };

  const closeModal = () => {
    setFormError("");
    setModal(null);
  };

  const handleLogout = async () => {
    await logout();
    navigate("/login", { replace: true });
  };

  const handleCreateClick = () => {
    if (section === "branches") return openModal("branch", "create");
    if (section === "staff") return openModal("staff", "create");
    if (section === "customers") return openModal("customer", "create");
    if (section === "brands") return openModal("brand", "create");
    if (section === "models") return openModal("model", "create");
    return openModal("service", "create");
  };

  const reloadSectionData = async (entity) => {
    if (entity === "branch") {
      await Promise.all([fetchBranchPage(), refreshGlobalBranches()]);
      return;
    }
    if (entity === "staff") return Promise.all([fetchStaff(), fetchRoles()]);
    if (entity === "customer") {
      return Promise.all([
        fetchCustomers(),
        fetchCustomerTiers(),
        fetchBrands(),
        fetchModels()
      ]);
    }
    if (entity === "brand") return fetchBrandPage();
    if (entity === "model") return fetchModelPage();
    if (entity === "service") return Promise.all([fetchServicePage(), fetchServiceCategories()]);
    return fetchModels();
  };

  const handleSave = async (formData) => {
    if (!modal) return;
    setSaving(true);
    setFormError("");

    try {
      if (modal.entity === "branch") {
        const hotline = String(formData.hotline || "").trim();
        const email = String(formData.email || "").trim();
        if (hotline && !isValidVietnamesePhone(hotline)) {
          throw new Error("Hotline chi nhánh phải đúng định dạng Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx).");
        }
        if (email && !isOptionalEmailValid(email)) {
          throw new Error("Email chi nhánh không đúng định dạng.");
        }

        const payload = {
          name: formData.name,
          address: formData.address || null,
          hotline: hotline || null,
          email: email || null,
          status: formData.status || "active",
          maxWorkSlots: formData.maxWorkSlots ?? null,
          description: formData.description || null
        };

        if (modal.mode === "create") await createBranch(payload);
        else await updateBranch(modal.item.id, payload);
      } else if (modal.entity === "staff") {
        if (!selectedBranchId) {
          throw new Error("Vui lòng chọn chi nhánh trước khi thêm/cập nhật nhân sự.");
        }
        const phone = String(formData.phone || "").trim();
        const email = String(formData.email || "").trim();
        if (!isValidVietnamesePhone(phone)) {
          throw new Error("Số điện thoại nhân viên phải đúng định dạng Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx).");
        }
        if (email && !isOptionalEmailValid(email)) {
          throw new Error("Email nhân viên không đúng định dạng.");
        }

        const payload = {
          fullName: formData.fullName,
          phone,
          email: email || null,
          roleId: formData.roleId || null,
          status: formData.status || "active",
          branchId: selectedBranchId || null
        };
        if (formData.password?.trim()) payload.password = formData.password.trim();

        if (modal.mode === "create") await createStaff(payload);
        else await updateStaff(modal.item.id, payload);
      } else if (modal.entity === "customer") {
        const phone = String(formData.phone || "").trim();
        const email = String(formData.email || "").trim();
        if (!isValidVietnamesePhone(phone)) {
          throw new Error("Số điện thoại khách hàng phải đúng định dạng Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx).");
        }
        if (email && !isOptionalEmailValid(email)) {
          throw new Error("Email khách hàng không đúng định dạng.");
        }

        const payload = {
          fullName: formData.fullName,
          phone,
          email: email || null,
          tierId: formData.tierId || null,
          note: formData.note,
          isActive: formData.isActive === true || formData.isActive === "true"
        };
        if (formData.password?.trim()) payload.password = formData.password.trim();

        const formattedLicensePlate = formatLicensePlateByFieldName("licensePlate", formData.licensePlate || "");
        if (formattedLicensePlate && !isValidVietnameseLicensePlate(formattedLicensePlate)) {
          throw new Error("Biển số xe không đúng định dạng Việt Nam.");
        }

        const hasVehicleInput = Boolean(formattedLicensePlate || Number(formData.modelId));
        if (hasVehicleInput && !formattedLicensePlate) {
          throw new Error("Vui lòng nhập biển số xe.");
        }
        if (hasVehicleInput && !formData.modelId) {
          throw new Error("Vui lòng chọn dòng xe khi nhập thông tin xe.");
        }

        let savedCustomer = null;
        if (modal.mode === "create") savedCustomer = await createCustomer(payload);
        else savedCustomer = await updateCustomer(modal.item.id, payload);

        if (hasVehicleInput) {
          const vehiclePayload = {
            licensePlate: formattedLicensePlate,
            modelId: Number(formData.modelId),
            color: String(formData.color || "").trim() || null,
            manufactureYear: formData.manufactureYear || null
          };

          const customerId = modal.mode === "create" ? savedCustomer?.id : modal.item.id;
          const existingLink = modal.mode === "edit" && modal.item.customerVehicleId && modal.item.vehicleId
            ? { id: modal.item.customerVehicleId, vehicleId: modal.item.vehicleId }
            : null;

          if (existingLink) {
            await updateVehicle(existingLink.vehicleId, vehiclePayload);
            await updateCustomerVehicle(existingLink.id, {
              customerId: modal.item.id,
              vehicleId: existingLink.vehicleId,
              isCurrentOwner: true,
              note: formData.note || null
            });
          } else if (customerId) {
            const createdVehicle = await createVehicle(vehiclePayload);
            await createCustomerVehicle({
              customerId,
              vehicleId: createdVehicle.id,
              isCurrentOwner: true,
              note: formData.note || null
            });
          }
        }
      } else if (modal.entity === "brand") {
        if (modal.mode === "create") await createVehicleBrand(formData);
        else await updateVehicleBrand(modal.item.id, formData);
      } else if (modal.entity === "model") {
        if (!formData.brandId || Number.isNaN(formData.brandId)) throw new Error("Vui lòng chọn hãng xe.");

        if (modal.mode === "create") await createVehicleModel(formData);
        else await updateVehicleModel(modal.item.id, formData);
      } else {
        if (!formData.categoryId || Number.isNaN(formData.categoryId)) {
          throw new Error("Vui lòng chọn nhóm dịch vụ.");
        }
        if (!formData.standardMinutes || Number(formData.standardMinutes) <= 0) {
          throw new Error("Thời lượng dự kiến phải lớn hơn 0.");
        }

        const payload = {
          code: formData.code,
          name: formData.name,
          categoryId: Number(formData.categoryId),
          basePrice: formData.basePrice ?? null,
          standardMinutes: Number(formData.standardMinutes),
          description: formData.description || null,
          isActive: formData.isActive === true || formData.isActive === "true"
        };

        if (modal.mode === "create") await createStandardService(payload);
        else await updateStandardService(modal.item.id, payload);
      }

      await reloadSectionData(modal.entity);
      closeModal();
    } catch (apiError) {
      setFormError(apiError.message || "Lưu dữ liệu thất bại.");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!modal?.item) return;
    setSaving(true);
    setFormError("");

    try {
      if (modal.entity === "brand") await deleteVehicleBrand(modal.item.id);
      else if (modal.entity === "model") await deleteVehicleModel(modal.item.id);
      else await deleteStandardService(modal.item.id);

      await reloadSectionData(modal.entity);
      closeModal();
    } catch (apiError) {
      setFormError(apiError.message || "Xóa dữ liệu thất bại.");
    } finally {
      setSaving(false);
    }
  };

  const sectionMeta = SECTION_META[section];

  return (
    <div className={`admin-page ${embedded ? "embedded" : ""}`}>
      {!embedded && (
        <aside className="admin-sidebar">
            <div className="sidebar-brand">
              <span className="brand-name">MyGarage</span>
            </div>

            <nav className="sidebar-nav">
              <button className={`nav-item ${section === "branches" ? "active" : ""}`} type="button" onClick={() => { setSection("branches"); }}>
                Chi nhánh
              </button>
              <button className={`nav-item ${section === "staff" ? "active" : ""}`} type="button" onClick={() => { setSection("staff"); }}>
                Nhân sự
              </button>
              <button className={`nav-item ${section === "customers" ? "active" : ""}`} type="button" onClick={() => { setSection("customers"); }}>
                Khách hàng
              </button>
              <button className={`nav-item ${section === "brands" ? "active" : ""}`} type="button" onClick={() => { setSection("brands"); }}>
                Hãng xe
              </button>
              <button className={`nav-item ${section === "models" ? "active" : ""}`} type="button" onClick={() => { setSection("models"); }}>
                Dòng xe
              </button>
              <button className={`nav-item ${section === "services" ? "active" : ""}`} type="button" onClick={() => { setSection("services"); }}>
                Dịch vụ
              </button>
              <button className="nav-item" type="button" onClick={handleLogout}>
                Đăng xuất
              </button>
            </nav>
        </aside>
      )}

      <main className="admin-main">
        <header className="admin-header">
          <div>
            <h1>{sectionMeta.title}</h1>
            <p className="header-sub">{sectionMeta.subtitle}</p>
            {selectedBranch?.name && section !== "branches" && (
              <p className="header-sub">Chi nhánh đang chọn: {selectedBranch.name}</p>
            )}
          </div>
          <button className="btn-primary" onClick={handleCreateClick} type="button">
            {sectionMeta.addLabel}
          </button>
        </header>

        <div className="stats-bar">
          {section === "branches" && (
            <>
              <div className="stat-card"><div className="stat-value">{branchStats.totalBranches}</div><div className="stat-label">Tổng chi nhánh</div></div>
              <div className="stat-card"><div className="stat-value">{branchStats.activeBranches}</div><div className="stat-label">Đang hoạt động</div></div>
              <div className="stat-card"><div className="stat-value">{selectedBranch?.name || "-"}</div><div className="stat-label">Chi nhánh đã chọn</div></div>
            </>
          )}

          {section === "staff" && (
            <>
              <div className="stat-card"><div className="stat-value">{staffStats.totalStaff}</div><div className="stat-label">Tổng nhân sự</div></div>
              <div className="stat-card"><div className="stat-value">{staffStats.activeStaff}</div><div className="stat-label">Đang hoạt động</div></div>
              <div className="stat-card"><div className="stat-value">{staffStats.rolesUsed}</div><div className="stat-label">Số vai trò sử dụng</div></div>
            </>
          )}

          {section === "customers" && (
            <>
              <div className="stat-card"><div className="stat-value">{customerStats.totalCustomers}</div><div className="stat-label">Tổng khách hàng</div></div>
              <div className="stat-card"><div className="stat-value">{customerStats.activeCustomers}</div><div className="stat-label">Đang hoạt động</div></div>
              <div className="stat-card"><div className="stat-value">{customerTierOptions.length}</div><div className="stat-label">Số hạng khách hàng</div></div>
            </>
          )}

          {section === "brands" && (
            <>
              <div className="stat-card"><div className="stat-value">{brandStats.totalBrands}</div><div className="stat-label">Tổng hãng xe</div></div>
              <div className="stat-card"><div className="stat-value">{brandStats.activeBrands}</div><div className="stat-label">Đang hoạt động</div></div>
              <div className="stat-card"><div className="stat-value">{brandStats.inactiveBrands}</div><div className="stat-label">Ngừng hoạt động</div></div>
            </>
          )}

          {section === "models" && (
            <>
              <div className="stat-card"><div className="stat-value">{modelStats.totalModels}</div><div className="stat-label">Tổng dòng xe</div></div>
              <div className="stat-card"><div className="stat-value">{modelStats.brandCount}</div><div className="stat-label">Số hãng có dòng xe</div></div>
              <div className="stat-card"><div className="stat-value">{modelStats.activeModels}</div><div className="stat-label">Dòng xe đang dùng</div></div>
            </>
          )}

          {section === "services" && (
            <>
              <div className="stat-card"><div className="stat-value">{serviceStats.totalServices}</div><div className="stat-label">Tổng dịch vụ</div></div>
              <div className="stat-card"><div className="stat-value">{serviceStats.activeServices}</div><div className="stat-label">Đang hoạt động</div></div>
              <div className="stat-card"><div className="stat-value">{serviceCategoryOptions.length}</div><div className="stat-label">Số nhóm dịch vụ</div></div>
            </>
          )}
        </div>

        <div className="table-toolbar">
          <span className="result-count">{visibleTotal} kết quả</span>
        </div>

        {(error || (section === "staff" && staffError) || (section === "customers" && customerError) || (section === "brands" && brandError) || (section === "branches" && branchError) || (section === "models" && modelError) || (section === "services" && serviceError)) && <div className="alert alert-error">{error || (section === "staff" ? staffError : section === "customers" ? customerError : section === "brands" ? brandError : section === "branches" ? branchError : section === "models" ? modelError : serviceError)}</div>}

        <div className="table-wrapper">
          {(loading || (section === "staff" && staffLoading) || (section === "customers" && customerLoading) || (section === "brands" && brandLoading) || (section === "branches" && branchLoading) || (section === "models" && modelLoading) || (section === "services" && serviceLoading)) ? (
            <div className="loading-state"><div className="spinner" /><p>Đang tải dữ liệu...</p></div>
          ) : visibleRows.length === 0 ? (
            <div className="empty-state"><p>Không có dữ liệu phù hợp.</p></div>
          ) : (
            <table className="admin-table">
              {section === "branches" && (
                <>
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Tên chi nhánh</th>
                      <th>Địa chỉ</th>
                      <th>Hotline</th>
                      <th>Email</th>
                      <th>Trạng thái</th>
                      <th>Số ô tối đa</th>
                      <th className="th-center">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pagedRows.map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td>
                        <td>{item.name || "-"}</td>
                        <td>{item.address || "-"}</td>
                        <td>{item.hotline || "-"}</td>
                        <td className="td-email">{item.email || "-"}</td>
                        <td><span className={`badge ${getStatusBadge(item.status)}`}>{getStatusLabel(item.status)}</span></td>
                        <td>{item.maxWorkSlots ?? "-"}</td>
                        <td className="td-center">
                          <div className="row-actions">
                            <button className="btn-icon btn-edit" type="button" onClick={() => openModal("branch", "edit", item)}>Sửa</button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}

              {section === "staff" && (
                <>
                  <thead><tr><th>ID</th><th>Họ và tên</th><th>Số điện thoại</th><th>Email</th><th>Vai trò</th><th>Trạng thái</th><th>Ngày tạo</th><th className="th-center">Thao tác</th></tr></thead>
                  <tbody>
                    {pagedRows.map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td><td>{item.fullName || "-"}</td><td>{item.phone || "-"}</td><td className="td-email">{item.email || "-"}</td>
                        <td>{roleNameMap.get(item.roleId) || "-"}</td>
                        <td><span className={`badge ${getStatusBadge(item.status)}`}>{getStatusLabel(item.status)}</span></td>
                        <td>{item.createdAt ? new Date(item.createdAt).toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
                        <td className="td-center"><div className="row-actions"><button className="btn-icon btn-edit" type="button" onClick={() => openModal("staff", "edit", item)}>Sửa</button></div></td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}

              {section === "customers" && (
                <>
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Họ và tên</th>
                      <th>Số điện thoại</th>
                      <th>Email</th>
                      <th>Hạng</th>
                      <th>Biển số</th>
                      <th>Hãng xe</th>
                      <th>Dòng xe</th>
                      <th>Trạng thái</th>
                      <th>Ngày tạo</th>
                      <th className="th-center">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {pagedRows.map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td><td>{item.fullName || "-"}</td><td>{item.phone || "-"}</td><td className="td-email">{item.email || "-"}</td>
                        <td>{item.tierName || "-"}</td>
                        <td>{item.licensePlate || "-"}</td>
                        <td>{item.brandName || "-"}</td>
                        <td>{item.modelName || "-"}</td>
                        <td><span className={`badge ${getStatusBadge(item.isActive)}`}>{item.isActive ? "đang hoạt động" : "ngừng hoạt động"}</span></td>
                        <td>{item.createdAt ? new Date(item.createdAt).toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
                        <td className="td-center"><div className="row-actions"><button className="btn-icon btn-edit" type="button" onClick={() => openModal("customer", "edit", item)}>Sửa</button></div></td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}

              {section === "brands" && (
                <>
                  <thead><tr><th>ID</th><th>Mã</th><th>Tên hãng</th><th>Quốc gia</th><th>Trạng thái</th><th>Cập nhật</th><th className="th-center">Thao tác</th></tr></thead>
                  <tbody>
                    {pagedRows.map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td><td>{item.code || "-"}</td><td>{item.name || "-"}</td><td>{item.countryOfOrigin || "-"}</td>
                        <td><span className={`badge ${getStatusBadge(item.isActive)}`}>{item.isActive ? "đang hoạt động" : "ngừng hoạt động"}</span></td>
                        <td>{item.updatedAt ? new Date(item.updatedAt).toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "-"}</td>
                        <td className="td-center"><div className="row-actions"><button className="btn-icon btn-edit" type="button" onClick={() => openModal("brand", "edit", item)}>Sửa</button><button className="btn-icon btn-del" type="button" onClick={() => openModal("brand", "delete", item)}>Xóa</button></div></td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}

              {section === "models" && (
                <>
                  <thead><tr><th>ID</th><th>Hãng xe</th><th>Dòng xe</th><th>Phân khúc</th><th>Nhiên liệu</th><th>Dung tích xi-lanh</th><th className="th-center">Thao tác</th></tr></thead>
                  <tbody>
                    {pagedRows.map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td><td>{brandNameMap.get(item.brandId) || `Hãng #${item.brandId}`}</td><td>{item.modelName || "-"}</td><td>{item.segment || "-"}</td><td>{item.fuelType || "-"}</td><td>{item.cylinderCapacity || "-"}</td>
                        <td className="td-center"><div className="row-actions"><button className="btn-icon btn-edit" type="button" onClick={() => openModal("model", "edit", item)}>Sửa</button><button className="btn-icon btn-del" type="button" onClick={() => openModal("model", "delete", item)}>Xóa</button></div></td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}

              {section === "services" && (
                <>
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Mã</th>
                      <th>Tên dịch vụ</th>
                      <th>Nhóm dịch vụ</th>
                      <th>Giá</th>
                      <th>Thời lượng</th>
                      <th>Trạng thái</th>
                      <th className="th-center">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(pagedRows || []).map((item) => (
                      <tr key={item.id}>
                        <td className="td-id">#{item.id}</td>
                        <td>{item.code || "-"}</td>
                        <td>{item.name || "-"}</td>
                        <td>{(serviceCategoryNameMap && serviceCategoryNameMap.get ? serviceCategoryNameMap.get(item.categoryId) : null) || `Nhóm #${item.categoryId}`}</td>
                        <td>{item.basePrice != null ? Number(item.basePrice).toLocaleString() : "-"}</td>
                        <td>{item.standardMinutes ? `${item.standardMinutes} phút` : "-"}</td>
                        <td><span className={`badge ${getStatusBadge(item.isActive)}`}>{item.isActive ? "đang hoạt động" : "ngừng hoạt động"}</span></td>
                        <td className="td-center"><div className="row-actions"><button className="btn-icon btn-edit" type="button" onClick={() => openModal("service", "edit", item)}>Sửa</button><button className="btn-icon btn-del" type="button" onClick={() => openModal("service", "delete", item)}>Xóa</button></div></td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}
            </table>
          )}
        </div>

        <TablePagination
          totalItems={visibleTotal}
          currentPage={page}
          pageSize={pageSize}
          onPageChange={setPage}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setPage(1);
          }}
        />
      </main>

      {modal?.mode === "create" && (
        <Modal title={modal.entity === "branch" ? "Thêm chi nhánh" : modal.entity === "staff" ? "Thêm nhân sự" : modal.entity === "customer" ? "Thêm khách hàng" : modal.entity === "brand" ? "Thêm hãng xe" : modal.entity === "model" ? "Thêm dòng xe" : "Thêm dịch vụ"} onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}

          {modal.entity === "branch" && <BranchForm initial={EMPTY_BRANCH_FORM} onSubmit={handleSave} onCancel={closeModal} loading={saving} />}
          {modal.entity === "staff" && <StaffForm initial={EMPTY_STAFF_FORM} roles={staffRoleOptions} onSubmit={handleSave} onCancel={closeModal} loading={saving} />}
          {modal.entity === "customer" && (
            <CustomerForm
              initial={EMPTY_CUSTOMER_FORM}
              tiers={customerTierOptions}
              brands={brands}
              models={models}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}
          {modal.entity === "brand" && <VehicleBrandForm initial={EMPTY_BRAND_FORM} onSubmit={handleSave} onCancel={closeModal} loading={saving} />}
          {modal.entity === "model" && <VehicleModelForm initial={EMPTY_MODEL_FORM} brands={brands} onSubmit={handleSave} onCancel={closeModal} loading={saving} />}
          {modal.entity === "service" && <ServiceForm initial={EMPTY_SERVICE_FORM} categories={serviceCategoryOptions} onSubmit={handleSave} onCancel={closeModal} loading={saving} />}
        </Modal>
      )}

      {modal?.mode === "edit" && modal.item && (
        <Modal title={modal.entity === "branch" ? `Sửa chi nhánh #${modal.item.id}` : modal.entity === "staff" ? `Sửa nhân sự #${modal.item.id}` : modal.entity === "customer" ? `Sửa khách hàng #${modal.item.id}` : modal.entity === "brand" ? `Sửa hãng xe #${modal.item.id}` : modal.entity === "model" ? `Sửa dòng xe #${modal.item.id}` : `Sửa dịch vụ #${modal.item.id}`} onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}

          {modal.entity === "branch" && (
            <BranchForm
              initial={{
                id: modal.item.id,
                name: modal.item.name || "",
                address: modal.item.address || "",
                hotline: modal.item.hotline || "",
                email: modal.item.email || "",
                status: modal.item.status || "active",
                maxWorkSlots: modal.item.maxWorkSlots ?? "",
                description: modal.item.description || ""
              }}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}

          {modal.entity === "staff" && (
            <StaffForm
              initial={{ id: modal.item.id, fullName: modal.item.fullName || "", phone: modal.item.phone || "", email: modal.item.email || "", password: "", roleId: modal.item.roleId ? String(modal.item.roleId) : "", status: modal.item.status || "active" }}
              roles={staffRoleOptions}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}

          {modal.entity === "customer" && (
            <CustomerForm
              initial={{
                id: modal.item.id,
                fullName: modal.item.fullName || "",
                phone: modal.item.phone || "",
                email: modal.item.email || "",
                password: "",
                tierId: modal.item.tierId ? String(modal.item.tierId) : "",
                brandId: modal.item.brandId ? String(modal.item.brandId) : "",
                modelId: modal.item.modelId ? String(modal.item.modelId) : "",
                licensePlate: modal.item.licensePlate && modal.item.licensePlate !== "-" ? modal.item.licensePlate : "",
                color: modal.item.color || "",
                manufactureYear: modal.item.manufactureYear || "",
                note: modal.item.note || "",
                isActive: modal.item.isActive ?? true
              }}
              tiers={customerTierOptions}
              brands={brands}
              models={models}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}

          {modal.entity === "brand" && (
            <VehicleBrandForm
              initial={{ id: modal.item.id, code: modal.item.code || "", name: modal.item.name || "", countryOfOrigin: modal.item.countryOfOrigin || "", logoUrl: modal.item.logoUrl || "", isActive: modal.item.isActive ?? true }}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}

          {modal.entity === "model" && (
            <VehicleModelForm
              initial={{ id: modal.item.id, brandId: modal.item.brandId, modelName: modal.item.modelName || "", segment: modal.item.segment || "", fuelType: modal.item.fuelType || "", cylinderCapacity: modal.item.cylinderCapacity || "" }}
              brands={brands}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}

          {modal.entity === "service" && (
            <ServiceForm
              initial={{
                id: modal.item.id,
                code: modal.item.code || "",
                name: modal.item.name || "",
                categoryId: modal.item.categoryId ? String(modal.item.categoryId) : "",
                basePrice: modal.item.basePrice ?? "",
                standardMinutes: modal.item.standardMinutes ?? "",
                description: modal.item.description || "",
                isActive: modal.item.isActive ?? true
              }}
              categories={serviceCategoryOptions}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}
        </Modal>
      )}

      {modal?.mode === "delete" && modal.item && (
        <Modal title="Xác nhận xóa" onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}
          <ConfirmDialog message={`Bạn có chắc muốn xóa bản ghi ${modal.entity} (#${modal.item.id})?`} onConfirm={handleDelete} onCancel={closeModal} loading={saving} />
        </Modal>
      )}
    </div>
  );
}
