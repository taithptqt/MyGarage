import React from 'react';

import { Icon } from '@chakra-ui/react';
import {
  MdBuild,
  MdCalendarToday,
  MdCategory,
  MdDirectionsCar,
  MdHome,
  MdPeople,
  MdPerson,
  MdSettings,
  MdLayers,
  MdOutlineCategory,
  MdWarehouse,
  MdPolicy,
  MdVisibility,
  MdFactCheck,
  MdSwapHoriz,
  MdVerifiedUser,
  MdWarning,
  MdAssessment,
  MdHistory,
} from 'react-icons/md';

import MainDashboard from "views/admin/default";
import AdminPage from "../features/admin/pages/AdminPage";
import InventoryAdminPage from "../features/inventory/pages/InventoryAdminPage";
import ServicesPage from "../features/services/pages/ServicesPage";
import ServiceCategoriesPage from "../features/services/pages/ServiceCategoriesPage";
import AppointmentManagementPage from "../features/appointments/pages/AppointmentManagementPage";
import AdminChangeLogPage from "../features/admin/pages/AdminChangeLogPage";

const inventoryItems = [
  {
    name: 'Cấu trúc kho',
    layout: '/admin',
    icon: <Icon as={MdWarehouse} width="20px" height="20px" color="inherit" />,
    path: '/inventory/warehouse-structure',
    component: <InventoryAdminPage section="warehouseStructure" />,
  },
  {
    name: 'Sản phẩm',
    layout: '/admin',
    icon: <Icon as={MdOutlineCategory} width="20px" height="20px" color="inherit" />,
    path: '/inventory/catalog',
    component: <InventoryAdminPage section="catalogProducts" />,
  },
  {
    name: 'Danh mục',
    layout: '/admin',
    icon: <Icon as={MdLayers} width="20px" height="20px" color="inherit" />,
    path: '/inventory/categories',
    component: <InventoryAdminPage section="catalogCategories" />,
  },
  {
    name: 'Nhà cung cấp',
    layout: '/admin',
    icon: <Icon as={MdPeople} width="20px" height="20px" color="inherit" />,
    path: '/inventory/suppliers',
    component: <InventoryAdminPage section="catalogSuppliers" />,
  },
  {
    name: 'Chính sách tồn',
    layout: '/admin',
    icon: <Icon as={MdPolicy} width="20px" height="20px" color="inherit" />,
    path: '/inventory/policy',
    component: <InventoryAdminPage section="policy" />,
  },
  {
    name: 'Giám sát kho',
    layout: '/admin',
    icon: <Icon as={MdVisibility} width="20px" height="20px" color="inherit" />,
    path: '/inventory/monitoring',
    component: <InventoryAdminPage section="monitoring" />,
  },
  {
    name: 'Kiểm kê',
    layout: '/admin',
    icon: <Icon as={MdFactCheck} width="20px" height="20px" color="inherit" />,
    path: '/inventory/audits',
    component: <InventoryAdminPage section="audits" />,
  },
  {
    name: 'Lịch sử nhập xuất',
    layout: '/admin',
    icon: <Icon as={MdSwapHoriz} width="20px" height="20px" color="inherit" />,
    path: '/inventory/transactions',
    component: <InventoryAdminPage section="transactions" />,
  },
  {
    name: 'Phê duyệt ngoại lệ',
    layout: '/admin',
    icon: <Icon as={MdVerifiedUser} width="20px" height="20px" color="inherit" />,
    path: '/inventory/approvals',
    component: <InventoryAdminPage section="approvals" />,
  },
  {
    name: 'Cảnh báo',
    layout: '/admin',
    icon: <Icon as={MdWarning} width="20px" height="20px" color="inherit" />,
    path: '/inventory/alerts',
    component: <InventoryAdminPage section="alerts" />,
  },
  {
    name: 'Báo cáo kho',
    layout: '/admin',
    icon: <Icon as={MdAssessment} width="20px" height="20px" color="inherit" />,
    path: '/inventory/reports',
    component: <InventoryAdminPage section="reports" />,
  },
];

const vehicleItems = [
  {
    name: 'Hãng xe',
    layout: '/admin',
    icon: <Icon as={MdDirectionsCar} width="20px" height="20px" color="inherit" />,
    path: '/brands',
    component: <AdminPage embedded initialSection="brands" />,
  },
  {
    name: 'Dòng xe',
    layout: '/admin',
    icon: <Icon as={MdCategory} width="20px" height="20px" color="inherit" />,
    path: '/models',
    component: <AdminPage embedded initialSection="models" />,
  },
];

const serviceItems = [
  {
    name: 'Dịch vụ',
    layout: '/admin',
    icon: <Icon as={MdBuild} width="20px" height="20px" color="inherit" />,
    path: '/services',
    component: <ServicesPage />,
  },
  {
    name: 'Danh mục dịch vụ',
    layout: '/admin',
    icon: <Icon as={MdLayers} width="20px" height="20px" color="inherit" />,
    path: '/service-categories',
    component: <ServiceCategoriesPage />,
  },
];

const routes = [
  {
    name: 'Bảng điều khiển',
    layout: '/admin',
    path: '/default',
    icon: <Icon as={MdHome} width="20px" height="20px" color="inherit" />,
    component: <MainDashboard />,
  },
  {
    name: 'Lịch hẹn',
    layout: '/admin',
    path: '/appointments',
    icon: <Icon as={MdCalendarToday} width="20px" height="20px" color="inherit" />,
    component: <AppointmentManagementPage />,
  },
  {
    name: 'Chi nhánh',
    layout: '/admin',
    path: '/branches',
    icon: <Icon as={MdSettings} width="20px" height="20px" color="inherit" />,
    component: <AdminPage embedded initialSection="branches" />,
  },
  {
    name: 'Nhân sự',
    layout: '/admin',
    path: '/staff',
    icon: <Icon as={MdPeople} width="20px" height="20px" color="inherit" />,
    component: <AdminPage embedded initialSection="staff" />,
  },
  {
    name: 'Khách hàng',
    layout: '/admin',
    icon: <Icon as={MdPerson} width="20px" height="20px" color="inherit" />,
    path: '/customers',
    component: <AdminPage embedded initialSection="customers" />,
  },
  {
    name: 'Xe',
    icon: <Icon as={MdDirectionsCar} width="20px" height="20px" color="inherit" />,
    collapse: true,
    items: vehicleItems,
  },
  {
    name: 'Dịch vụ',
    icon: <Icon as={MdBuild} width="20px" height="20px" color="inherit" />,
    collapse: true,
    items: serviceItems,
  },
  {
    name: 'Nhật ký chỉnh sửa',
    layout: '/admin',
    icon: <Icon as={MdHistory} width="20px" height="20px" color="inherit" />,
    path: '/change-logs',
    component: <AdminChangeLogPage />,
  },
  {
    name: 'Kho',
    icon: <Icon as={MdWarehouse} width="20px" height="20px" color="inherit" />,
    collapse: true,
    items: inventoryItems,
  },
];

export default routes;

