export const EMPTY_STAFF_FORM = {
  fullName: "",
  phone: "",
  email: "",
  password: "",
  roleId: "",
  branchId: "",
  status: "ACTIVE"
};

export const EMPTY_CUSTOMER_FORM = {
  fullName: "",
  phone: "",
  email: "",
  googleId: "",
  type: "GUEST",
  password: "",
  tierId: "",
  brandId: "",
  modelId: "",
  licensePlate: "",
  color: "",
  manufactureYear: "",
  note: "",
  isActive: true
};

export const EMPTY_BRAND_FORM = {
  code: "",
  name: "",
  countryOfOrigin: "",
  logoUrl: "",
  isActive: true
};

export const EMPTY_MODEL_FORM = {
  brandId: "",
  modelName: "",
  segment: "",
  fuelType: "",
  cylinderCapacity: ""
};

export const EMPTY_SERVICE_FORM = {
  code: "",
  name: "",
  categoryId: "",
  basePrice: "",
  standardMinutes: "",
  description: "",
  isActive: true
};

export const SECTION_META = {
  staff: {
    title: "Staff Management",
    subtitle: "Manage internal staff accounts and role assignment",
    addLabel: "+ Add Staff",
    searchPlaceholder: "Search staff by name, email, phone or role..."
  },
  customers: {
    title: "Users (Customers)",
    subtitle: "Manage customer accounts only",
    addLabel: "+ Add Customer",
    searchPlaceholder: "Search by name, phone, tier, plate, brand or model..."
  },
  brands: {
    title: "Vehicle Brands",
    subtitle: "Manage car brands for service catalog",
    addLabel: "+ Add Brand",
    searchPlaceholder: "Search by code, brand name or country..."
  },
  models: {
    title: "Vehicle Models",
    subtitle: "Manage car models for each brand",
    addLabel: "+ Add Model",
    searchPlaceholder: "Search by model, segment or fuel type..."
  },
  services: {
    title: "Service Management",
    subtitle: "Manage repair and maintenance service catalog",
    addLabel: "+ Add Service",
    searchPlaceholder: "Search by code, service name, category or status..."
  }
};
