// Chakra imports
import { Portal, Box, useDisclosure } from '@chakra-ui/react';
import Footer from '@horizon/components/footer/FooterAdmin';
// Layout components
import Navbar from '@horizon/components/navbar/NavbarAdmin';
import Sidebar from '@horizon/components/sidebar/Sidebar';
import { SidebarContext } from '@horizon/contexts/SidebarContext';
import React, { useEffect, useMemo, useState } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import routes from '@horizon/routes.jsx';
import { useAuth } from '@shared/core/context/AuthContext';

const SIDEBAR_EXPANDED_WIDTH = 300;
const SIDEBAR_COLLAPSED_WIDTH = 5;
const SIDEBAR_COLLAPSE_STORAGE_KEY = 'mygarage.admin.sidebar.collapsed';

// Custom Chakra theme
export default function Dashboard(props) {
  const { ...rest } = props;
  const { user } = useAuth();
  const userRole = String(user?.roleCode || '').trim().toUpperCase();

  // states and functions
  const [fixed] = useState(false);
  const [toggleSidebar, setToggleSidebar] = useState(false);
  const [isSidebarDragging, setIsSidebarDragging] = useState(false);
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(() => {
    if (typeof window === 'undefined') return false;
    return window.localStorage.getItem(SIDEBAR_COLLAPSE_STORAGE_KEY) === '1';
  });
  const [sidebarWidth, setSidebarWidth] = useState(() =>
    typeof window !== 'undefined' &&
    window.localStorage.getItem(SIDEBAR_COLLAPSE_STORAGE_KEY) === '1'
      ? SIDEBAR_COLLAPSED_WIDTH
      : SIDEBAR_EXPANDED_WIDTH
  );
  const location = useLocation();
  const sidebarWidthVar = `var(--admin-sidebar-width, ${sidebarWidth}px)`;

  const filteredRoutes = useMemo(() => {
    return routes.filter((route) => {
      if (route.roles && route.roles.length > 0) {
        return route.roles.includes(userRole);
      }
      return userRole === 'ADMIN';
    });
  }, [userRole]);

  const sidebarRoutes = useMemo(() => {
    return filteredRoutes.filter((route) => !route.hideInSidebar);
  }, [filteredRoutes]);

  const defaultPath = userRole === 'RECEPTIONIST' ? 'service-orders' : 'default';

  useEffect(() => {
    if (typeof document === 'undefined') return;
    document.documentElement.style.setProperty('--admin-sidebar-width', `${sidebarWidth}px`);
  }, [sidebarWidth]);

  const normalizePath = (value) => {
    const path = String(value || '').trim();
    if (!path) return '/';
    return path.endsWith('/') && path !== '/' ? path.slice(0, -1) : path;
  };

  const isRouteMatch = (route) => {
    if (!route.path) return false;
    const fullPath = normalizePath(`${route.layout || ''}${route.path || ''}`);
    const currentPath = normalizePath(location.pathname);
    if (fullPath.includes(':')) {
      const basePrefix = fullPath.split('/:')[0];
      return currentPath.startsWith(basePrefix);
    }
    return currentPath === fullPath || currentPath.startsWith(`${fullPath}/`);
  };

  // functions for changing the states from components
  const getRoute = () => {
    return location.pathname !== '/admin/full-screen-maps';
  };
  const getActiveRoute = (routeList) => {
    let activeRoute = userRole === 'RECEPTIONIST' ? 'Lệnh sửa chữa' : 'Admin Dashboard';
    for (let i = 0; i < routeList.length; i++) {
      if (routeList[i].collapse) {
        let collapseActiveRoute = getActiveRoute(routeList[i].items);
        if (collapseActiveRoute !== activeRoute) {
          return collapseActiveRoute;
        }
      } else if (routeList[i].category) {
        let categoryActiveRoute = getActiveRoute(routeList[i].items);
        if (categoryActiveRoute !== activeRoute) {
          return categoryActiveRoute;
        }
      } else {
        if (isRouteMatch(routeList[i])) {
          return routeList[i].name;
        }
      }
    }
    return activeRoute;
  };
  const getActiveNavbar = (routeList) => {
    let activeNavbar = false;
    for (let i = 0; i < routeList.length; i++) {
      if (routeList[i].collapse) {
        let collapseActiveNavbar = getActiveNavbar(routeList[i].items);
        if (collapseActiveNavbar !== activeNavbar) {
          return collapseActiveNavbar;
        }
      } else if (routeList[i].category) {
        let categoryActiveNavbar = getActiveNavbar(routeList[i].items);
        if (categoryActiveNavbar !== activeNavbar) {
          return categoryActiveNavbar;
        }
      } else {
        if (isRouteMatch(routeList[i])) {
          return routeList[i].secondary;
        }
      }
    }
    return activeNavbar;
  };
  const getActiveNavbarText = (routeList) => {
    let activeNavbar = false;
    for (let i = 0; i < routeList.length; i++) {
      if (routeList[i].collapse) {
        let collapseActiveNavbar = getActiveNavbarText(routeList[i].items);
        if (collapseActiveNavbar !== activeNavbar) {
          return collapseActiveNavbar;
        }
      } else if (routeList[i].category) {
        let categoryActiveNavbar = getActiveNavbarText(routeList[i].items);
        if (categoryActiveNavbar !== activeNavbar) {
          return categoryActiveNavbar;
        }
      } else {
        if (isRouteMatch(routeList[i])) {
          return routeList[i].messageNavbar;
        }
      }
    }
    return activeNavbar;
  };
  const getRoutes = (routeList) => {
    return routeList.map((route, key) => {
      if (route.layout === '/admin') {
        const relativePath = route.path.startsWith("/")
          ? route.path.slice(1)
          : route.path;
        return (
          <Route path={relativePath} element={route.component} key={key} />
        );
      }
      if (route.collapse) {
        return getRoutes(route.items);
      } else {
        return null;
      }
    });
  };
  document.documentElement.dir = 'ltr';
  const { onOpen } = useDisclosure();
  document.documentElement.dir = 'ltr';
  const handleSidebarCollapseChange = (nextCollapsed) => {
    setIsSidebarCollapsed(nextCollapsed);
    setSidebarWidth(nextCollapsed ? SIDEBAR_COLLAPSED_WIDTH : SIDEBAR_EXPANDED_WIDTH);
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(
        SIDEBAR_COLLAPSE_STORAGE_KEY,
        nextCollapsed ? '1' : '0',
      );
    }
  };

  return (
    <Box>
      <Box>
        <SidebarContext.Provider
          value={{
            toggleSidebar,
            setToggleSidebar,
            isSidebarCollapsed,
            setIsSidebarCollapsed: handleSidebarCollapseChange,
            isSidebarDragging,
            setIsSidebarDragging,
            sidebarWidth,
            setSidebarWidth,
            sidebarMinWidth: SIDEBAR_COLLAPSED_WIDTH,
            sidebarMaxWidth: SIDEBAR_EXPANDED_WIDTH,
          }}
        >
          <Sidebar routes={sidebarRoutes} display="none" {...rest} />
          <Box
            float="right"
            minHeight="100vh"
            height="100%"
            overflow="auto"
            position="relative"
            maxHeight="100%"
            w={{ base: '100%', xl: `calc(100% - ${sidebarWidthVar})` }}
            maxWidth={{ base: '100%', xl: `calc(100% - ${sidebarWidthVar})` }}
            transition={isSidebarDragging ? "none" : "width 0.14s ease-out"}
          >
            <Portal>
              <Box position="relative" zIndex={1200}>
                <Navbar
                  onOpen={onOpen}
                  logoText={'Horizon UI Dashboard PRO'}
                  brandText={getActiveRoute(filteredRoutes)}
                  secondary={getActiveNavbar(filteredRoutes)}
                  message={getActiveNavbarText(filteredRoutes)}
                  fixed={fixed}
                  sidebarWidth={sidebarWidth}
                  routes={sidebarRoutes}
                  {...rest}
                />
              </Box>
            </Portal>

            {getRoute() ? (
              <Box
                mx="auto"
                p={{ base: '20px', md: '30px' }}
                pe="20px"
                minH="100vh"
                pt={{ base: '130px', md: '110px', xl: '110px' }}
              >
                <Routes>
                  <Route index element={<Navigate to={defaultPath} replace />} />
                  {getRoutes(filteredRoutes)}
                  <Route
                    path="*"
                    element={<Navigate to={defaultPath} replace />}
                  />
                </Routes>
              </Box>
            ) : null}
            <Box>
              <Footer />
            </Box>
          </Box>
        </SidebarContext.Provider>
      </Box>
    </Box>
  );
}
