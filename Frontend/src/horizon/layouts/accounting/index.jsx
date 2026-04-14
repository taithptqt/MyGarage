import { Portal, Box, useDisclosure } from "@chakra-ui/react";
import Footer from "components/footer/FooterAdmin";
import Navbar from "components/navbar/NavbarAdmin";
import Sidebar from "components/sidebar/Sidebar";
import { SidebarContext } from "contexts/SidebarContext";
import React, { useState } from "react";
import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import accountingRoutes from "../../accountingRoutes";

export default function AccountingLayout(props) {
  const { ...rest } = props;
  const [fixed] = useState(false);
  const [toggleSidebar, setToggleSidebar] = useState(false);
  const location = useLocation();

  const normalizePath = (value) => {
    const path = String(value || "").trim();
    if (!path) return "/";
    return path.endsWith("/") && path !== "/" ? path.slice(0, -1) : path;
  };

  const isRouteMatch = (route) => {
    const fullPath = normalizePath(`${route.layout || ""}${route.path || ""}`);
    const currentPath = normalizePath(location.pathname);
    return currentPath === fullPath || currentPath.startsWith(`${fullPath}/`);
  };

  const getRoute = () => {
    return location.pathname !== "/accounting/full-screen-maps";
  };

  const getActiveRoute = (routes) => {
    let activeRoute = "Accounting Dashboard";
    for (let i = 0; i < routes.length; i++) {
      if (routes[i].collapse) {
        const collapseActiveRoute = getActiveRoute(routes[i].items);
        if (collapseActiveRoute !== activeRoute) {
          return collapseActiveRoute;
        }
      } else if (routes[i].category) {
        const categoryActiveRoute = getActiveRoute(routes[i].items);
        if (categoryActiveRoute !== activeRoute) {
          return categoryActiveRoute;
        }
      } else if (isRouteMatch(routes[i])) {
        return routes[i].name;
      }
    }
    return activeRoute;
  };

  const getActiveNavbar = (routes) => {
    let activeNavbar = false;
    for (let i = 0; i < routes.length; i++) {
      if (routes[i].collapse) {
        const collapseActiveNavbar = getActiveNavbar(routes[i].items);
        if (collapseActiveNavbar !== activeNavbar) {
          return collapseActiveNavbar;
        }
      } else if (routes[i].category) {
        const categoryActiveNavbar = getActiveNavbar(routes[i].items);
        if (categoryActiveNavbar !== activeNavbar) {
          return categoryActiveNavbar;
        }
      } else if (isRouteMatch(routes[i])) {
        return routes[i].secondary;
      }
    }
    return activeNavbar;
  };

  const getActiveNavbarText = (routes) => {
    let activeNavbar = false;
    for (let i = 0; i < routes.length; i++) {
      if (routes[i].collapse) {
        const collapseActiveNavbar = getActiveNavbarText(routes[i].items);
        if (collapseActiveNavbar !== activeNavbar) {
          return collapseActiveNavbar;
        }
      } else if (routes[i].category) {
        const categoryActiveNavbar = getActiveNavbarText(routes[i].items);
        if (categoryActiveNavbar !== activeNavbar) {
          return categoryActiveNavbar;
        }
      } else if (isRouteMatch(routes[i])) {
        return routes[i].messageNavbar;
      }
    }
    return activeNavbar;
  };

  const getRoutes = (routes) => {
    return routes.map((route, key) => {
      if (route.layout === "/accounting") {
        const relativePath = route.path.startsWith("/") ? route.path.slice(1) : route.path;
        return <Route path={relativePath} element={route.component} key={key} />;
      }
      if (route.collapse) {
        return getRoutes(route.items);
      }
      return null;
    });
  };

  document.documentElement.dir = "ltr";
  const { onOpen } = useDisclosure();

  return (
    <Box>
      <Box>
        <SidebarContext.Provider value={{ toggleSidebar, setToggleSidebar }}>
          <Sidebar routes={accountingRoutes} display='none' {...rest} />
          <Box
            float='right'
            minHeight='100vh'
            height='100%'
            overflow='auto'
            position='relative'
            maxHeight='100%'
            w={{ base: "100%", xl: "calc( 100% - 290px )" }}
            maxWidth={{ base: "100%", xl: "calc( 100% - 290px )" }}
            transition='all 0.33s cubic-bezier(0.685, 0.0473, 0.346, 1)'
            transitionDuration='.2s, .2s, .35s'
            transitionProperty='top, bottom, width'
            transitionTimingFunction='linear, linear, ease'>
            <Portal>
              <Box>
                <Navbar
                  onOpen={onOpen}
                  logoText='MyGarage Accounting'
                  brandText={getActiveRoute(accountingRoutes)}
                  secondary={getActiveNavbar(accountingRoutes)}
                  message={getActiveNavbarText(accountingRoutes)}
                  fixed={fixed}
                  routes={accountingRoutes}
                  {...rest}
                />
              </Box>
            </Portal>

            {getRoute() ? (
              <Box
                mx='auto'
                p={{ base: "20px", md: "30px" }}
                pe='20px'
                minH='100vh'
                pt={{ base: "130px", md: "110px", xl: "110px" }}>
                <Routes>
                  {getRoutes(accountingRoutes)}
                  <Route path='*' element={<Navigate to='dashboard' replace />} />
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
