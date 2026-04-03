import { useMemo, useState } from "react";
import { extractField, formatStoredVehicleLabel } from "@features/appointments/utils/bookingCommon";

export default function VehicleActionPanel({
  linkedVehicles,
  selectedVehicles,
  brands,
  models,
  activeVehicleKey,
  onSelectVehicle,
  draftVehicle,
  onDraftChange,
  onToggleExistingVehicle,
  onAddNewVehicle,
  onRemoveVehicle,
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [isNewVehicleFormVisible, setIsNewVehicleFormVisible] = useState(false);

  const selectedExistingKeys = useMemo(
    () => new Set((selectedVehicles || []).filter((item) => item.source === "existing").map((item) => item.key)),
    [selectedVehicles]
  );
  const selectableLinkedVehicles = useMemo(
    () =>
      (linkedVehicles || []).filter((vehicle) => {
        const key = `existing-${Number(extractField(vehicle, ["id", "Id"]))}`;
        return !selectedExistingKeys.has(key);
      }),
    [linkedVehicles, selectedExistingKeys]
  );

  const modelOptions = useMemo(
    () =>
      (models || []).filter(
        (item) => String(extractField(item, ["brandId", "BrandId"])) === String(draftVehicle.brandId)
      ),
    [draftVehicle.brandId, models]
  );

  function handleOpenNewVehicleForm() {
    setIsNewVehicleFormVisible(true);
  }

  function handleCancelNewVehicleForm() {
    setIsNewVehicleFormVisible(false);
  }

  function handleSubmitNewVehicle(event) {
    event?.preventDefault?.();
    event?.stopPropagation?.();
    onAddNewVehicle();
  }

  function handleOpenSavedVehicles(event) {
    event.preventDefault();
    event.stopPropagation();
    setIsOpen(true);
  }

  function handleCloseSavedVehicles(event) {
    event.preventDefault();
    event.stopPropagation();
    setIsOpen(false);
  }

  function handleOpenNewVehicle(event) {
    event.preventDefault();
    event.stopPropagation();
    handleOpenNewVehicleForm();
  }

  function handleCancelNewVehicle(event) {
    event.preventDefault();
    event.stopPropagation();
    handleCancelNewVehicleForm();
  }

  function handlePickExistingVehicle(event, vehicle) {
    event.preventDefault();
    event.stopPropagation();
    onToggleExistingVehicle(vehicle);
    const key = `existing-${Number(extractField(vehicle, ["id", "Id"]))}`;
    onSelectVehicle?.(key);
  }

  function handleSelectVehicle(event, vehicleKey) {
    event.preventDefault();
    event.stopPropagation();
    onSelectVehicle?.(vehicleKey);
  }

  function handleRemoveVehicle(event, vehicleKey) {
    event.preventDefault();
    event.stopPropagation();
    onRemoveVehicle(vehicleKey);
  }

  return (
    <section className="vehicle-action-panel">
      <div className="vehicle-action-head">
        <h4>Danh sách xe</h4>
        <p>Chọn nhiều xe đã lưu qua popup hoặc thêm xe mới ngay tại đây.</p>
      </div>

      <div className="vehicle-action-toolbar">
        <button type="button" className="ghost-btn" onClick={handleOpenSavedVehicles}>
          Chọn xe đã lưu lại
        </button>
        {!isNewVehicleFormVisible ? (
          <button type="button" className="ghost-btn" onClick={handleOpenNewVehicle}>
            Thêm xe mới
          </button>
        ) : null}
      </div>

      {isOpen ? (
        <div className="vehicle-popup-backdrop" onClick={() => setIsOpen(false)}>
          <div className="vehicle-popup-card" onClick={(event) => event.stopPropagation()}>
            <div className="vehicle-popup-head">
              <div className="vehicle-popup-head-text">
                <h5>Chọn xe đã lưu lại</h5>
                <p>Chỉ hiển thị các xe chưa chọn.</p>
              </div>
              <button type="button" className="vehicle-popup-close-btn" onClick={handleCloseSavedVehicles}>
                Đóng
              </button>
            </div>
            <div className="vehicle-popup-list">
              {(linkedVehicles || []).length === 0 ? (
                <p className="booking-empty">Chưa có xe đã lưu để chọn.</p>
              ) : selectableLinkedVehicles.length === 0 ? (
                <p className="booking-empty">Bạn đã chọn hết xe đã lưu. Xóa xe khỏi danh sách bên dưới để chọn lại.</p>
              ) : (
                selectableLinkedVehicles.map((vehicle) => {
                  const key = `existing-${Number(extractField(vehicle, ["id", "Id"]))}`;
                  return (
                    <button
                      key={key}
                      type="button"
                      className="vehicle-popup-item"
                      onClick={(event) => handlePickExistingVehicle(event, vehicle)}
                    >
                      <span className="vehicle-popup-item-mark" aria-hidden="true">
                        +
                      </span>
                      <div className="vehicle-popup-item-content">
                        <strong>{formatStoredVehicleLabel(vehicle)}</strong>
                        <small>Nhấn để thêm xe vào lịch hẹn</small>
                      </div>
                      <span className="vehicle-popup-item-action">Thêm xe</span>
                    </button>
                  );
                })
              )}
            </div>
          </div>
        </div>
      ) : null}

      {isNewVehicleFormVisible ? (
        <>
          <div className="form-grid step4-form-grid">
            <label>
              Biển số xe <span>*</span>
              <input
                type="text"
                value={draftVehicle.licensePlate}
                onChange={(e) => onDraftChange("licensePlate", e.target.value)}
                placeholder="Nhập biển số xe"
              />
            </label>
            <label>
              Hãng xe <span>*</span>
              <select
                value={draftVehicle.brandId}
                onChange={(e) => {
                  onDraftChange("brandId", e.target.value);
                  onDraftChange("modelId", "");
                }}
              >
                <option value="">Chọn hãng xe</option>
                {(brands || []).map((brand) => (
                  <option key={extractField(brand, ["id", "Id"])} value={extractField(brand, ["id", "Id"])}>
                    {extractField(brand, ["name", "Name"], "")}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Dòng xe <span>*</span>
              <select
                value={draftVehicle.modelId}
                disabled={!draftVehicle.brandId}
                onChange={(e) => onDraftChange("modelId", e.target.value)}
              >
                <option value="">Chọn dòng xe</option>
                {modelOptions.map((model) => (
                  <option key={extractField(model, ["id", "Id"])} value={extractField(model, ["id", "Id"])}>
                    {extractField(model, ["modelName", "ModelName"], "")}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Số VIN <span>*</span>
              <input
                type="text"
                value={draftVehicle.vin}
                onChange={(e) => onDraftChange("vin", e.target.value)}
                placeholder="Nhập số VIN"
              />
            </label>
          </div>

          <div className="vehicle-action-toolbar">
            <button type="button" className="vehicle-add-btn" onClick={handleSubmitNewVehicle}>
              Thêm xe mới
            </button>
            <button type="button" className="ghost-btn" onClick={handleCancelNewVehicle}>
              Hủy
            </button>
          </div>
        </>
      ) : null}

      {(selectedVehicles || []).length > 0 ? (
        <div className="vehicle-batch-list">
          {selectedVehicles.map((vehicle) => (
            <article
              key={vehicle.key}
              className={`vehicle-batch-item ${activeVehicleKey === vehicle.key ? "active" : ""}`}
            >
              <button
                type="button"
                className="vehicle-batch-select"
                onClick={(event) => handleSelectVehicle(event, vehicle.key)}
              >
                <strong>{vehicle.displayLabel}</strong>
              </button>
              <button type="button" onClick={(event) => handleRemoveVehicle(event, vehicle.key)}>
                Xóa
              </button>
            </article>
          ))}
        </div>
      ) : null}
    </section>
  );
}
