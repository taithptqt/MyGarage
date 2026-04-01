import "./assets/css/App.css";
import "./assets/css/Management.css";
import { ChakraProvider } from "@chakra-ui/react";
import { useState } from "react";
import AdminLayout from "@horizon/layouts/admin";
import initialTheme from "@horizon/theme/theme";

export default function HorizonMain() {
  const [currentTheme, setCurrentTheme] = useState(initialTheme);

  return (
    <ChakraProvider theme={currentTheme}>
      <AdminLayout theme={currentTheme} setTheme={setCurrentTheme} />
    </ChakraProvider>
  );
}

