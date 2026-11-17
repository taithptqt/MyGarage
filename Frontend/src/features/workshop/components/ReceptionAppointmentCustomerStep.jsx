import React from "react";
import {
  VIETNAM_LICENSE_PLATE_PATTERN,
  VIETNAM_LICENSE_PLATE_TITLE,
} from "@shared/utils/licensePlateValidation";
import { VIETNAM_VIN_PATTERN, VIETNAM_VIN_TITLE } from "@shared/utils/vinValidation";
import { VN_PHONE_PATTERN } from "@features/workshop/components/receptionCreateAppointmentHelpers";

export default function ReceptionAppointmentCustomerStep({
  formData,
  search,
  setSearch,
  filteredCustomers,
  selectCustomer,
  toggleQuickOrder,
  customerVehiclesFiltered,
  selectedCustomerVehicles,
  selectableCustomerVehicles,
  isSavedVehiclePopupOpen,
  setIsSavedVehiclePopupOpen,
  describeSavedVehicle,
  toggleVehicleSelection,
  addExistingVehicle,
  switchToNewVehicleMode,
  switchToSavedVehicleMode,
  confirmNewVehicleMode,
  cancelNewVehicleMode,
  customerHasNoVehicles,
  handleChange,
  handlePlateBlur,
  selectedBrandId,
  setSelectedBrandId,
  guestSelectedBrandId,
  setGuestSelectedBrandId,
  brands,
  models,
  years,
}) {
  return (
    <section className="reception-appt-section">
      <div className="reception-appt-step-head">
        <span className="reception-step-badge">
          {!formData.isQuickOrder ? "1. KHÁCH HÀNG & PHƯƠNG TIỆN" : "1. KHÁCH VÃNG LAI"}
        </span>
        <label className="reception-appt-quick-toggle" htmlFor="appt-quick-toggle">
          <input
            id="appt-quick-toggle"
            type="checkbox"
            checked={formData.isQuickOrder}
            onChange={toggleQuickOrder}
          />
          <span>Đặt lịch cho khách vãng lai</span>
        </label>
      </div>

      {!formData.isQuickOrder ? (
        <>
          <div className="form-group">
            <label>Tìm khách hàng</label>
            <div className="search-input-wrapper">
              <input
                type="text"
                placeholder="Nhập tên hoặc số điện thoại..."
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                required={!formData.customerId}
              />
              {filteredCustomers.length > 0 ? (
                <div
                  className="dropdown-panel"
                  style={{ position: "absolute", top: "100%", left: 0, right: 0, zIndex: 20 }}
                >
                  {filteredCustomers.map((customer) => (
                    <div
                      key={customer.id}
                      className="dropdown-item"
                      onClick={() => selectCustomer(customer)}
                    >
                      <strong>{customer.fullName}</strong> - {customer.phone}
                    </div>
                  ))}
                </div>
              ) : null}
            </div>
          </div>

          {formData.customerId ? (
            <div className="reception-customer-selected-card">
              <div className="reception-customer-selected-main">
                <strong>{formData.customerName}</strong>
                <span>{formData.customerPhone}</span>
              </div>
            </div>
          ) : null}

          {!formData.isNewVehicle ? (
            <div className="form-group">
              <label>Chọn xe (có thể chọn nhiều xe)</label>
              {!formData.customerId ? (
                <div className="reception-muted-note">Hãy chọn khách hàng trước.</div>
              ) : (
                <>
                  <div className="reception-vehicle-popup-toolbar">
                    <button
                      type="button"
                      className="reception-open-vehicle-popup-btn"
                      onClick={() => setIsSavedVehiclePopupOpen(true)}
                      disabled={customerVehiclesFiltered.length === 0}
                    >
                      Xe đã lưu
                    </button>
                    <button
                      type="button"
                      className="reception-open-vehicle-popup-btn"
                      onClick={switchToNewVehicleMode}
                    >
                      Thêm xe
                    </button>
                  </div>
                  {customerVehiclesFiltered.length === 0 ? (
                    <div className="reception-muted-note">Khách hàng chưa có xe đã lưu.</div>
                  ) : (
                    <>
                      {selectedCustomerVehicles.length > 0 ? (
                        <div className="reception-selected-vehicle-list">
                          {selectedCustomerVehicles.map((vehicle) => {
                            const plate = String(vehicle.licensePlate || "").trim() || "Không biển số";
                            return (
                              <div
                                key={`selected-vehicle-${vehicle.id}`}
                                className="reception-selected-vehicle-chip"
                              >
                                <span className="reception-selected-vehicle-chip-main">
                                  <strong>{plate}</strong>
                                  <small>{describeSavedVehicle(vehicle)}</small>
                                </span>
                                <button
                                  type="button"
                                  className="reception-selected-vehicle-chip-remove"
                                  onClick={() => toggleVehicleSelection(vehicle.id)}
                                >
                                  Xóa
                                </button>
                              </div>
                            );
                          })}
                        </div>
                      ) : null}

                      {isSavedVehiclePopupOpen ? (
                        <div
                          className="reception-vehicle-popup-backdrop"
                          onClick={() => setIsSavedVehiclePopupOpen(false)}
                        >
                          <div
                            className="reception-vehicle-popup-card"
                            role="dialog"
                            aria-modal="true"
                            aria-label="Chọn xe đã lưu"
                            onClick={(event) => event.stopPropagation()}
                          >
                            <div className="reception-vehicle-popup-head">
                              <div className="reception-vehicle-popup-head-copy">
                                <h5>Chọn xe đã lưu lại</h5>
                                <p>Chỉ hiển thị các xe chưa chọn.</p>
                              </div>
                              <button
                                type="button"
                                className="reception-vehicle-popup-close-btn"
                                onClick={() => setIsSavedVehiclePopupOpen(false)}
                              >
                                Đóng
                              </button>
                            </div>

                            <div className="reception-vehicle-popup-list">
                              {selectableCustomerVehicles.length === 0 ? (
                                <div className="reception-muted-note">
                                  Bạn đã chọn hết xe đã lưu. Bỏ xe ở danh sách đã chọn để chọn lại.
                                </div>
                              ) : (
                                selectableCustomerVehicles.map((vehicle) => {
                                  const plate = String(vehicle.licensePlate || "").trim() || "Không biển số";
                                  return (
                                    <button
                                      key={`popup-vehicle-${vehicle.id}`}
                                      type="button"
                                      className="reception-vehicle-popup-item"
                                      onClick={() => addExistingVehicle(vehicle.id)}
                                    >
                                      <span className="reception-vehicle-popup-item-mark" aria-hidden="true">
                                        +
                                      </span>
                                      <span className="reception-vehicle-popup-item-copy">
                                        <strong>{plate}</strong>
                                        <small>{describeSavedVehicle(vehicle)}</small>
                                      </span>
                                      <span className="reception-vehicle-popup-item-action">Thêm xe</span>
                                    </button>
                                  );
                                })
                              )}
                            </div>
                          </div>
                        </div>
                      ) : null}
                    </>
                  )}
                </>
              )}
            </div>
          ) : (
            <div className="reception-new-vehicle-box">
              <div className="reception-vehicle-popup-toolbar">
                <button
                  type="button"
                  className="reception-open-vehicle-popup-btn"
                  onClick={switchToSavedVehicleMode}
                  disabled={customerHasNoVehicles}
                >
                  Xe đã lưu
                </button>
                <button
                  type="button"
                  className="reception-open-vehicle-popup-btn is-active"
                  onClick={switchToNewVehicleMode}
                >
                  Thêm xe
                </button>
              </div>
              <div className="form-group">
                <label>Biển số xe mới *</label>
                <input
                  type="text"
                  name="vehicleLicensePlate"
                  value={formData.vehicleLicensePlate}
                  onChange={handleChange}
                  onBlur={handlePlateBlur}
                  pattern={VIETNAM_LICENSE_PLATE_PATTERN}
                  title={VIETNAM_LICENSE_PLATE_TITLE}
                  style={{ textTransform: "uppercase" }}
                  placeholder="VD: 30A-12345"
                />
              </div>
              <div className="reception-grid reception-grid-2">
                <div className="form-group">
                  <label>Hãng xe *</label>
                  <select value={selectedBrandId} onChange={(event) => setSelectedBrandId(event.target.value)}>
                    <option value="">-- Chọn hãng --</option>
                    {brands.map((brand) => (
                      <option key={brand.id} value={brand.id}>
                        {brand.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>Dòng xe *</label>
                  <select
                    name="vehicleModelId"
                    value={formData.vehicleModelId}
                    onChange={handleChange}
                    disabled={!selectedBrandId}
                  >
                    <option value="">-- Chọn dòng xe --</option>
                    {models
                      .filter((model) => Number(model.brandId) === Number(selectedBrandId))
                      .map((model) => (
                        <option key={model.id} value={model.id}>
                          {model.modelName}
                        </option>
                      ))}
                  </select>
                </div>
              </div>
              <div className="reception-grid reception-grid-3">
                <div className="form-group">
                  <label>Màu sắc</label>
                  <input name="vehicleColor" value={formData.vehicleColor} onChange={handleChange} />
                </div>
                <div className="form-group">
                  <label>Năm sản xuất</label>
                  <select name="vehicleYear" value={formData.vehicleYear} onChange={handleChange}>
                    <option value="">-- Chọn năm --</option>
                    {years.map((year) => (
                      <option key={year} value={year}>
                        {year}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>VIN *</label>
                  <input
                    name="vehicleVin"
                    value={formData.vehicleVin}
                    onChange={handleChange}
                    pattern={VIETNAM_VIN_PATTERN}
                    title={VIETNAM_VIN_TITLE}
                    maxLength={17}
                    style={{ textTransform: "uppercase" }}
                  />
                </div>
              </div>
              <div className="reception-new-vehicle-actions">
                <button
                  type="button"
                  className="reception-open-vehicle-popup-btn is-active"
                  onClick={confirmNewVehicleMode}
                >
                  Xác nhận
                </button>
                <button type="button" className="reception-open-vehicle-popup-btn" onClick={cancelNewVehicleMode}>
                  Hủy
                </button>
              </div>
            </div>
          )}
        </>
      ) : (
        <div className="reception-new-vehicle-box">
          <div className="reception-grid reception-grid-2">
            <div className="form-group">
              <label>Tên khách *</label>
              <input name="guestName" value={formData.guestName} onChange={handleChange} />
            </div>
            <div className="form-group">
              <label>Số điện thoại *</label>
              <input
                type="tel"
                inputMode="tel"
                name="guestPhone"
                value={formData.guestPhone}
                onChange={handleChange}
                pattern={VN_PHONE_PATTERN}
                title="Số điện thoại hợp lệ: 0xxxxxxxxx hoặc +84xxxxxxxxx"
              />
            </div>
          </div>
          <div className="form-group">
            <label>Biển số xe *</label>
            <input
              name="guestVehiclePlate"
              value={formData.guestVehiclePlate}
              onChange={handleChange}
              onBlur={handlePlateBlur}
              pattern={VIETNAM_LICENSE_PLATE_PATTERN}
              title={VIETNAM_LICENSE_PLATE_TITLE}
              style={{ textTransform: "uppercase" }}
            />
          </div>
          <div className="reception-grid reception-grid-2">
            <div className="form-group">
              <label>Hãng xe *</label>
              <select value={guestSelectedBrandId} onChange={(event) => setGuestSelectedBrandId(event.target.value)}>
                <option value="">-- Chọn hãng --</option>
                {brands.map((brand) => (
                  <option key={brand.id} value={brand.id}>
                    {brand.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>Dòng xe *</label>
              <select
                name="guestVehicleModelId"
                value={formData.guestVehicleModelId}
                onChange={handleChange}
                disabled={!guestSelectedBrandId}
              >
                <option value="">-- Chọn dòng xe --</option>
                {models
                  .filter((model) => Number(model.brandId) === Number(guestSelectedBrandId))
                  .map((model) => (
                    <option key={model.id} value={model.id}>
                      {model.modelName}
                    </option>
                  ))}
              </select>
            </div>
          </div>
          <div className="reception-grid reception-grid-3">
            <div className="form-group">
              <label>Màu sắc</label>
              <input name="guestVehicleColor" value={formData.guestVehicleColor} onChange={handleChange} />
            </div>
            <div className="form-group">
              <label>Năm sản xuất</label>
              <select name="guestVehicleYear" value={formData.guestVehicleYear} onChange={handleChange}>
                <option value="">-- Chọn năm --</option>
                {years.map((year) => (
                  <option key={year} value={year}>
                    {year}
                  </option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>VIN *</label>
              <input
                name="guestVehicleVin"
                value={formData.guestVehicleVin}
                onChange={handleChange}
                pattern={VIETNAM_VIN_PATTERN}
                title={VIETNAM_VIN_TITLE}
                maxLength={17}
                style={{ textTransform: "uppercase" }}
              />
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
