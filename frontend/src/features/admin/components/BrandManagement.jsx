import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useSearch } from "@context/SearchContext";
import { getAllVehicleBrands, createVehicleBrand, updateVehicleBrand, deleteVehicleBrand } from "../api/vehicleCatalogApi";
import { Modal, ConfirmDialog } from "./AdminModals";
import { VehicleBrandForm } from "./AdminForms";
import { getStatusBadge } from "../utils/adminUtils";
import { EMPTY_BRAND_FORM } from "../constants/adminConstants";
import TablePagination from "../../../shared/components/TablePagination";

export default function BrandManagement() {
  const [data, setData] = useState([]);
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
      setData(await getAllVehicleBrands() || []);
    } catch (err) {
      setError(err.message || "Failed to load brands.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const filteredData = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return data;
    return data.filter(item => (
      (item.code || "").toLowerCase().includes(query) ||
      (item.name || "").toLowerCase().includes(query)
    ));
  }, [data, searchQuery]);

  const pagedData = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredData.slice(start, start + pageSize);
  }, [filteredData, currentPage, pageSize]);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, data.length]);

  const openModal = (mode, item = null) => setModal({ mode, item });
  const closeModal = () => setModal(null);

  const handleSave = async (formData) => {
    setSaving(true);
    try {
      if (modal.mode === "create") await createVehicleBrand(formData);
      else await updateVehicleBrand(modal.item.id, formData);
      await fetchData();
      closeModal();
    } catch (err) {
      setError(err.message || "Save failed.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="management-section">
      <div className="table-toolbar">
        <span className="result-count">{filteredData.length} brand(s)</span>
        <button className="btn-primary" onClick={() => openModal("create")}>+ Add Brand</button>
      </div>
      <div className="table-wrapper">
        {loading ? <p>Loading...</p> : (
          <table className="admin-table">
            <thead><tr><th>ID</th><th>Code</th><th>Name</th><th>Status</th><th className="th-center">Actions</th></tr></thead>
            <tbody>
              {pagedData.map(item => (
                <tr key={item.id}>
                  <td>#{item.id}</td><td>{item.code}</td><td>{item.name}</td>
                  <td><span className={`badge ${getStatusBadge(item.isActive)}`}>{item.isActive ? "active" : "inactive"}</span></td>
                  <td className="td-center">
                    <button className="btn-icon" onClick={() => openModal("edit", item)}>✏️</button>
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
        <Modal title={modal.mode === "create" ? "Add Brand" : "Edit Brand"} onClose={closeModal}>
          <VehicleBrandForm initial={modal.mode === "create" ? EMPTY_BRAND_FORM : modal.item} onSubmit={handleSave} onCancel={closeModal} loading={saving} />
        </Modal>
      )}
    </section>
  );
}
