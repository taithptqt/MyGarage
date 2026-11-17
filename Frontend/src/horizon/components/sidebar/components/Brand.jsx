import React from "react";

// Chakra imports
import { Flex, useColorModeValue, Text } from "@chakra-ui/react";

// Custom components
import { HSeparator } from "@horizon/components/separator/Separator";
import { useAuth } from "@shared/core/context/AuthContext";

export function SidebarBrand({ collapsed = false }) {
  // Chakra color mode
  let logoColor = useColorModeValue("navy.700", "white");
  const { user } = useAuth();
  const userRole = String(user?.roleCode || '').trim().toUpperCase();

  const brandTitle = userRole === "RECEPTIONIST" ? "LỄ TÂN WORKSPACE" : "ADMIN DASHBOARD";
  const brandShort = userRole === "RECEPTIONIST" ? "LT" : "AD";

  return (
    <Flex align='center' direction='column'>
      {collapsed ? (
        <Text
          fontSize='18px'
          fontWeight='bold'
          my='20px'
          color={logoColor}
          textAlign='center'
          letterSpacing='0.08em'>
          {brandShort}
        </Text>
      ) : (
        <Text
          fontSize='24px'
          fontWeight='bold'
          my='32px'
          color={logoColor}
          textAlign='center'>
          {brandTitle}
        </Text>
      )}
      <HSeparator mb='20px' />
    </Flex>
  );
}

export default SidebarBrand;
