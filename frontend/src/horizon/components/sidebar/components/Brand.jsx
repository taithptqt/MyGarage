import React from "react";

// Chakra imports
import { Flex, useColorModeValue, Text } from "@chakra-ui/react";

// Custom components
import { HSeparator } from "components/separator/Separator";

export function SidebarBrand({ collapsed = false }) {
  //   Chakra color mode
  let logoColor = useColorModeValue("navy.700", "white");

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
          AD
        </Text>
      ) : (
        <Text
          fontSize='26px'
          fontWeight='bold'
          my='32px'
          color={logoColor}
          textAlign='center'>
          ADMIN DASHBOARD
        </Text>
      )}
      <HSeparator mb='20px' />
    </Flex>
  );
}

export default SidebarBrand;
