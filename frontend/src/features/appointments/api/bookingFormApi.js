import {
  bookAppointment,
  getAllBranches,
  getAllCustomerVehicles,
  getAllCustomers,
  getAllServiceCategories,
  getAllStandardServices,
  getAllVehicles,
  getAvailableSlots,
  getVehicleBrands,
  getVehicleModels,
} from "./appointmentApi";
import { createBookingGroup } from "./bookingGroupApi";
import { normalizeText } from "../utils/bookingCommon";

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

export async function loadBookingAvailableSlots({ branchId, date, serviceIds, bookingFlow }) {
  return getAvailableSlots(Number(branchId), date, {
    requestedServiceIds: serviceIds?.length ? serviceIds.join(",") : null,
    bookingFlow,
  });
}

export async function submitSingleBooking(payload) {
  return bookAppointment(payload);
}

export async function submitBatchBooking(payload) {
  return createBookingGroup(payload);
}
