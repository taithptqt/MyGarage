import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useSearch } from "@context/SearchContext";
import { getAllStaff, getAllRoles, getAllBranches, createStaff, updateStaff, deleteStaff } from "../api/staffApi";
import { Modal, ConfirmDialog } from "./AdminModals";
import { StaffForm } from "./AdminForms";
import { normalizeText, getStatusBadge } from "../utils/adminUtils";
import { EMPTY_STAFF_FORM } from "../constants/adminConstants";
import TablePagination from "../../../shared/components/TablePagination";

export default function StaffManagement() {
  const [data, setData] = useState([]);
  const [roles, setRoles] = useState([]);
  const [branches, setBranches] = useState([]);
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
      const [staffRes, rolesRes, branchesRes] = await Promise.all([
        getAllStaff(), 
        getAllRoles(),
        getAllBranches()
      ]);
      setData(staffRes || []);
      setRoles(rolesRes || []);
      setBranches(branchesRes || []);
    } catch (err) {
      setError(err.message || "Failed to load staff data.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const roleNameMap = useMemo(() => {
    const map = new Map();
    (roles || []).forEach((role) => map.set(role.id, role.roleName || role.roleCode || `Role #${role.id}`));
    return map;
  }, [roles]);

  const branchNameMap = useMemo(() => {
    const map = new Map();
    (branches || []).forEach((b) => map.set(b.id, b.name || `Branch #${b.id}`));
    return map;
  }, [branches]);

  const filteredData = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return data;
    return data.filter((item) => {
      const roleName = roleNameMap.get(item.roleId) || "";
      const branchName = branchNameMap.get(item.branchId) || "";
      return (
        (item.fullName || "").toLowerCase().includes(query) ||
        (item.email || "").toLowerCase().includes(query) ||
        (item.phone || "").toLowerCase().includes(query) ||
        roleName.toLowerCase().includes(query) ||
        branchName.toLowerCase().includes(query)
      );
    });
  }, [data, roleNameMap, searchQuery]);

  const staffRoleOptions = useMemo(() => {
    return (roles || []).filter((r) => r?.isDeleted !== true && r?.isActive !== false);
  }, [roles]);

  const pagedData = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredData.slice(start, start + pageSize);
  }, [filteredData, currentPage, pageSize]);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, data.length]);

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
      
      if (modal.mode === "create") await createStaff(payload);
      else await updateStaff(modal.item.id, payload);
      
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
      await deleteStaff(modal.item.id);
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
        <div className="stat-card"><div className="stat-value">{data.length}</div><div className="stat-label">Total Staff</div></div>
        <div className="stat-card"><div className="stat-value">{data.filter(s => s.status === "ACTIVE").length}</div><div className="stat-label">Active</div></div>
        <div className="stat-card"><div className="stat-value">{new Set(data.filter(s => s.roleId).map(s => s.roleId)).size}</div><div className="stat-label">Roles Used</div></div>
      </div>

      <div className="table-toolbar">
        <span className="result-count">{filteredData.length} result(s)</span>
        <button className="btn-primary" onClick={() => openModal("create")}>+ Add Staff</button>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <div className="table-wrapper">
        {loading ? (
          <div className="loading-state"><div className="spinner" /><p>Loading...</p></div>
        ) : filteredData.length === 0 ? (
          <div className="empty-state"><p>No records found.</p></div>
        ) : (
          <table className="admin-table">
            <thead><tr><th>ID</th><th>Full Name</th><th>Phone</th><th>Branch</th><th>Role</th><th>Status</th><th>Created</th><th className="th-center">Actions</th></tr></thead>
            <tbody>
              {pagedData.map((item) => (
                <tr key={item.id}>
                  <td className="td-id">#{item.id}</td>
                  <td>{item.fullName || "-"}</td>
                  <td>{item.phone || "-"}</td>
                  <td>{branchNameMap.get(item.branchId) || "-"}</td>
                  <td>{roleNameMap.get(item.roleId) || "-"}</td>
                  <td><span className={`badge ${getStatusBadge(item.status)}`}>{item.status || "-"}</span></td>
                  <td>{item.createdAt ? new Date(item.createdAt).toLocaleDateString() : "-"}</td>
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

      {modal?.mode === "create" && (
        <Modal title="Add Staff" onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}
          <StaffForm 
            initial={EMPTY_STAFF_FORM} 
            roles={staffRoleOptions} 
            branches={branches}
            onSubmit={handleSave} 
            onCancel={closeModal} 
            loading={saving} 
          />
        </Modal>
      )}

      {modal?.mode === "edit" && (
        <Modal title={`Edit Staff #${modal.item.id}`} onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}
          <StaffForm 
            initial={{ ...modal.item, password: "", roleId: String(modal.item.roleId), branchId: String(modal.item.branchId || "") }} 
            roles={staffRoleOptions} 
            branches={branches}
            onSubmit={handleSave} 
            onCancel={closeModal} 
            loading={saving} 
          />
        </Modal>
      )}

      {modal?.mode === "delete" && (
        <Modal title="Confirm Delete" onClose={closeModal}>
          {formError && <div className="alert alert-error">{formError}</div>}
          <ConfirmDialog 
            message={`Delete staff member ${modal.item.fullName} (#${modal.item.id})?`} 
            onConfirm={handleDelete} 
            onCancel={closeModal} 
            loading={saving} 
          />
        </Modal>
      )}
    </section>
  );
}
