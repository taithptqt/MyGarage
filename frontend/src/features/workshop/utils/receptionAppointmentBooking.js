import {
  createCustomer,
  createCustomerVehicle,
  createVehicle,
} from "@features/admin/api/customerApi";
import { createBookingGroup } from "@features/appointments/api/bookingGroupApi";
import { bookAppointment, cancelAppointment } from "@features/appointments/api/appointmentApi";

const ERRORS = {
  invalidBranch: "Chi nh\u00e1nh hi\u1ec7n t\u1ea1i kh\u00f4ng h\u1ee3p l\u1ec7.",
  invalidDateTime: "Ng\u00e0y gi\u1edd h\u1eb9n kh\u00f4ng h\u1ee3p l\u1ec7.",
  missingGuestModel: "Vui l\u00f2ng ch\u1ecdn d\u00f2ng xe cho kh\u00e1ch v\u00e3ng lai.",
  invalidCustomer: "Kh\u00e1ch h\u00e0ng kh\u00f4ng h\u1ee3p l\u1ec7.",
  missingNewVehicleModel: "Vui l\u00f2ng ch\u1ecdn d\u00f2ng xe m\u1edbi.",
  missingVehicle: "Vui l\u00f2ng ch\u1ecdn xe c\u1ee7a kh\u00e1ch.",
  vehicleNotFound: "Kh\u00f4ng t\u00ecm th\u1ea5y xe \u0111\u00e3 ch\u1ecdn.",
  replaceReason: "\u0110i\u1ec1u ch\u1ec9nh l\u1ecbch h\u1eb9n b\u1edfi l\u1ec5 t\u00e2n",
};

function toNumberList(value) {
  if (!Array.isArray(value)) return [];
  return value.map(Number).filter((id) => id > 0);
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
      (vehicle) => String(vehicle.licensePlate || "").trim().toUpperCase() === normalizedPlate,
    ) || null
  );
}

function buildAppointmentBasePayload({
  effectiveBranchId,
  customerId,
  customerFullName,
  customerPhone,
  startAt,
  endAt,
  requestedServiceIds,
  customerNotes,
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
    customerNotes,
  };
}

