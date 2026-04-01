/* eslint-disable */
import React from "react";
import { NavLink, useLocation } from "react-router-dom";
// chakra imports
import {
  Box,
  Flex,
  HStack,
  Text,
  Tooltip,
  useColorModeValue,
} from "@chakra-ui/react";
import { MdExpandMore, MdChevronRight } from "react-icons/md";
import { SidebarContext } from "@horizon/contexts/SidebarContext";

export function SidebarLinks(props) {
  //   Chakra color mode
  let location = useLocation();
  let activeColor = useColorModeValue("gray.700", "white");
  let inactiveColor = useColorModeValue(
    "secondaryGray.700",
    "secondaryGray.500"
  );
  let activeIcon = useColorModeValue("brand.500", "white");
  let textColor = useColorModeValue("secondaryGray.700", "white");
  let brandColor = useColorModeValue("brand.500", "brand.400");

  const { routes, collapsed = false } = props;
  const sidebarContext = React.useContext(SidebarContext) || {};
  const setIsSidebarCollapsed = sidebarContext.setIsSidebarCollapsed;
  const [openGroups, setOpenGroups] = React.useState({});

  // verifies if routeName is the one active (in browser input)
  const activeRoute = (routeName) => {
    return location.pathname.includes(routeName);
  };
  const isGroupActive = (route) => {
    if (!route?.items?.length) return false;
    return route.items.some((item) => {
      if (item.collapse) return isGroupActive(item);
      if (!item.path) return false;
      return activeRoute(item.path.toLowerCase());
    });
  };

  const toggleGroup = (groupKey) => {
    if (collapsed && setIsSidebarCollapsed) {
      setOpenGroups((prev) => ({ ...prev, [groupKey]: true }));
      setIsSidebarCollapsed(false);
      return;
    }
    setOpenGroups((prev) => ({ ...prev, [groupKey]: !prev[groupKey] }));
  };

  // this function creates the links from the secondary accordions (for example auth -> sign-in -> default)
  const createLinks = (routes, level = 0) => {
    return routes.map((route, index) => {
      if (route.category) {
        return (
          <Box key={`${route.name}-${index}-${level}`}>
            {!collapsed ? (
              <Text
                fontSize={"md"}
                color={activeColor}
                fontWeight='bold'
                mx='auto'
                ps={{
                  sm: "10px",
                  xl: "16px",
                }}
                pt='18px'
                pb='12px'>
                {route.name}
              </Text>
            ) : null}
            {createLinks(route.items, level + 1)}
          </Box>
        );
      } else if (route.collapse && route.items) {
        const groupKey = `${route.name}-${index}-${level}`;
        const groupIsActive = isGroupActive(route);
        const isOpen = openGroups[groupKey] ?? groupIsActive;

        return (
          <Box key={groupKey}>
            <Tooltip label={route.name} isDisabled={!collapsed} placement='right'>
              <Box
                onClick={() => toggleGroup(groupKey)}
                cursor='pointer'
                borderRadius='10px'
                _hover={{ bg: "gray.50" }}>
                <HStack
                  spacing={collapsed ? '0px' : '12px'}
                  py='7px'
                  ps={collapsed ? '0px' : `${10 + level * 14}px`}
                  justifyContent={collapsed ? 'center' : 'flex-start'}>
                  <Box color={groupIsActive ? activeIcon : textColor} me={collapsed ? '0px' : '6px'}>
                    {route.icon}
                  </Box>
                  {!collapsed ? (
                    <Text
                      me='auto'
                      color={groupIsActive ? activeColor : textColor}
                      fontWeight={groupIsActive ? "bold" : "normal"}>
                      {route.name}
                    </Text>
                  ) : null}
                  {!collapsed ? (
                    <Box color={groupIsActive ? activeIcon : textColor}>
                      {isOpen ? <MdExpandMore /> : <MdChevronRight />}
                    </Box>
                  ) : null}
                  <Box
                    h='30px'
                    w='4px'
                    bg={groupIsActive ? brandColor : "transparent"}
                    borderRadius='5px'
                    ms={collapsed ? '8px' : '0px'}
                  />
                </HStack>
              </Box>
            </Tooltip>
            {isOpen && !collapsed ? (
              <Box ps='8px'>
                {createLinks(route.items, level + 1)}
              </Box>
            ) : null}
          </Box>
        );
      } else if (
        route.layout === "/admin" ||
        route.layout === "/auth" ||
        route.layout === "/rtl" ||
        route.layout === "/horizon/admin" ||
        route.layout === "/horizon/auth" ||
        route.layout === "/horizon/rtl"
      ) {
        return (
          <Tooltip
            key={index}
            label={route.name}
            isDisabled={!collapsed}
            placement='right'>
            <NavLink to={route.layout + route.path}>
              {route.icon ? (
                <Box>
                  <HStack
                    spacing={collapsed ? "0px" : activeRoute(route.path.toLowerCase()) ? "22px" : "26px"}
                    py='5px'
                    ps={collapsed ? '0px' : `${10 + level * 14}px`}
                    justifyContent={collapsed ? 'center' : 'flex-start'}>
                    <Flex
                      w='100%'
                      alignItems='center'
                      justifyContent={collapsed ? 'center' : 'flex-start'}>
                      <Box
                        color={
                          activeRoute(route.path.toLowerCase())
                            ? activeIcon
                            : textColor
                        }
                        me={collapsed ? '0px' : '18px'}>
                        {route.icon}
                      </Box>
                      {!collapsed ? (
                        <Text
                          me='auto'
                          color={
                            activeRoute(route.path.toLowerCase())
                              ? activeColor
                              : textColor
                          }
                          fontWeight={
                            activeRoute(route.path.toLowerCase())
                              ? "bold"
                              : "normal"
                          }>
                          {route.name}
                        </Text>
                      ) : null}
                    </Flex>
                    <Box
                      h='36px'
                      w='4px'
                      bg={
                        activeRoute(route.path.toLowerCase())
                          ? brandColor
                          : "transparent"
                      }
                      borderRadius='5px'
                      ms={collapsed ? '8px' : '0px'}
                    />
                  </HStack>
                </Box>
              ) : (
                <Box>
                  <HStack
                    spacing={collapsed ? "0px" : activeRoute(route.path.toLowerCase()) ? "22px" : "26px"}
                    py='5px'
                    ps={collapsed ? '0px' : `${10 + level * 14}px`}>
                    {!collapsed ? (
                      <Text
                        me='auto'
                        color={
                          activeRoute(route.path.toLowerCase())
                            ? activeColor
                            : inactiveColor
                        }
                        fontWeight={
                          activeRoute(route.path.toLowerCase()) ? "bold" : "normal"
                        }>
                        {route.name}
                      </Text>
                    ) : null}
                    <Box
                      h='36px'
                      w='4px'
                      bg={activeRoute(route.path.toLowerCase()) ? brandColor : "transparent"}
                      borderRadius='5px'
                    />
                  </HStack>
                </Box>
              )}
            </NavLink>
          </Tooltip>
        );
      }
      return null;
    });
  };
  //  BRAND
  return createLinks(routes, 0);
}

export default SidebarLinks;

