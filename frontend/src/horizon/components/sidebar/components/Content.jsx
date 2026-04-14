// chakra imports
import { Box, Flex, Stack } from "@chakra-ui/react";
//   Custom components
import Brand from "@horizon/components/sidebar/components/Brand";
import Links from "@horizon/components/sidebar/components/Links";
import React from "react";

// FUNCTIONS

function SidebarContent(props) {
  const { routes, collapsed = false } = props;

  // SIDEBAR
  return (
    <Flex
      direction='column'
      height='100%'
      pt='25px'
      px={collapsed ? "10px" : "16px"}
      borderRadius='0'>
      <Brand collapsed={collapsed} />
      <Stack direction='column' mb='auto' mt='8px'>
        <Box ps={collapsed ? '6px' : '20px'} pe={{ md: "16px", "2xl": "1px" }}>
          <Links routes={routes} collapsed={collapsed} />
        </Box>
      </Stack>
    </Flex>
  );
}

export default SidebarContent;

