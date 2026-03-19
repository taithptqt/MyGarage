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
  const { routes } = props;
  const navigate = useNavigate();

  const handleLogout = () => {
    clearAuth();
    navigate("/auth/sign-in");
  };

  // SIDEBAR
  return (
    <Flex direction='column' height='100%' pt='25px' px="16px" borderRadius='30px'>
      <Brand />
      <Stack direction='column' mb='auto' mt='8px'>
        <Box ps='20px' pe={{ md: "16px", "2xl": "1px" }}>
          <Links routes={routes} />
        </Box>
      </Stack>

      <Box ps='20px' pe={{ md: "16px", "2xl": "1px" }} mt='auto' pb='20px'>
        <Flex
          align='center'
          p='12px'
          borderRadius='10px'
          cursor='pointer'
          onClick={handleLogout}
          _hover={{ bg: "gray.100" }}
          transition='0.2s linear'
        >
          <Icon as={MdOutlineLogout} width='20px' height='20px' color='inherit' me='12px' />
          <Text color='inherit' fontWeight='500'>
            Log out
          </Text>
        </Flex>
      </Box>
    </Flex>
  );
}

export default SidebarContent;
