import {
  bookAppointment,
  getAllBranches,
  getAllBranchOperatingHours,
  getAllCustomerVehicles,
  getAllCustomers,
  getAllInsuranceCompanies,
  getAllPackages,
  getAllServiceCategories,
  getAllStandardServices,
  getAllVehicles,
  getAvailableSlots,
  createSlotHold,
  releaseSlotHold,
  getVehicleBrands,
  getVehicleModels,
  uploadAppointmentInsuranceAsset,
} from "@features/appointments/api/appointmentApi";
import { createGroupSession } from "@features/appointments/api/groupSessionApi";
import { normalizeText } from "@features/appointments/utils/bookingCommon";
import { mergeBookableServicesAndPackages } from "@features/appointments/utils/bookingPackages";

export async function loadBookingFormMasterData() {
  const [
    categoryData,
    serviceData,
    packageData,
    branchData,
    operatingHoursData,
    brandData,
    modelData,
    insuranceCompanyData,
    customerData,
    customerVehicleData,
    vehicleData,
  ] = await Promise.all([
    getAllServiceCategories(),
    getAllStandardServices(),
    getAllPackages().catch(() => []),
    getAllBranches(),
    getAllBranchOperatingHours().catch(() => []),
    getVehicleBrands(),
    getVehicleModels(),
    getAllInsuranceCompanies().catch(() => []),
    getAllCustomers(),
    getAllCustomerVehicles(),
    getAllVehicles(),
  ]);

  return {
    categories: Array.isArray(categoryData) ? categoryData.filter((item) => item?.isDeleted !== true) : [],
    services: mergeBookableServicesAndPackages(
      Array.isArray(serviceData) ? serviceData.filter((item) => item?.isDeleted !== true) : [],
      Array.isArray(packageData) ? packageData.filter((item) => item?.isDeleted !== true) : []
    ),
    packages: Array.isArray(packageData) ? packageData.filter((item) => item?.isDeleted !== true) : [],
    branches: Array.isArray(branchData) ? branchData.filter((item) => normalizeText(item?.status) === "active") : [],
    branchOperatingHours: Array.isArray(operatingHoursData) ? operatingHoursData : [],
    brands: Array.isArray(brandData) ? brandData.filter((item) => item?.isDeleted !== true) : [],
    models: Array.isArray(modelData) ? modelData.filter((item) => item?.isDeleted !== true) : [],
    insuranceCompanies: Array.isArray(insuranceCompanyData)
      ? insuranceCompanyData.filter((item) => item?.isDeleted !== true)
      : [],
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
  requestedPackageIds,
  bookingFlow,
  vehicleCount = 1,
  isParallel = true,
}) {
  const mergedRequestedServiceIds =
    requestedServiceIds || (serviceIds?.length ? serviceIds.join(",") : null);
  return getAvailableSlots(Number(branchId), date, {
    requestedServiceIds: mergedRequestedServiceIds,
    requestedPackageIds,
    bookingFlow,
    vehicleCount,
    isParallel,
  });
}

export async function submitSingleBooking(payload) {
  return bookAppointment(payload);
}

export async function submitBatchBooking(payload) {
  return createGroupSession(payload);
}

export async function createBookingSlotHold(payload) {
  return createSlotHold(payload);
}

export async function releaseBookingSlotHold(holdToken) {
  return releaseSlotHold(holdToken);
}

export async function uploadBookingInsuranceAsset(file, assetType) {
  return uploadAppointmentInsuranceAsset(file, assetType);
}
