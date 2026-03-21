import { Navigate, Route, Routes } from "react-router-dom";
import AuthPage from "../features/auth/pages/AuthPage";
import HomePage from "../features/home/pages/HomePage";
import AdminPage from "../features/admin/pages/AdminPage";
import HorizonMain from "../horizon/Main";
import CustomerLayout from "../shared/layouts/CustomerLayout";
import BookingPage from "../features/appointments/pages/BookingPage";
import MyAppointmentsPage from "../features/appointments/pages/MyAppointmentsPage";

function CustomerRoute({ children }) {
  return <CustomerLayout>{children}</CustomerLayout>;
}

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="/login" element={<AuthPage />} />
      <Route path="/home" element={<HomePage />} />
      <Route path="/booking" element={<CustomerRoute><BookingPage /></CustomerRoute>} />
      <Route path="/my-appointments" element={<CustomerRoute><MyAppointmentsPage /></CustomerRoute>} />
      <Route path="/admin/*" element={<HorizonMain />} />
      <Route path="/admin-legacy" element={<AdminPage />} />
      <Route path="/horizon/*" element={<Navigate to="/admin/default" replace />} />
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}

export default App;
