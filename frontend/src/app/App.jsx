import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import AuthPage from "../features/auth/pages/AuthPage";
import HomePage from "../features/home/pages/HomePage";

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<AuthPage />} />
        <Route path="/home" element={<HomePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