export async function saveReceptionAppointment({
  effectiveBranchId,
  formData,
  customers,
  vehicles,
  customerVehicles,
  services,
  replaceAppointmentId = 0,
}) {
  if (!effectiveBranchId) {
    throw new Error(ERRORS.invalidBranch);
  }

  const startAt = new Date(`${formData.appointmentDate}T${formData.startTime}`);
  if (Number.isNaN(startAt.getTime())) {
    throw new Error(ERRORS.invalidDateTime);
  }

  const durationMinutes = Math.max(Number(formData.totalMinutes || 0), 30);
  const endAt = new Date(startAt.getTime() + durationMinutes * 60 * 1000);
  const serviceIds = toNumberList(formData.serviceIds);
  const requestedServiceIds = serviceIds.length > 0 ? serviceIds.join(",") : null;
  const customerNotes = String(formData.customerNotes || "").trim() || null;
  const selectedServiceObjects = (services || []).filter((service) =>
    serviceIds.includes(Number(service.id)),
  );

  let successSummary = null;

  if (formData.isQuickOrder) {
    let customer = (customers || []).find(
      (item) => String(item.phone || "").trim() === String(formData.guestPhone || "").trim(),
    );
    if (!customer) {
      customer = await createCustomer({
        fullName: formData.guestName,
        phone: formData.guestPhone,
        isRegistered: false,
        status: "active",
      });
    }

    const modelId = Number(formData.guestVehicleModelId || 0);
    if (!modelId) {
      throw new Error(ERRORS.missingGuestModel);
    }

    let vehicle = findVehicleByPlate(vehicles, formData.guestVehiclePlate);
    if (!vehicle) {
      vehicle = await createVehicle({
        licensePlate: formData.guestVehiclePlate,
        modelId,
        color: formData.guestVehicleColor || null,
        vin: formData.guestVehicleVin || null,
        year: formData.guestVehicleYear ? Number(formData.guestVehicleYear) : null,
        status: "active",
      });
    }

    const linked = (customerVehicles || []).some(
      (item) =>
        Number(item.customerId) === Number(customer.id) &&
        Number(item.vehicleId) === Number(vehicle.id),
    );
    if (!linked) {
      await createCustomerVehicle({
        customerId: Number(customer.id),
        vehicleId: Number(vehicle.id),
        isCurrentOwner: true,
        ownershipStartDate: new Date().toISOString().slice(0, 10),
      });
    }

    const payload = {
      ...buildAppointmentBasePayload({
        effectiveBranchId,
        customerId: Number(customer.id),
        customerFullName: formData.guestName,
        customerPhone: formData.guestPhone,
        startAt,
        endAt,
        requestedServiceIds,
        customerNotes,
      }),
      vehicleId: Number(vehicle.id),
      vehicleModelId: modelId,
      licensePlate: formData.guestVehiclePlate,
      vin: formData.guestVehicleVin,
      color: formData.guestVehicleColor || null,
    };

    const created = await bookAppointment(payload);
    successSummary = created || payload;
  } else {
    const customerId = Number(formData.customerId || 0);
    const customer = findCustomerById(customers, customerId);
    if (!customerId || !customer) {
      throw new Error(ERRORS.invalidCustomer);
    }

    if (formData.isNewVehicle) {
      const modelId = Number(formData.vehicleModelId || 0);
      if (!modelId) {
        throw new Error(ERRORS.missingNewVehicleModel);
      }

      const newVehicle = await createVehicle({
        licensePlate: formData.vehicleLicensePlate,
        modelId,
        color: formData.vehicleColor || null,
        vin: formData.vehicleVin || null,
        year: formData.vehicleYear ? Number(formData.vehicleYear) : null,
        status: "active",
      });

      await createCustomerVehicle({
        customerId,
        vehicleId: Number(newVehicle.id),
        isCurrentOwner: true,
        ownershipStartDate: new Date().toISOString().slice(0, 10),
      });

      const payload = {
        ...buildAppointmentBasePayload({
          effectiveBranchId,
          customerId,
          customerFullName: customer.fullName || formData.customerName,
          customerPhone: customer.phone || formData.customerPhone,
          startAt,
          endAt,
          requestedServiceIds,
          customerNotes,
        }),
        vehicleId: Number(newVehicle.id),
        vehicleModelId: modelId,
        licensePlate: formData.vehicleLicensePlate,
        vin: formData.vehicleVin,
        color: formData.vehicleColor || null,
      };

      const created = await bookAppointment(payload);
      successSummary = created || payload;
    } else {
      const selectedVehicleIds = toNumberList(formData.selectedVehicleIds);
      if (selectedVehicleIds.length === 0) {
        throw new Error(ERRORS.missingVehicle);
      }

      if (selectedVehicleIds.length === 1) {
        const vehicle = findVehicleById(vehicles, selectedVehicleIds[0]);
        if (!vehicle) {
          throw new Error(ERRORS.vehicleNotFound);
        }
        const payload = {
          ...buildAppointmentBasePayload({
            effectiveBranchId,
            customerId,
            customerFullName: customer.fullName || formData.customerName,
            customerPhone: customer.phone || formData.customerPhone,
            startAt,
            endAt,
            requestedServiceIds,
            customerNotes,
          }),
          vehicleId: Number(vehicle.id),
        };

        const created = await bookAppointment(payload);
        successSummary = created || payload;
      } else {
        const items = selectedVehicleIds.map((vehicleId) => {
          const vehicle = findVehicleById(vehicles, vehicleId);
          return {
            ...buildAppointmentBasePayload({
              effectiveBranchId,
              customerId,
              customerFullName: customer.fullName || formData.customerName,
              customerPhone: customer.phone || formData.customerPhone,
              startAt,
              endAt,
              requestedServiceIds,
              customerNotes,
            }),
            vehicleId,
            licensePlate: vehicle?.licensePlate || null,
            vehicleModelId: vehicle?.modelId ? Number(vehicle.modelId) : null,
            vin: vehicle?.vin || null,
            color: vehicle?.color || null,
          };
        });

        const grouped = await createBookingGroup({
          branchId: effectiveBranchId,
          customerId,
          customerNote: customerNotes,
          items,
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
    groupVehicleCount: Number(formData.selectedVehicleIds?.length || 0),
    updateMode: shouldReplace,
  };
}
