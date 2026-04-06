import {
  createCustomer,
  createCustomerVehicle,
  createVehicle
} from "@features/admin/api/customerApi";
import { createGroupSession } from "@features/appointments/api/groupSessionApi";
import { bookAppointment, cancelAppointment } from "@features/appointments/api/appointmentApi";
import { buildCustomerNotesPayload } from "@features/appointments/utils/bookingCommon";

const ERRORS = {
  invalidBranch: "Chi nhánh hiện tại không hợp lệ.",
  invalidDateTime: "Ngày giờ hẹn không hợp lệ.",
  missingGuestModel: "Vui lòng chọn dòng xe cho khách vãng lai.",
  invalidCustomer: "Khách hàng không hợp lệ.",
  missingNewVehicleModel: "Vui lòng chọn dòng xe mới.",
  missingVehicle: "Vui lòng chọn xe của khách.",
  vehicleNotFound: "Không tìm thấy xe đã chọn.",
  replaceReason: "Điều chỉnh lịch hẹn bởi lễ tân"
};

function toNumberList(value) {
  if (!Array.isArray(value)) return [];
  return Array.from(
    new Set(
      value
        .map(Number)
        .filter((id) => Number.isFinite(id) && id > 0)
    )
  );
}

function findCustomerById(customers, id) {
  return (customers || []).find((customer) => Number(customer.id) === Number(id)) || null;
}

function findVehicleById(vehicles, id) {
  return (vehicles || []).find((vehicle) => Number(vehicle.id) === Number(id)) || null;
}

function findVehicleByPlate(vehicles, plate) {
  const normalizedPlate = String(plate || "").trim().toUpperCase();
  if (!normalizedPlate) return null;
  return (
    (vehicles || []).find(
      (vehicle) => String(vehicle.licensePlate || "").trim().toUpperCase() === normalizedPlate
    ) || null
  );
}

function addMinutes(date, minutes) {
  return new Date(date.getTime() + Math.max(Number(minutes || 0), 0) * 60 * 1000);
}

function splitCustomerNotesInput(value) {
  return String(value || "")
    .split(/\r?\n|;/g)
    .map((item) => String(item || "").trim())
    .filter(Boolean);
}

function normalizeServiceLookup(services) {
  const map = new Map();
  (services || []).forEach((service) => {
    const id = Number(service?.id ?? service?.Id);
    if (!Number.isFinite(id) || id <= 0) return;
    map.set(id, {
      ...service,
      id,
      minutes: Math.max(0, Number(service?.standardMinutes ?? service?.StandardMinutes ?? 0) || 0)
    });
  });
  return map;
}

function normalizeVehicleServicePlanMaps(vehicleServicePlans, serviceLookup) {
  const byVehicleKey = new Map();
  const byVehicleId = new Map();

  (Array.isArray(vehicleServicePlans) ? vehicleServicePlans : []).forEach((rawPlan) => {
    const vehicleKey = String(rawPlan?.vehicleKey || "").trim() || null;
    const vehicleId = Number(rawPlan?.vehicleId);
    const serviceIds = toNumberList(rawPlan?.serviceIds);
    const computedMinutes = serviceIds.reduce((sum, serviceId) => {
      const service = serviceLookup.get(serviceId);
      return sum + Number(service?.minutes || 0);
    }, 0);
    const totalMinutes = Number(rawPlan?.totalMinutes);
    const resolvedMinutes = Number.isFinite(totalMinutes) && totalMinutes > 0 ? totalMinutes : computedMinutes;

    const plan = {
      serviceIds,
      requestedServiceIds: serviceIds.length > 0 ? serviceIds.join(",") : null,
      totalMinutes: Math.max(resolvedMinutes, 30)
    };

    if (vehicleKey) byVehicleKey.set(vehicleKey, plan);
    if (Number.isFinite(vehicleId) && vehicleId > 0) byVehicleId.set(vehicleId, plan);
  });

  return { byVehicleKey, byVehicleId };
}

function resolveVehiclePlan({ vehicleKey, vehicleId, defaultServiceIds, defaultDuration, planMaps }) {
  const byKeyPlan = vehicleKey ? planMaps.byVehicleKey.get(vehicleKey) : null;
  const byIdPlan = Number.isFinite(vehicleId) && vehicleId > 0 ? planMaps.byVehicleId.get(vehicleId) : null;
  const plan = byKeyPlan || byIdPlan;

  const serviceIds = plan?.serviceIds?.length > 0 ? plan.serviceIds : defaultServiceIds;
  const requestedServiceIds = serviceIds.length > 0 ? serviceIds.join(",") : null;
  const totalMinutes = Math.max(Number(plan?.totalMinutes || defaultDuration || 30), 30);

  return { serviceIds, requestedServiceIds, totalMinutes };
}

