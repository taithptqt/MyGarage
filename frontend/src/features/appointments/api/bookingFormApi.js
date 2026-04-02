import {
  bookAppointment,
  getAllBranches,
  getAllCustomerVehicles,
  getAllCustomers,
  getAllServiceCategories,
  getAllStandardServices,
  getAllVehicles,
  getAvailableSlots,
  createSlotHold,
  releaseSlotHold,
  getVehicleBrands,
  getVehicleModels,
} from "@features/appointments/api/appointmentApi";
import { createBookingGroup } from "@features/appointments/api/bookingGroupApi";
import { normalizeText } from "@features/appointments/utils/bookingCommon";

export async function loadBookingFormMasterData() {
  const [
    categoryData,
    serviceData,
    branchData,
    brandData,
    modelData,
    customerData,
    customerVehicleData,
    vehicleData,
  ] = await Promise.all([
    getAllServiceCategories(),
    getAllStandardServices(),
    getAllBranches(),
    getVehicleBrands(),
    getVehicleModels(),
    getAllCustomers(),
    getAllCustomerVehicles(),
    getAllVehicles(),
  ]);

  return {
    categories: Array.isArray(categoryData) ? categoryData.filter((item) => item?.isDeleted !== true) : [],
    services: Array.isArray(serviceData) ? serviceData.filter((item) => item?.isDeleted !== true) : [],
    branches: Array.isArray(branchData) ? branchData.filter((item) => normalizeText(item?.status) === "active") : [],
    brands: Array.isArray(brandData) ? brandData.filter((item) => item?.isDeleted !== true) : [],
    models: Array.isArray(modelData) ? modelData.filter((item) => item?.isDeleted !== true) : [],
    customers: Array.isArray(customerData) ? customerData : [],
    customerVehicles: Array.isArray(customerVehicleData) ? customerVehicleData : [],
    vehicles: Array.isArray(vehicleData) ? vehicleData : [],
  };
}

export async function loadBookingAvailableSlots({
  branchId,
  date,
  serviceIds,
  requestedServiceIds,
  bookingFlow,
  vehicleCount = 1,
  isParallel = true,
}) {
  const mergedRequestedServiceIds =
    requestedServiceIds || (serviceIds?.length ? serviceIds.join(",") : null);
  return getAvailableSlots(Number(branchId), date, {
    requestedServiceIds: mergedRequestedServiceIds,
    bookingFlow,
    vehicleCount,
    isParallel,
  });
}

export async function submitSingleBooking(payload) {
  return bookAppointment(payload);
}

export async function submitBatchBooking(payload) {
  return createBookingGroup(payload);
}

export async function createBookingSlotHold(payload) {
  return createSlotHold(payload);
}

export async function releaseBookingSlotHold(holdToken) {
  return releaseSlotHold(holdToken);
}
