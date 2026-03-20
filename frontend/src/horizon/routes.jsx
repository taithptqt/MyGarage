import React from 'react';

import { Icon } from '@chakra-ui/react';
import {
  MdBuild,
  MdCategory,
  MdDirectionsCar,
  MdHome,
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
import PartsPage from "../features/inventory/pages/PartsPage";
import PartCategoriesPage from "../features/inventory/pages/PartCategoriesPage";
import ServicesPage from "../features/services/pages/ServicesPage";
import ServiceCategoriesPage from "../features/services/pages/ServiceCategoriesPage";

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
    name: 'Branches',
    layout: '/admin',
    path: '/branches',
    icon: <Icon as={MdSettings} width="20px" height="20px" color="inherit" />,
    component: <AdminPage embedded initialSection="branches" />,
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
    component: <ServicesPage />,
  },
  {
    name: 'Service Categories',
    layout: '/admin',
    icon: <Icon as={MdLayers} width="20px" height="20px" color="inherit" />,
    path: '/service-categories',
    component: <ServiceCategoriesPage />,
  },
  {
    name: 'Parts',
    layout: '/admin',
    icon: <Icon as={MdInventory} width="20px" height="20px" color="inherit" />,
    path: '/parts',
    component: <PartsPage />,
  },
  {
    name: 'Part Categories',
    layout: '/admin',
    icon: <Icon as={MdOutlineCategory} width="20px" height="20px" color="inherit" />,
    path: '/part-categories',
    component: <PartCategoriesPage />,
  },
];

export default routes;
