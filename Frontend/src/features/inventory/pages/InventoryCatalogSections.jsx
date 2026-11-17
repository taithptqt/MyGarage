import { Modal } from "@features/admin/components/AdminModals";
import TablePagination from "@shared/components/TablePagination";
import { ActionPanel, NoDataRow, StatCards } from "./InventoryAdminSharedWidgets";
import { ITEM_TYPE_OPTIONS, friendlyType, toNumber } from "./inventoryAdminHelpers";

export function WarehouseStructureSection({
  warehouseStructure,
  form,
  updateForm,
  runAction,
  handleCreateWarehouse,
  settings,
  setSettings,
  saveSettings,
  pagedWarehouses,
  handleToggleWarehouse,
  warehousePage,
  warehousePageSize,
  setWarehousePage,
  setWarehousePageSize
}) {
  return (
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
      <TablePagination
        totalItems={warehouseStructure.total ?? (warehouseStructure.warehouses || []).length}
        currentPage={warehousePage}
        pageSize={warehousePageSize}
        onPageChange={setWarehousePage}
        onPageSizeChange={(size) => {
          setWarehousePageSize(size);
          setWarehousePage(1);
        }}
      />
    </>
  );
}

export function CatalogProductsSection({
  catalogSummary,
  openCreateProductModal,
  runAction,
  exportProductsCsv,
  importProductsCsv,
  showCreateProductModal,
  productModalMode,
  closeCreateProductModal,
  productForm,
  updateProductForm,
  categoryOptions,
  productMainImagePreview,
  editProductImages,
  productMainImageFile,
  onMainProductImageChange,
  clearMainImagePick,
  editingProduct,
  galleryBusy,
  onGalleryImageSelected,
  onDeleteProductImageRow,
  savingProduct,
  categoryOptionsReady,
  handleUpdateProduct,
  handleCreateProduct,
  products,
  pagedProducts,
  productThumbById,
  openEditProductModal,
  handleDeleteProduct,
  markDiscontinued,
  productPage,
  productPageSize,
  setProductPage,
  setProductPageSize
}) {
  return (
    <>
      <StatCards
        items={[
          { label: "Tổng sản phẩm", value: catalogSummary.productCount },
          { label: "Danh mục", value: catalogSummary.categoryCount },
          { label: "Nhà cung cấp", value: catalogSummary.supplierCount }
        ]}
      />

      <ActionPanel
        title="Quản lý danh mục sản phẩm"
        description="Thêm mới sản phẩm bằng popup chi tiết theo Product model, đồng thời hỗ trợ import/export CSV."
      >
        <div className="table-toolbar inventory-form-actions">
          <button className="btn-primary" onClick={openCreateProductModal}>
            + Thêm sản phẩm
          </button>
        </div>
        <div className="table-toolbar inventory-form-actions">
          <button className="btn-ghost" onClick={() => runAction(exportProductsCsv)}>Xuất CSV danh mục</button>
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

      {showCreateProductModal ? (
        <Modal title={productModalMode === "edit" ? "Sửa sản phẩm" : "Thêm sản phẩm mới"} onClose={closeCreateProductModal}>
          <div className="inventory-product-modal">
            <p className="inventory-product-modal-desc">
              Điền thông tin sản phẩm. Ảnh tải lên Azure Blob (cấu hình{" "}
              <code className="inventory-code-inline">AzureStorage</code>); hệ thống chỉ lưu URL trong{" "}
              <code className="inventory-code-inline">ProductImages</code>. Định dạng JPEG/PNG/WEBP, tối đa 5MB.
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
                    placeholder="Ví dụ: SKU-0001"
                  />
                </label>
                <label className="compact-field">
                  <span>Tên sản phẩm *</span>
                  <input
                    className="search-input"
                    value={productForm.name}
                    onChange={(e) => updateProductForm("name", e.target.value)}
                    placeholder="Ví dụ: Lọc nhơt Honda"
                  />
                </label>
                <label className="compact-field">
                  <span>Đơn vị tính *</span>
                  <input
                    className="search-input"
                    value={productForm.unit}
                    onChange={(e) => updateProductForm("unit", e.target.value)}
                    placeholder="Cái"
                  />
                </label>
                <label className="compact-field">
                  <span>Danh mục</span>
                  <select
                    className="search-input"
                    value={productForm.categoryId}
                    onChange={(e) => updateProductForm("categoryId", e.target.value)}
                  >
                    <option value="">-- Chưa gán --</option>
                    {categoryOptions.map((category) => (
                      <option key={category.id} value={category.id}>
                        {category.categoryName}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
            </div>

            <div className="inventory-product-modal-section">
              <h4>Ảnh Part Shop (cloud)</h4>
              <p className="inventory-product-image-hint">
                Ảnh đại diện dùng cho catalog / giỏ hàng. Nhấn <strong>Lưu sản phẩm</strong> để gửi file lên Azure và cập nhật
                URL trong CSDL. Khi sửa, chọn ảnh mới rồi lưu để thay ảnh chính (ghi đè bản ghi đại diện).
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
                  <div className="inventory-product-image-placeholder">Chưa có ảnh</div>
                )}
                <div className="inventory-product-image-actions">
                  <label className="btn-ghost inventory-product-image-file">
                    Chọn ảnh đại diện
                    <input
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      className="inventory-file-input-hidden"
                      onChange={onMainProductImageChange}
                    />
                  </label>
                  {productMainImageFile ? (
                    <button type="button" className="btn-ghost" onClick={clearMainImagePick}>
                      Bỏ chọn file
                    </button>
                  ) : null}
                </div>
              </div>

              {productModalMode === "edit" && editingProduct?.id ? (
                <div className="inventory-product-gallery-block">
                  <div className="inventory-product-gallery-toolbar">
                    <span className="inventory-product-gallery-title">Gallery (trang chi tiết)</span>
                    <label
                      className={`btn-ghost inventory-product-image-file ${galleryBusy ? "is-busy" : ""}`}
                    >
                      {galleryBusy ? "Đang tải..." : "+ Thêm ảnh phụ"}
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
                    <p className="inventory-product-gallery-empty">Chưa có ảnh trong CSDL.</p>
                  ) : (
                    <ul className="inventory-product-gallery-list">
                      {editProductImages.map((img) => (
                        <li key={img.id} className="inventory-product-gallery-item">
                          <img src={img.imageUrl} alt="" className="inventory-product-gallery-thumb" />
                          <div className="inventory-product-gallery-meta">
                            {img.isMain ? (
                              <span className="badge badge-active">Đại diện</span>
                            ) : (
                              <span className="badge">Phụ</span>
                            )}
                            <button
                              type="button"
                              className="btn-icon"
                              onClick={() => onDeleteProductImageRow(img)}
                              disabled={galleryBusy}
                            >
                              Xóa
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
              <h4>Giá và chính sách</h4>
              <div className="inventory-product-modal-grid inventory-product-modal-grid--price">
                <label className="compact-field">
                  <span>Giá vđn</span>
                  <input
                    className="search-input"
                    type="number"
                    value={productForm.costPrice}
                    onChange={(e) => updateProductForm("costPrice", e.target.value)}
                    placeholder="0"
                  />
                </label>
                <label className="compact-field">
                  <span>Giá bán lẻ</span>
                  <input
                    className="search-input"
                    type="number"
                    value={productForm.retailPrice}
                    onChange={(e) => updateProductForm("retailPrice", e.target.value)}
                    placeholder="0"
                  />
                </label>
                <label className="compact-field">
                  <span>Giá dịch vụ</span>
                  <input
                    className="search-input"
                    type="number"
                    value={productForm.servicePrice}
                    onChange={(e) => updateProductForm("servicePrice", e.target.value)}
                    placeholder="0"
                  />
                </label>
                <label className="compact-field">
                  <span>Thuế (%)</span>
                  <input
                    className="search-input"
                    type="number"
                    value={productForm.taxRate}
                    onChange={(e) => updateProductForm("taxRate", e.target.value)}
                    placeholder="0"
                  />
                </label>
                <label className="compact-field">
                  <span>Bảo hành (tháng)</span>
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
                <span>Kiểm soát sức chứa nghiêm ngặt</span>
              </label>
              <label className="inventory-product-toggle">
                <input
                  type="checkbox"
                  checked={!!productForm.isActive}
                  onChange={(e) => updateProductForm("isActive", e.target.checked)}
                />
                <span>Đang hoạt động</span>
              </label>
            </div>

            <div className="form-actions">
              <button type="button" className="btn-ghost" onClick={closeCreateProductModal} disabled={savingProduct}>
                Hủy
              </button>
              <button
                type="button"
                className="btn-primary"
                onClick={() => runAction(productModalMode === "edit" ? handleUpdateProduct : handleCreateProduct)}
                disabled={savingProduct || !categoryOptionsReady}
              >
                {savingProduct ? "Đang lưu..." : (productModalMode === "edit" ? "Cập nhật sản phẩm" : "Lưu sản phẩm")}
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
              <th>Ảnh</th>
              <th>SKU</th>
              <th>Tên sản phẩm</th>
              <th>Đơn vị</th>
              <th>Danh mục</th>
              <th>Giá vđn</th>
              <th>Giá bán lẻ</th>
              <th>Giá dịch vụ</th>
              <th>Thuế (%)</th>
              <th>Bảo hành (tháng)</th>
              <th>K.S sức chứa</th>
              <th>Trạng thái</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {products.length === 0 ? (
              <NoDataRow colSpan={14} message="Chưa có sản phẩm trong danh mục kho." />
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
                      <span className="inventory-product-table-thumb-fallback">Ảnh</span>
                    )}
                  </td>
                  <td>{p.sku}</td>
                  <td>{p.name}</td>
                  <td>{p.unit}</td>
                  <td>{p._categoryName || "-"}</td>
                  <td>{toNumber(p.costPrice, 0).toLocaleString()}</td>
                  <td>{toNumber(p.retailPrice, 0).toLocaleString()}</td>
                  <td>{toNumber(p.servicePrice, 0).toLocaleString()}</td>
                  <td>{toNumber(p.taxRate, 0)}</td>
                  <td>{toNumber(p.warrantyMonths, 0)}</td>
                  <td>{p.isStrictCapacityControl ? "Có" : "Không"}</td>
                  <td>
                    <span className={`badge ${p.isActive ? "badge-active" : "badge-deleted"}`}>
                      {p.isActive ? "Đang kinh doanh" : "Ngừng kinh doanh"}
                    </span>
                  </td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-icon" onClick={() => openEditProductModal(p)}>
                        Sửa
                      </button>
                      <button className="btn-icon" onClick={() => runAction(() => handleDeleteProduct(p))}>
                        Xóa
                      </button>
                      {p.isActive ? (
                        <button className="btn-icon" onClick={() => runAction(() => markDiscontinued(p))}>
                          Ngừng KD
                        </button>
                      ) : (
                        <span className="result-count">Đã ngừng</span>
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
        totalItems={catalogSummary.productCount || 0}
        currentPage={productPage}
        pageSize={productPageSize}
        onPageChange={setProductPage}
        onPageSizeChange={(size) => {
          setProductPageSize(size);
          setProductPage(1);
        }}
      />
    </>
  );
}

export function CatalogCategoriesSection({
  catalogSummary,
  openCreateCategoryModal,
  showCategoryModal,
  categoryModalMode,
  closeCategoryModal,
  categoryForm,
  updateCategoryForm,
  categoryOptions,
  editingCategory,
  savingCategory,
  categoryOptionsReady,
  runAction,
  handleUpdateCategory,
  handleCreateCategory,
  categories,
  pagedCategories,
  openEditCategoryModal,
  handleDeleteCategory,
  categoryPage,
  categoryPageSize,
  setCategoryPage,
  setCategoryPageSize
}) {
  return (
    <>
      <StatCards
        items={[
          { label: "Tổng danh mục", value: catalogSummary.categoryCount },
          { label: "Sản phẩm đã phân loại", value: catalogSummary.classified },
          { label: "Sản phẩm chưa gán danh mục", value: catalogSummary.unclassified }
        ]}
      />

      <ActionPanel
        title="Quản lý danh mục"
        description="Thêm và chỉnh sửa danh mục bằng popup để chuẩn hóa dữ liệu phân loại vật tư."
      >
        <div className="table-toolbar inventory-form-actions">
          <button className="btn-primary" onClick={openCreateCategoryModal}>
            + Tạo danh mục
          </button>
        </div>
      </ActionPanel>

      {showCategoryModal ? (
        <Modal title={categoryModalMode === "edit" ? "Sửa danh mục" : "Thêm danh mục mới"} onClose={closeCategoryModal}>
          <div className="inventory-product-modal">
            <p className="inventory-product-modal-desc">
              Quản lý nhóm danh mục để chuẩn hóa phân loại sản phẩm trong kho.
            </p>

            <div className="inventory-product-modal-section">
              <h4>Thông tin danh mục</h4>
              <div className="inventory-product-modal-grid inventory-product-modal-grid--basic">
                <label className="compact-field">
                  <span>Tên danh mục *</span>
                  <input
                    className="search-input"
                    value={categoryForm.categoryName}
                    onChange={(e) => updateCategoryForm("categoryName", e.target.value)}
                    placeholder="Ví dụ: Dầu nhơt"
                  />
                </label>
                <label className="compact-field">
                  <span>Danh mục cha</span>
                  <select
                    className="search-input"
                    value={categoryForm.parentId}
                    onChange={(e) => updateCategoryForm("parentId", e.target.value)}
                  >
                    <option value="">-- Không có --</option>
                    {categoryOptions
                      .filter((item) => !editingCategory || Number(item.id) !== Number(editingCategory.id))
                      .map((item) => (
                        <option key={item.id} value={item.id}>
                          {item.categoryName}
                        </option>
                      ))}
                  </select>
                </label>
                <label className="compact-field inventory-span-2">
                  <span>Mô tả</span>
                  <input
                    className="search-input"
                    value={categoryForm.description}
                    onChange={(e) => updateCategoryForm("description", e.target.value)}
                    placeholder="Mô tả ngắn về nhóm danh mục"
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
                <span>Yêu cầu đặt cọc</span>
              </label>
            </div>

            <div className="form-actions">
              <button className="btn-ghost" onClick={closeCategoryModal} disabled={savingCategory}>
                Hủy
              </button>
              <button
                className="btn-primary"
                onClick={() => runAction(categoryModalMode === "edit" ? handleUpdateCategory : handleCreateCategory)}
                disabled={savingCategory || !categoryOptionsReady}
              >
                {savingCategory ? "Đang lưu..." : (categoryModalMode === "edit" ? "Cập nhật danh mục" : "Lưu danh mục")}
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
              <th>Tên danh mục</th>
              <th>Danh mục cha</th>
              <th>Mô tả</th>
              <th>Đặt cọc</th>
              <th>Số sản phẩm</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {categories.length === 0 ? (
              <NoDataRow colSpan={7} message="Chưa có danh mục nào trong kho." />
            ) : (
              pagedCategories.map((category) => (
                <tr key={category.id}>
                  <td className="td-id">#{category.id}</td>
                  <td>{category.categoryName || "-"}</td>
                  <td>{category.parentId ? (category._parentName || `#${category.parentId}`) : "-"}</td>
                  <td>{category.description || "-"}</td>
                  <td>{category.isDepositRequired ? "Có" : "Không"}</td>
                  <td>{toNumber(category._productCount, 0)}</td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-icon" onClick={() => openEditCategoryModal(category)}>
                        Sửa
                      </button>
                      <button className="btn-icon" onClick={() => runAction(() => handleDeleteCategory(category))}>
                        Xóa
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
        totalItems={catalogSummary.categoryCount || 0}
        currentPage={categoryPage}
        pageSize={categoryPageSize}
        onPageChange={setCategoryPage}
        onPageSizeChange={(size) => {
          setCategoryPageSize(size);
          setCategoryPage(1);
        }}
      />
    </>
  );
}

export function CatalogSuppliersSection({
  catalogSummary,
  form,
  updateForm,
  runAction,
  handleCreateSupplier,
  suppliers,
  pagedSuppliers,
  handleEditSupplier,
  handleDeleteSupplier,
  supplierPage,
  supplierPageSize,
  setSupplierPage,
  setSupplierPageSize
}) {
  return (
    <>
      <StatCards
        items={[
          { label: "Tổng nhà cung cấp", value: catalogSummary.supplierCount },
          { label: "Sản phẩm hiện có", value: catalogSummary.productCount },
          { label: "Danh mục hiện có", value: catalogSummary.categoryCount }
        ]}
      />

      <ActionPanel
        title="Tạo nhà cung cấp mới"
        description="Quản lý danh sách nhà cung cấp để phục vụ truy xuất nguồn gốc và mua hàng."
      >
        <div className="table-toolbar inventory-form-grid inventory-form-grid--small">
          <label className="compact-field">
            <span>Tên nhà cung cấp</span>
            <input
              className="search-input"
              placeholder="Ví dụ: Nhà cung cấp A"
              value={form.supplierName}
              onChange={(e) => updateForm("supplierName", e.target.value)}
            />
          </label>
          <button className="btn-primary" onClick={() => runAction(handleCreateSupplier)}>
            + Tạo nhà cung cấp
          </button>
        </div>
      </ActionPanel>

      <div className="table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Nhà cung cấp</th>
              <th>Công nợ hiện tại</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {suppliers.length === 0 ? (
              <NoDataRow colSpan={4} message="Chưa có nhà cung cấp nào trong hệ thống." />
            ) : (
              pagedSuppliers.map((supplier) => (
                <tr key={supplier.id}>
                  <td className="td-id">#{supplier.id}</td>
                  <td>{supplier.supplierName || supplier.name || "-"}</td>
                  <td>{toNumber(supplier.currentDebt, 0).toLocaleString()}</td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-icon" onClick={() => runAction(() => handleEditSupplier(supplier))}>
                        Sửa
                      </button>
                      <button className="btn-icon" onClick={() => runAction(() => handleDeleteSupplier(supplier))}>
                        Xóa
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
        totalItems={catalogSummary.supplierCount || 0}
        currentPage={supplierPage}
        pageSize={supplierPageSize}
        onPageChange={setSupplierPage}
        onPageSizeChange={(size) => {
          setSupplierPageSize(size);
          setSupplierPage(1);
        }}
      />
    </>
  );
}
