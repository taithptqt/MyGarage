// Chakra imports
import { Portal, Box, useDisclosure } from '@chakra-ui/react';
import Footer from '@horizon/components/footer/FooterAdmin';
// Layout components
import Navbar from '@horizon/components/navbar/NavbarAdmin';
import Sidebar from '@horizon/components/sidebar/Sidebar';
import { SidebarContext } from '@horizon/contexts/SidebarContext';
import React, { useEffect, useState } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import routes from '@horizon/routes.jsx';

const SIDEBAR_EXPANDED_WIDTH = 300;
const SIDEBAR_COLLAPSED_WIDTH = 5;
const SIDEBAR_COLLAPSE_STORAGE_KEY = 'mygarage.admin.sidebar.collapsed';

// Custom Chakra theme
export default function Dashboard(props) {
  const { ...rest } = props;
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
  const sidebarWidthVar = `var(--admin-sidebar-width, ${SIDEBAR_EXPANDED_WIDTH}px)`;

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
    const fullPath = normalizePath(`${route.layout || ''}${route.path || ''}`);
    const currentPath = normalizePath(location.pathname);
    return currentPath === fullPath || currentPath.startsWith(`${fullPath}/`);
  };

  // functions for changing the states from components
  const getRoute = () => {
    return location.pathname !== '/admin/full-screen-maps';
  };
  const getActiveRoute = (routes) => {
    let activeRoute = 'Admin Dashboard';
    for (let i = 0; i < routes.length; i++) {
      if (routes[i].collapse) {
        let collapseActiveRoute = getActiveRoute(routes[i].items);
        if (collapseActiveRoute !== activeRoute) {
          return collapseActiveRoute;
        }
      } else if (routes[i].category) {
        let categoryActiveRoute = getActiveRoute(routes[i].items);
        if (categoryActiveRoute !== activeRoute) {
          return categoryActiveRoute;
        }
      } else {
        if (isRouteMatch(routes[i])) {
          return routes[i].name;
        }
      }
    }
    return activeRoute;
  };
  const getActiveNavbar = (routes) => {
    let activeNavbar = false;
    for (let i = 0; i < routes.length; i++) {
      if (routes[i].collapse) {
        let collapseActiveNavbar = getActiveNavbar(routes[i].items);
        if (collapseActiveNavbar !== activeNavbar) {
          return collapseActiveNavbar;
        }
      } else if (routes[i].category) {
        let categoryActiveNavbar = getActiveNavbar(routes[i].items);
        if (categoryActiveNavbar !== activeNavbar) {
          return categoryActiveNavbar;
        }
      } else {
        if (isRouteMatch(routes[i])) {
          return routes[i].secondary;
        }
      }
    }
    return activeNavbar;
  };
  const getActiveNavbarText = (routes) => {
    let activeNavbar = false;
    for (let i = 0; i < routes.length; i++) {
      if (routes[i].collapse) {
        let collapseActiveNavbar = getActiveNavbarText(routes[i].items);
        if (collapseActiveNavbar !== activeNavbar) {
          return collapseActiveNavbar;
        }
      } else if (routes[i].category) {
        let categoryActiveNavbar = getActiveNavbarText(routes[i].items);
        if (categoryActiveNavbar !== activeNavbar) {
          return categoryActiveNavbar;
        }
      } else {
        if (isRouteMatch(routes[i])) {
          return routes[i].messageNavbar;
        }
      }
    }
    return activeNavbar;
  };
  const getRoutes = (routes) => {
    return routes.map((route, key) => {
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
          <Sidebar routes={routes} display="none" {...rest} />
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
              <Box>
                <Navbar
                  onOpen={onOpen}
                  logoText={'Horizon UI Dashboard PRO'}
                  brandText={getActiveRoute(routes)}
                  secondary={getActiveNavbar(routes)}
                  message={getActiveNavbarText(routes)}
                  fixed={fixed}
                  sidebarWidth={sidebarWidth}
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
                  {getRoutes(routes)}
                  <Route
                    path="*"
                    element={<Navigate to="default" replace />}
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

