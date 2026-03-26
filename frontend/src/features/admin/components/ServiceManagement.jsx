import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useSearch } from "@context/SearchContext";
import { getAllStandardServices, getAllServiceCategories, createStandardService, updateStandardService, deleteStandardService } from "../api/serviceApi";
import { Modal, ConfirmDialog } from "./AdminModals";
import { ServiceForm } from "./AdminForms";
import { getStatusBadge } from "../utils/adminUtils";
import { EMPTY_SERVICE_FORM } from "../constants/adminConstants";
import TablePagination from "../../../shared/components/TablePagination";

export default function ServiceManagement() {
  const [data, setData] = useState([]);
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [modal, setModal] = useState(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const { searchQuery } = useSearch();

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [sRes, cRes] = await Promise.all([getAllStandardServices(), getAllServiceCategories()]);
      setData(sRes || []);
      setCategories(cRes || []);
    } catch (err) {
      setError(err.message || "Failed to load services.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const categoryNameMap = useMemo(() => {
    const map = new Map();
    categories.forEach(c => map.set(c.id, c.name || c.code || `Category #${c.id}`));
    return map;
  }, [categories]);

  const filteredData = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return data;
    return data.filter(item => (
      (item.name || "").toLowerCase().includes(query) ||
      (item.code || "").toLowerCase().includes(query)
    ));
  }, [data, searchQuery]);

  const pagedData = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredData.slice(start, start + pageSize);
  }, [filteredData, currentPage, pageSize]);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, data.length]);

  const handleSave = async (formData) => {
    setSaving(true);
    try {
      if (modal.mode === "create") await createStandardService(formData);
      else await updateStandardService(modal.item.id, formData);
      await fetchData();
      setModal(null);
    } catch (err) {
      setError(err.message || "Save failed.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="management-section">
      <div className="table-toolbar"><button className="btn-primary" onClick={() => setModal({ mode: "create" })}>+ Add Service</button></div>
      <div className="table-wrapper">
        {loading ? <p>Loading...</p> : (
          <table className="admin-table">
            <thead><tr><th>ID</th><th>Code</th><th>Name</th><th>Category</th><th>Price</th><th>Status</th><th className="th-center">Actions</th></tr></thead>
            <tbody>
              {pagedData.map(item => (
                <tr key={item.id}>
                  <td>#{item.id}</td><td>{item.code}</td><td>{item.name}</td><td>{categoryNameMap.get(item.categoryId)}</td>
                  <td>{Number(item.basePrice || 0).toLocaleString()}</td>
                  <td><span className={`badge ${getStatusBadge(item.isActive)}`}>{item.isActive ? "active" : "inactive"}</span></td>
                  <td className="td-center"><button onClick={() => setModal({ mode: "edit", item })}>✏️</button></td>
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
        <Modal title="Service Management" onClose={() => setModal(null)}>
          <ServiceForm initial={modal.mode === "create" ? EMPTY_SERVICE_FORM : modal.item} categories={categories} onSubmit={handleSave} onCancel={() => setModal(null)} loading={saving} />
        </Modal>
      )}
    </section>
  );
}
