import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import AuthPage from "../features/auth/pages/AuthPage";
import HomePage from "../features/home/pages/HomePage";
import VehicleBrandsPage from "../features/vehicles/pages/VehicleBrandsPage";
import VehicleModelsPage from "../features/vehicles/pages/VehicleModelsPage";

function RequireAuth({ children }) {
  const token = localStorage.getItem("mygarage_token");
  return token ? children : <Navigate to="/" replace />;
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<AuthPage />} />
        <Route path="/home" element={<RequireAuth><HomePage /></RequireAuth>} />

        {/* Admin – Vehicle Framework */}
        <Route path="/admin/vehicle-brands" element={<RequireAuth><VehicleBrandsPage /></RequireAuth>} />
        <Route path="/admin/vehicle-models" element={<RequireAuth><VehicleModelsPage /></RequireAuth>} />

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
