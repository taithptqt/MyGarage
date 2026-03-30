// chakra imports
import { Box, Flex, Stack, Text, Icon } from "@chakra-ui/react";
//   Custom components
import Brand from "components/sidebar/components/Brand";
import Links from "components/sidebar/components/Links";
import React from "react";
import { MdOutlineLogout } from "react-icons/md";
import { useNavigate } from "react-router-dom";
import { clearAuth } from "../../../../app/authState";

// FUNCTIONS

function SidebarContent(props) {
  const { routes, collapsed = false } = props;
  const navigate = useNavigate();

  const handleLogout = () => {
    clearAuth();
    navigate("/auth/sign-in");
  };

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
      <Box ps={collapsed ? '6px' : '20px'} pe={{ md: "16px", "2xl": "1px" }} mt='auto' pb='20px'>
        <Flex
          align='center'
          justify={collapsed ? 'center' : 'flex-start'}
          p='12px'
          borderRadius='10px'
          cursor='pointer'
          onClick={handleLogout}
          _hover={{ bg: "gray.100" }}
          transition='0.2s linear'
        >
          <Icon as={MdOutlineLogout} width='20px' height='20px' color='inherit' me={collapsed ? '0px' : '12px'} />
          {!collapsed ? (
            <Text color='inherit' fontWeight='500'>
              Log out
            </Text>
          ) : null}
        </Flex>
      </Box>
    </Flex>
  );
}

export default SidebarContent;
