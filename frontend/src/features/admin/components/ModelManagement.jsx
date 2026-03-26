import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useSearch } from "@context/SearchContext";
import { getAllVehicleModels, getAllVehicleBrands, createVehicleModel, updateVehicleModel, deleteVehicleModel } from "../api/vehicleCatalogApi";
import { Modal, ConfirmDialog } from "./AdminModals";
import { VehicleModelForm } from "./AdminForms";
import { EMPTY_MODEL_FORM } from "../constants/adminConstants";
import TablePagination from "../../../shared/components/TablePagination";

export default function ModelManagement() {
  const [data, setData] = useState([]);
  const [brands, setBrands] = useState([]);
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
      const [mRes, bRes] = await Promise.all([getAllVehicleModels(), getAllVehicleBrands()]);
      setData(mRes || []);
      setBrands(bRes || []);
    } catch (err) {
      setError(err.message || "Failed to load models.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const brandNameMap = useMemo(() => {
    const map = new Map();
    brands.forEach(b => map.set(b.id, b.name || b.code || `Brand #${b.id}`));
    return map;
  }, [brands]);

  const filteredData = useMemo(() => {
    const query = String(searchQuery || "").trim().toLowerCase();
    if (!query) return data;
    return data.filter(item => (
      (item.modelName || "").toLowerCase().includes(query) ||
      (item.segment || "").toLowerCase().includes(query)
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
      if (modal.mode === "create") await createVehicleModel(formData);
      else await updateVehicleModel(modal.item.id, formData);
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
      <div className="table-toolbar"><button className="btn-primary" onClick={() => setModal({ mode: "create" })}>+ Add Model</button></div>
      <div className="table-wrapper">
        {loading ? <p>Loading...</p> : (
          <table className="admin-table">
            <thead><tr><th>ID</th><th>Brand</th><th>Model Name</th><th>Segment</th><th className="th-center">Actions</th></tr></thead>
            <tbody>
              {pagedData.map(item => (
                <tr key={item.id}>
                  <td>#{item.id}</td><td>{brandNameMap.get(item.brandId)}</td><td>{item.modelName}</td><td>{item.segment}</td>
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
        <Modal title="Vehicle Model" onClose={() => setModal(null)}>
          <VehicleModelForm initial={modal.mode === "create" ? EMPTY_MODEL_FORM : modal.item} brands={brands} onSubmit={handleSave} onCancel={() => setModal(null)} loading={saving} />
        </Modal>
      )}
    </section>
  );
}
