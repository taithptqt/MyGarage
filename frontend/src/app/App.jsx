import { Navigate, Route, Routes } from "react-router-dom";
import { isAuthenticated } from "./authState";
import AuthPage from "../features/auth/pages/AuthPage";
import HomePage from "../features/home/pages/HomePage";
import AdminPage from "../features/admin/pages/AdminPage";
import HorizonMain from "../horizon/Main";
import CustomerLayout from "../shared/layouts/CustomerLayout";
import BookingPage from "../features/appointments/pages/BookingPage";
import MyAppointmentsPage from "../features/appointments/pages/MyAppointmentsPage";
import PayosCheckoutPage from "../features/payment/pages/PayosCheckoutPage";
import PaymentSuccessPage from "../features/payment/pages/PaymentSuccessPage";
import PaymentCancelPage from "../features/payment/pages/PaymentCancelPage";
import PartsSalesPage from "../features/retail/pages/PartsSalesPage";
import MyRetailOrdersPage from "../features/retail/pages/MyRetailOrdersPage";

function parseSavedUser() {
  const userStr = localStorage.getItem("mygarage_user");
  if (!userStr) return null;
  try {
    return JSON.parse(userStr);
  } catch {
    return null;
  }
}

function resolveStaffHome(user) {
  return "/admin/default";
}

function CustomerRoute({ children }) {
  if (!isAuthenticated()) {
    return <Navigate to="/login" replace />;
  }

  const user = parseSavedUser();
  if (user?.role === "Staff") {
    return <Navigate to={resolveStaffHome(user)} replace />;
  }
  return <CustomerLayout>{children}</CustomerLayout>;
}

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="/login" element={<AuthPage />} />
      <Route path="/home" element={<HomePage />} />
      <Route path="/booking" element={<CustomerRoute><BookingPage /></CustomerRoute>} />
      <Route path="/parts-shop" element={<CustomerRoute><PartsSalesPage /></CustomerRoute>} />
      <Route path="/my-retail-orders" element={<CustomerRoute><MyRetailOrdersPage /></CustomerRoute>} />
      <Route path="/my-appointments" element={<CustomerRoute><MyAppointmentsPage /></CustomerRoute>} />
      <Route path="/payment" element={<CustomerRoute><PayosCheckoutPage /></CustomerRoute>} />
      <Route path="/payment/success" element={<CustomerRoute><PaymentSuccessPage /></CustomerRoute>} />
      <Route path="/payment/cancel" element={<CustomerRoute><PaymentCancelPage /></CustomerRoute>} />
      <Route path="/admin/*" element={<HorizonMain />} />
      <Route path="/admin-legacy" element={<AdminPage />} />
      <Route path="/horizon/*" element={<Navigate to="/admin/default" replace />} />
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}

export default App;