function buildAppointmentBasePayload({
  effectiveBranchId,
  customerId,
  customerFullName,
  customerPhone,
  startAt,
  endAt,
  requestedServiceIds,
  customerNotes
}) {
  return {
    branchId: effectiveBranchId,
    customerId,
    customerFullName,
    customerPhone,
    bookingFlow: "FIXED",
    startTime: startAt.toISOString(),
    endTime: endAt.toISOString(),
    checkInWindowStart: null,
    checkInWindowEnd: null,
    requestedServiceIds,
    customerNotes
  };
}

export async function saveReceptionAppointment({
  effectiveBranchId,
  formData,
  customers,
  vehicles,
  customerVehicles,
  services,
  replaceAppointmentId = 0
}) {
  if (!effectiveBranchId) throw new Error(ERRORS.invalidBranch);

  const startAt = new Date(`${formData.appointmentDate}T${formData.startTime}`);
  if (Number.isNaN(startAt.getTime())) throw new Error(ERRORS.invalidDateTime);

  const serviceLookup = normalizeServiceLookup(services);
  const fallbackServiceIds = toNumberList(formData.serviceIds);
  const fallbackDuration = Math.max(Number(formData.totalMinutes || 0), 30);
  const splitCustomerNotes = splitCustomerNotesInput(formData.customerNotes);
  const customerNotes =
    buildCustomerNotesPayload(splitCustomerNotes) || String(formData.customerNotes || "").trim() || null;
  const planMaps = normalizeVehicleServicePlanMaps(formData.vehicleServicePlans, serviceLookup);
  const selectedServiceIdsUnion = toNumberList([
    ...fallbackServiceIds,
    ...(Array.isArray(formData.vehicleServicePlans)
      ? formData.vehicleServicePlans.flatMap((plan) => toNumberList(plan?.serviceIds))
      : [])
  ]);
  const selectedServiceObjects = selectedServiceIdsUnion.map((serviceId) => serviceLookup.get(serviceId)).filter(Boolean);

  let successSummary = null;

  if (formData.isQuickOrder) {
    let customer = (customers || []).find(
      (item) => String(item.phone || "").trim() === String(formData.guestPhone || "").trim()
    );
    if (!customer) {
      customer = await createCustomer({
        fullName: formData.guestName,
        phone: formData.guestPhone,
        isRegistered: false,
        status: "active"
      });
    }

    const modelId = Number(formData.guestVehicleModelId || 0);
    if (!modelId) throw new Error(ERRORS.missingGuestModel);

    let vehicle = findVehicleByPlate(vehicles, formData.guestVehiclePlate);
    if (!vehicle) {
      vehicle = await createVehicle({
        licensePlate: formData.guestVehiclePlate,
        modelId,
        color: formData.guestVehicleColor || null,
        vin: formData.guestVehicleVin || null,
        year: formData.guestVehicleYear ? Number(formData.guestVehicleYear) : null,
        status: "active"
      });
    }

    const linked = (customerVehicles || []).some(
      (item) =>
        Number(item.customerId) === Number(customer.id) &&
        Number(item.vehicleId) === Number(vehicle.id)
    );
    if (!linked) {
      await createCustomerVehicle({
        customerId: Number(customer.id),
        vehicleId: Number(vehicle.id),
        isCurrentOwner: true,
        ownershipStartDate: new Date().toISOString().slice(0, 10)
      });
    }

    const plan = resolveVehiclePlan({
      vehicleKey: "guest",
      vehicleId: Number(vehicle.id),
      defaultServiceIds: fallbackServiceIds,
      defaultDuration: fallbackDuration,
      planMaps
    });
    const endAt = addMinutes(startAt, plan.totalMinutes);

    const payload = {
      ...buildAppointmentBasePayload({
        effectiveBranchId,
        customerId: Number(customer.id),
        customerFullName: formData.guestName,
        customerPhone: formData.guestPhone,
        startAt,
        endAt,
        requestedServiceIds: plan.requestedServiceIds,
        customerNotes
      }),
      vehicleId: Number(vehicle.id),
      vehicleModelId: modelId,
      licensePlate: formData.guestVehiclePlate,
      vin: formData.guestVehicleVin,
      color: formData.guestVehicleColor || null
    };

    const created = await bookAppointment(payload);
    successSummary = created || payload;
  } else {
    const customerId = Number(formData.customerId || 0);
    const customer = findCustomerById(customers, customerId);
    if (!customerId || !customer) throw new Error(ERRORS.invalidCustomer);

    if (formData.isNewVehicle) {
      const modelId = Number(formData.vehicleModelId || 0);
      if (!modelId) throw new Error(ERRORS.missingNewVehicleModel);

      const newVehicle = await createVehicle({
        licensePlate: formData.vehicleLicensePlate,
        modelId,
        color: formData.vehicleColor || null,
        vin: formData.vehicleVin || null,
        year: formData.vehicleYear ? Number(formData.vehicleYear) : null,
        status: "active"
      });

      await createCustomerVehicle({
        customerId,
        vehicleId: Number(newVehicle.id),
        isCurrentOwner: true,
        ownershipStartDate: new Date().toISOString().slice(0, 10)
      });

      const plan = resolveVehiclePlan({
        vehicleKey: "new-customer",
        vehicleId: Number(newVehicle.id),
        defaultServiceIds: fallbackServiceIds,
        defaultDuration: fallbackDuration,
        planMaps
      });
      const endAt = addMinutes(startAt, plan.totalMinutes);

      const payload = {
        ...buildAppointmentBasePayload({
          effectiveBranchId,
          customerId,
          customerFullName: customer.fullName || formData.customerName,
          customerPhone: customer.phone || formData.customerPhone,
          startAt,
          endAt,
          requestedServiceIds: plan.requestedServiceIds,
          customerNotes
        }),
        vehicleId: Number(newVehicle.id),
        vehicleModelId: modelId,
        licensePlate: formData.vehicleLicensePlate,
        vin: formData.vehicleVin,
        color: formData.vehicleColor || null
      };

      const created = await bookAppointment(payload);
      successSummary = created || payload;
    } else {
      const selectedVehicleIds = toNumberList(formData.selectedVehicleIds);
      if (selectedVehicleIds.length === 0) throw new Error(ERRORS.missingVehicle);

      if (selectedVehicleIds.length === 1) {
        const vehicle = findVehicleById(vehicles, selectedVehicleIds[0]);
        if (!vehicle) throw new Error(ERRORS.vehicleNotFound);

        const plan = resolveVehiclePlan({
          vehicleKey: `existing-${selectedVehicleIds[0]}`,
          vehicleId: selectedVehicleIds[0],
          defaultServiceIds: fallbackServiceIds,
          defaultDuration: fallbackDuration,
          planMaps
        });
        const endAt = addMinutes(startAt, plan.totalMinutes);

        const payload = {
          ...buildAppointmentBasePayload({
            effectiveBranchId,
            customerId,
            customerFullName: customer.fullName || formData.customerName,
            customerPhone: customer.phone || formData.customerPhone,
            startAt,
            endAt,
            requestedServiceIds: plan.requestedServiceIds,
            customerNotes
          }),
          vehicleId: Number(vehicle.id)
        };

        const created = await bookAppointment(payload);
        successSummary = created || payload;
      } else {
        const items = selectedVehicleIds.map((vehicleId) => {
          const vehicle = findVehicleById(vehicles, vehicleId);
          const plan = resolveVehiclePlan({
            vehicleKey: `existing-${vehicleId}`,
            vehicleId,
            defaultServiceIds: fallbackServiceIds,
            defaultDuration: fallbackDuration,
            planMaps
          });
          const endAt = addMinutes(startAt, plan.totalMinutes);

          return {
            ...buildAppointmentBasePayload({
              effectiveBranchId,
              customerId,
              customerFullName: customer.fullName || formData.customerName,
              customerPhone: customer.phone || formData.customerPhone,
              startAt,
              endAt,
              requestedServiceIds: plan.requestedServiceIds,
              customerNotes
            }),
            vehicleId,
            licensePlate: vehicle?.licensePlate || null,
            vehicleModelId: vehicle?.modelId ? Number(vehicle.modelId) : null,
            vin: vehicle?.vin || null,
            color: vehicle?.color || null
          };
        });

        const grouped = await createGroupSession({
          branchId: effectiveBranchId,
          customerId,
          customerNote: customerNotes,
          items
        });

        const groupedAppointments = Array.isArray(grouped?.appointments)
          ? grouped.appointments
          : Array.isArray(grouped?.Appointments)
            ? grouped.Appointments
            : [];
        successSummary = groupedAppointments[0] || items[0];
      }
    }
  }

  const shouldReplace = Number(replaceAppointmentId) > 0;
  if (shouldReplace) {
    await cancelAppointment(Number(replaceAppointmentId), ERRORS.replaceReason);
  }

  return {
    customerName:
      successSummary?.customerName ||
      successSummary?.CustomerName ||
      formData.customerName ||
      formData.guestName ||
      "",
    customerPhone:
      successSummary?.customerPhone ||
      successSummary?.CustomerPhone ||
      formData.customerPhone ||
      formData.guestPhone ||
      "",
    vehicleLicensePlate:
      successSummary?.vehicleLicensePlate ||
      successSummary?.VehicleLicensePlate ||
      successSummary?.licensePlate ||
      formData.vehicleLicensePlate ||
      formData.guestVehiclePlate ||
      "",
    startTime: successSummary?.startTime || successSummary?.StartTime || startAt.toISOString(),
    services: selectedServiceObjects,
    groupVehicleCount: Math.max(1, Number(formData.selectedVehicleIds?.length || 0)),
    updateMode: shouldReplace
  };
}
