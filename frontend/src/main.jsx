import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./app/App";
import { AuthProvider } from "./context/AuthContext";
import { BranchProvider } from "./context/BranchContext";
import { SearchProvider } from "./context/SearchContext";
import "./styles/global.css";

ReactDOM.createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <BranchProvider>
          <SearchProvider>
            <App />
          </SearchProvider>
        </BranchProvider>
      </AuthProvider>
    </BrowserRouter>
  </React.StrictMode>
);
