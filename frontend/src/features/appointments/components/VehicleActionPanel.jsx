import { useEffect, useMemo, useRef, useState } from "react";
import { extractField, formatStoredVehicleLabel } from "@features/appointments/utils/bookingCommon";
import { VIETNAM_LICENSE_PLATE_PATTERN, VIETNAM_LICENSE_PLATE_TITLE } from "@shared/utils/licensePlateValidation";
import { VIETNAM_VIN_PATTERN, VIETNAM_VIN_TITLE } from "@shared/utils/vinValidation";

function splitVehicleDisplayLabel(label) {
  const normalized = String(label || "").trim();
  if (!normalized) {
    return { plate: "Không biển số", descriptor: "" };
  }

  const parts = normalized.split(" - ");
  const plate = String(parts.shift() || "").trim() || normalized;
  const descriptor = parts.join(" - ").trim();
  return { plate, descriptor };
}

export default function VehicleActionPanel({
  linkedVehicles,
  selectedVehicles,
  brands,
  models,
  activeVehicleKey,
  forceNewVehicleWhenNoSaved = false,
  onSelectVehicle,
  draftVehicle,
  onDraftChange,
  onToggleExistingVehicle,
  onAddNewVehicle,
  onRemoveVehicle,
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [isNewVehicleFormVisible, setIsNewVehicleFormVisible] = useState(false);
  const savedVehiclePopupRef = useRef(null);

  const selectedExistingKeys = useMemo(
    () =>
      new Set((selectedVehicles || []).filter((item) => item.source === "existing").map((item) => item.key)),
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
  const hasLinkedVehicles = (linkedVehicles || []).length > 0;

  const modelOptions = useMemo(
    () =>
      (models || []).filter(
        (item) => String(extractField(item, ["brandId", "BrandId"])) === String(draftVehicle.brandId)
      ),
    [draftVehicle.brandId, models]
  );

  const manufactureYearOptions = useMemo(() => {
    const currentYear = new Date().getFullYear();
    const years = [];
    for (let year = currentYear; year >= 1980; year -= 1) {
      years.push(year);
    }
    return years;
  }, []);

  useEffect(() => {
    if (!isOpen) return undefined;

    function handleOutsidePointerDown(event) {
      if (!savedVehiclePopupRef.current?.contains(event.target)) {
        setIsOpen(false);
      }
    }

    document.addEventListener("mousedown", handleOutsidePointerDown);
    document.addEventListener("touchstart", handleOutsidePointerDown);

    return () => {
      document.removeEventListener("mousedown", handleOutsidePointerDown);
      document.removeEventListener("touchstart", handleOutsidePointerDown);
    };
  }, [isOpen]);

  useEffect(() => {
    if (!forceNewVehicleWhenNoSaved) return;
    if (!hasLinkedVehicles) {
      setIsNewVehicleFormVisible(true);
      setIsOpen(false);
    }
  }, [forceNewVehicleWhenNoSaved, hasLinkedVehicles]);

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
    setIsNewVehicleFormVisible(true);
  }

  function handleCancelNewVehicle(event) {
    event.preventDefault();
    event.stopPropagation();
    setIsNewVehicleFormVisible(false);
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
        <button type="button" className="ghost-btn" onClick={handleOpenSavedVehicles} disabled={!hasLinkedVehicles}>
          Chọn xe đã lưu lại
        </button>
        {!isNewVehicleFormVisible ? (
          <button type="button" className="ghost-btn" onClick={handleOpenNewVehicle}>
            Thêm xe mới
          </button>
        ) : null}
      </div>
      {!hasLinkedVehicles ? <p className="booking-empty">Khách hàng chưa có xe đã lưu, vui lòng thêm xe mới.</p> : null}

      {isOpen ? (
        <div className="vehicle-popup-backdrop" onClick={() => setIsOpen(false)}>
          <div ref={savedVehiclePopupRef} className="vehicle-popup-card" onClick={(event) => event.stopPropagation()}>
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
                <p className="booking-empty">
                  Bạn đã chọn hết xe đã lưu. Xóa xe khỏi danh sách bên dưới để chọn lại.
                </p>
              ) : (
                selectableLinkedVehicles.map((vehicle) => {
                  const key = `existing-${Number(extractField(vehicle, ["id", "Id"]))}`;
                  const vehicleLabel = formatStoredVehicleLabel(vehicle);
                  const { plate, descriptor } = splitVehicleDisplayLabel(vehicleLabel);
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
                        <strong>{plate}</strong>
                        <small>{descriptor || "Nhấn để thêm xe vào lịch hẹn"}</small>
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
          <div className="form-grid step4-form-grid vehicle-new-form-grid">
            <label className="vehicle-new-field vehicle-new-field-plate">
              <span className="vehicle-new-label">
                Biển số xe mới <em>*</em>
              </span>
              <input
                type="text"
                value={draftVehicle.licensePlate}
                onChange={(e) => onDraftChange("licensePlate", e.target.value)}
                pattern={VIETNAM_LICENSE_PLATE_PATTERN}
                title={VIETNAM_LICENSE_PLATE_TITLE}
                style={{ textTransform: "uppercase" }}
                placeholder="VD: 30A-12345"
              />
            </label>
            <label className="vehicle-new-field vehicle-new-field-brand">
              <span className="vehicle-new-label">
                Hãng xe <em>*</em>
              </span>
              <select
                value={draftVehicle.brandId}
                onChange={(e) => {
                  onDraftChange("brandId", e.target.value);
                  onDraftChange("modelId", "");
                }}
              >
                <option value="">-- Chọn hãng --</option>
                {(brands || []).map((brand) => (
                  <option key={extractField(brand, ["id", "Id"])} value={extractField(brand, ["id", "Id"])}>
                    {extractField(brand, ["name", "Name"], "")}
                  </option>
                ))}
              </select>
            </label>
            <label className="vehicle-new-field vehicle-new-field-model">
              <span className="vehicle-new-label">
                Dòng xe <em>*</em>
              </span>
              <select
                value={draftVehicle.modelId}
                disabled={!draftVehicle.brandId}
                onChange={(e) => onDraftChange("modelId", e.target.value)}
              >
                <option value="">-- Chọn dòng xe --</option>
                {modelOptions.map((model) => (
                  <option key={extractField(model, ["id", "Id"])} value={extractField(model, ["id", "Id"])}>
                    {extractField(model, ["modelName", "ModelName"], "")}
                  </option>
                ))}
              </select>
            </label>
            <div className="vehicle-new-row-bottom">
              <label className="vehicle-new-field vehicle-new-field-color">
                Màu sắc (tùy chọn)
                <input
                  type="text"
                  value={draftVehicle.color || ""}
                  onChange={(e) => onDraftChange("color", e.target.value)}
                />
              </label>
              <label className="vehicle-new-field vehicle-new-field-year">
                <span className="vehicle-new-label">
                  Năm sản xuất <em>*</em>
                </span>
                <select
                  value={draftVehicle.manufactureYear || ""}
                  onChange={(e) => onDraftChange("manufactureYear", e.target.value)}
                >
                  <option value="">-- Chọn năm --</option>
                  {manufactureYearOptions.map((year) => (
                    <option key={year} value={year}>
                      {year}
                    </option>
                  ))}
                </select>
              </label>
              <label className="vehicle-new-field vehicle-new-field-vin">
                <span className="vehicle-new-label">VIN (tùy chọn)</span>
                <input
                  type="text"
                  value={draftVehicle.vin}
                  onChange={(e) => onDraftChange("vin", e.target.value)}
                  pattern={VIETNAM_VIN_PATTERN}
                  title={VIETNAM_VIN_TITLE}
                  maxLength={17}
                  style={{ textTransform: "uppercase" }}
                />
              </label>
            </div>
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
          {selectedVehicles.map((vehicle) => {
            const { plate, descriptor } = splitVehicleDisplayLabel(vehicle.displayLabel);
            return (
              <article
                key={vehicle.key}
                className={`vehicle-batch-item ${activeVehicleKey === vehicle.key ? "active" : ""}`}
              >
                <button
                  type="button"
                  className="vehicle-batch-select"
                  onClick={(event) => handleSelectVehicle(event, vehicle.key)}
                >
                  <span className="vehicle-batch-label">
                    <strong>{plate}</strong>
                    {descriptor ? <small>{descriptor}</small> : null}
                  </span>
                </button>
                <button type="button" onClick={(event) => handleRemoveVehicle(event, vehicle.key)}>
                  Xóa
                </button>
              </article>
            );
          })}
        </div>
      ) : null}
    </section>
  );
}
