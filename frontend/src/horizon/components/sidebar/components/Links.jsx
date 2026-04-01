/* eslint-disable */
import React from "react";
import { NavLink, useLocation } from "react-router-dom";
// chakra imports
import { Box, Flex, HStack, Text, useColorModeValue } from "@chakra-ui/react";
import { MdExpandMore, MdChevronRight } from "react-icons/md";

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

  const { routes } = props;
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
    setOpenGroups((prev) => ({ ...prev, [groupKey]: !prev[groupKey] }));
  };

  // this function creates the links from the secondary accordions (for example auth -> sign-in -> default)
  const createLinks = (routes, level = 0) => {
    return routes.map((route, index) => {
      if (route.category) {
        return (
          <>
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
              pb='12px'
              key={index}>
              {route.name}
            </Text>
            {createLinks(route.items, level + 1)}
          </>
        );
      } else if (route.collapse && route.items) {
        const groupKey = `${route.name}-${index}-${level}`;
        const groupIsActive = isGroupActive(route);
        const isOpen = openGroups[groupKey] ?? groupIsActive;

        return (
          <Box key={groupKey}>
            <Box
              onClick={() => toggleGroup(groupKey)}
              cursor='pointer'
              borderRadius='10px'
              _hover={{ bg: "gray.50" }}>
              <HStack spacing='12px' py='7px' ps={`${10 + level * 14}px`}>
                <Box color={groupIsActive ? activeIcon : textColor} me='6px'>
                  {route.icon}
                </Box>
                <Text
                  me='auto'
                  color={groupIsActive ? activeColor : textColor}
                  fontWeight={groupIsActive ? "bold" : "normal"}>
                  {route.name}
                </Text>
                <Box color={groupIsActive ? activeIcon : textColor}>
                  {isOpen ? <MdExpandMore /> : <MdChevronRight />}
                </Box>
                <Box
                  h='30px'
                  w='4px'
                  bg={groupIsActive ? brandColor : "transparent"}
                  borderRadius='5px'
                />
              </HStack>
            </Box>
            {isOpen ? (
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
          <NavLink key={index} to={route.layout + route.path}>
            {route.icon ? (
              <Box>
                <HStack
                  spacing={
                    activeRoute(route.path.toLowerCase()) ? "22px" : "26px"
                  }
                  py='5px'
                  ps={`${10 + level * 14}px`}>
                  <Flex w='100%' alignItems='center' justifyContent='center'>
                    <Box
                      color={
                        activeRoute(route.path.toLowerCase())
                          ? activeIcon
                          : textColor
                      }
                      me='18px'>
                      {route.icon}
                    </Box>
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
                  />
                </HStack>
              </Box>
            ) : (
              <Box>
                <HStack
                  spacing={
                    activeRoute(route.path.toLowerCase()) ? "22px" : "26px"
                  }
                  py='5px'
                  ps={`${10 + level * 14}px`}>
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
                  <Box h='36px' w='4px' bg='brand.400' borderRadius='5px' />
                </HStack>
              </Box>
            )}
          </NavLink>
        );
      }
    });
  };
  //  BRAND
  return createLinks(routes, 0);
}

export default SidebarLinks;
