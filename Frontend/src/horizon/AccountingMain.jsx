import "./assets/css/App.css";
import "./assets/css/Management.css";
import { ChakraProvider } from "@chakra-ui/react";
import { useState } from "react";
import AccountingLayout from "./layouts/accounting";
import initialTheme from "./theme/theme";

export default function AccountingMain() {
  const [currentTheme, setCurrentTheme] = useState(initialTheme);

  return (
    <ChakraProvider theme={currentTheme}>
      <AccountingLayout theme={currentTheme} setTheme={setCurrentTheme} />
    </ChakraProvider>
  );
}
