import React from 'react';

import { Icon } from '@chakra-ui/react';
import {
  MdBarChart,
  MdBuild,
  MdCategory,
  MdDirectionsCar,
  MdHome,
  MdLock,
  MdPeople,
  MdPerson,
  MdSettings,
  MdInventory,
  MdLayers,
  MdOutlineCategory,
} from 'react-icons/md';

// Admin Imports
import MainDashboard from "views/admin/default";
import AdminPage from "../features/admin/pages/AdminPage";
import InventoryPage from "../features/admin/pages/InventoryPage";
import InventoryCategoryPage from "../features/admin/pages/InventoryCategoryPage";
import ServicePage from "../features/admin/pages/ServicePage";
import ServiceCategoryPage from "../features/admin/pages/ServiceCategoryPage";
import RTL from 'views/admin/rtl';

// Auth Imports
import SignInCentered from 'views/auth/signIn';

const routes = [
  {
    name: 'Main Dashboard',
    layout: '/admin',
    path: '/default',
    icon: <Icon as={MdHome} width="20px" height="20px" color="inherit" />,
    component: <MainDashboard />,
  },
  {
    name: 'Staff',
    layout: '/admin',
    path: '/staff',
    icon: <Icon as={MdPeople} width="20px" height="20px" color="inherit" />,
    component: <AdminPage embedded initialSection="staff" />,
  },
  {
    name: 'Customers',
    layout: '/admin',
    icon: <Icon as={MdPerson} width="20px" height="20px" color="inherit" />,
    path: '/customers',
    component: <AdminPage embedded initialSection="customers" />,
  },
  {
    name: 'Vehicle Brands',
    layout: '/admin',
    icon: <Icon as={MdDirectionsCar} width="20px" height="20px" color="inherit" />,
    path: '/brands',
    component: <AdminPage embedded initialSection="brands" />,
  },
  {
    name: 'Vehicle Models',
    layout: '/admin',
    icon: <Icon as={MdCategory} width="20px" height="20px" color="inherit" />,
    path: '/models',
    component: <AdminPage embedded initialSection="models" />,
  },
  {
    name: 'Services',
    layout: '/admin',
    icon: <Icon as={MdBuild} width="20px" height="20px" color="inherit" />,
    path: '/services',
    component: <ServicePage />,
  },
  {
    name: 'Service Categories',
    layout: '/admin',
    icon: <Icon as={MdLayers} width="20px" height="20px" color="inherit" />,
    path: '/service-categories',
    component: <ServiceCategoryPage />,
  },
  {
    name: 'Parts',
    layout: '/admin',
    icon: <Icon as={MdInventory} width="20px" height="20px" color="inherit" />,
    path: '/parts',
    component: <InventoryPage />,
  },
  {
    name: 'Part Categories',
    layout: '/admin',
    icon: <Icon as={MdOutlineCategory} width="20px" height="20px" color="inherit" />,
    path: '/part-categories',
    component: <InventoryCategoryPage />,
  },
];

export default routes;
