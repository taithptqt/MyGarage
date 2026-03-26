import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useSearch } from "@context/SearchContext";
import { 
  getAllCustomers, 
  getAllCustomerTiers, 
  getAllCustomerVehicles, 
  getAllVehicles, 
  createCustomer, 
  updateCustomer, 
  deleteCustomer, 
  createVehicle, 
  updateVehicle, 
  createCustomerVehicle, 
  updateCustomerVehicle, 
  deleteCustomerVehicle 
} from "../api/customerApi";
import { Modal, ConfirmDialog } from "./AdminModals";
import { CustomerForm } from "./AdminForms";
import { getStatusBadge } from "../utils/adminUtils";
import { EMPTY_CUSTOMER_FORM } from "../constants/adminConstants";
import TablePagination from "../../../shared/components/TablePagination";

export default function CustomerManagement() {
  const [customers, setCustomers] = useState([]);
  const [tiers, setTiers] = useState([]);
  const [customerVehicles, setCustomerVehicles] = useState([]);
  const [vehicles, setVehicles] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [modal, setModal] = useState(null);
  const [formError, setFormError] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const { searchQuery } = useSearch();

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [cRes, tRes, cvRes, vRes] = await Promise.all([
        getAllCustomers(),
        getAllCustomerTiers(),
        getAllCustomerVehicles(),
        getAllVehicles()
      ]);
      setCustomers(cRes || []);
      setTiers(tRes || []);
      setCustomerVehicles(cvRes || []);
      setVehicles(vRes || []);
    } catch (err) {
      setError(err.message || "Failed to load customer data.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Logic maps for data enrichment
  const tierNameMap = useMemo(() => {
    const map = new Map();
    (tiers || []).forEach(t => map.set(t.id, t.tierName || `Tier #${t.id}`));
    return map;
  }, [tiers]);

  const vehicleMap = useMemo(() => {
    const map = new Map();
    (vehicles || []).forEach(v => map.set(v.id, v));
    return map;
  }, [vehicles]);

  const primaryVehicleByCustomerId = useMemo(() => {
    const map = new Map();
    (customerVehicles || []).filter(v => v?.isDeleted !== true).forEach(link => {
      if (!map.get(link.customerId) || link.isCurrentOwner) {
        map.set(link.customerId, link);
      }
    });
    return map;
  }, [customerVehicles]);

  const enrichedData = useMemo(() => {
    return (customers || []).map(customer => {
      const link = primaryVehicleByCustomerId.get(customer.id);
      const vehicle = link ? vehicleMap.get(link.vehicleId) : null;
      return {
        ...customer,
        tierName: tierNameMap.get(customer.tierId) || "-",
        licensePlate: vehicle?.licensePlate || "-",
        vehicleId: vehicle?.id || null,
        linkId: link?.id || null
      };
    });
  }, [customers, primaryVehicleByCustomerId, tierNameMap, vehicleMap]);

  const filteredData = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return enrichedData;
    return enrichedData.filter(item => (
      (item.fullName || "").toLowerCase().includes(query) ||
      (item.email || "").toLowerCase().includes(query) ||
      (item.phone || "").toLowerCase().includes(query) ||
      (item.googleId || "").toLowerCase().includes(query) ||
      (item.type || "").toLowerCase().includes(query) ||
      (item.licensePlate || "").toLowerCase().includes(query)
    ));
  }, [enrichedData, searchQuery]);

  const pagedData = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredData.slice(start, start + pageSize);
  }, [filteredData, currentPage, pageSize]);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, customers.length]);

  const openModal = (mode, item = null) => {
    setFormError("");
    setModal({ mode, item });
  };

  const closeModal = () => {
    setFormError("");
    setModal(null);
  };

  const handleSave = async (formData) => {
    setSaving(true);
    setFormError("");
    try {
      const payload = { ...formData };
      if (!payload.password?.trim()) delete payload.password;

      let savedCustomer;
      if (modal.mode === "create") savedCustomer = await createCustomer(payload);
      else savedCustomer = await updateCustomer(modal.item.id, payload);

      // Handle vehicle logic if provided
      const plate = String(formData.licensePlate || "").trim();
      if (plate) {
        const vPayload = { licensePlate: plate, modelId: formData.modelId, color: formData.color, manufactureYear: formData.manufactureYear };
        if (modal.mode === "edit" && modal.item.vehicleId) {
          await updateVehicle(modal.item.vehicleId, vPayload);
        } else {
          const v = await createVehicle(vPayload);
          await createCustomerVehicle({ customerId: savedCustomer.id || modal.item.id, vehicleId: v.id, isCurrentOwner: true });
        }
      }

      await fetchData();
      closeModal();
    } catch (err) {
      setFormError(err.message || "Save failed.");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    setSaving(true);
    try {
      // Delete links first
      const links = customerVehicles.filter(link => link.customerId === modal.item.id);
      for (const link of links) await deleteCustomerVehicle(link.id);
      await deleteCustomer(modal.item.id);
      await fetchData();
      closeModal();
    } catch (err) {
      setFormError(err.message || "Delete failed.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="management-section">
      <div className="stats-bar">
        <div className="stat-card"><div className="stat-value">{customers.length}</div><div className="stat-label">Total Users</div></div>
        <div className="stat-card"><div className="stat-value">{customers.filter(c => c.isActive).length}</div><div className="stat-label">Active</div></div>
      </div>

      <div className="table-toolbar">
        <span className="result-count">{filteredData.length} result(s)</span>
        <button className="btn-primary" onClick={() => openModal("create")}>+ Add Customer</button>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state"><div className="spinner" /><p>Loading...</p></div>
        ) : (
          <table className="admin-table">
            <thead><tr><th>ID</th><th>Full Name</th><th>Phone</th><th>Type</th><th>Google ID</th><th>Tier</th><th>Plate</th><th>Status</th><th className="th-center">Actions</th></tr></thead>
            <tbody>
              {pagedData.map(item => (
                <tr key={item.id}>
                  <td className="td-id">#{item.id}</td>
                  <td>{item.fullName}</td>
                  <td>{item.phone}</td>
                  <td>{item.type || "-"}</td>
                  <td>{item.googleId || "-"}</td>
                  <td>{item.tierName}</td>
                  <td>{item.licensePlate}</td>
                  <td><span className={`badge ${getStatusBadge(item.isActive)}`}>{item.isActive ? "active" : "inactive"}</span></td>
                  <td className="td-center">
                    <div className="row-actions">
                      <button className="btn-icon btn-edit" onClick={() => openModal("edit", item)}>✏️</button>
                      <button className="btn-icon btn-del" onClick={() => openModal("delete", item)}>🗑️</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      <TablePagination
        totalItems={filteredData.length}
        currentPage={currentPage}
        pageSize={pageSize}
        onPageChange={setCurrentPage}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setCurrentPage(1);
        }}
      />

      {modal && (
        <Modal title={modal.mode === "delete" ? "Confirm Delete" : `${modal.mode === "create" ? "Add" : "Edit"} Customer`} onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}
          {modal.mode === "delete" ? (
             <ConfirmDialog message={`Delete customer ${modal.item.fullName}?`} onConfirm={handleDelete} onCancel={closeModal} loading={saving} />
          ) : (
            <CustomerForm 
              initial={modal.mode === "create" ? EMPTY_CUSTOMER_FORM : { ...modal.item, password: "" }}
              tiers={tiers}
              onSubmit={handleSave}
              onCancel={closeModal}
              loading={saving}
            />
          )}
        </Modal>
      )}
    </section>
  );
}
