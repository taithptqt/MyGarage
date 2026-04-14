import { Navigate, Route, Routes } from "react-router-dom";
import AuthPage from "@features/auth/pages/AuthPage";
import HomePage from "@features/home/pages/HomePage";
import AdminPage from "@features/admin/pages/AdminPage";
import DispatchCenterPage from "@features/admin/pages/DispatchCenterPage";
import WorkshopPage from "@features/workshop/pages/WorkshopPage";
import ReceptionOrdersSection from "@features/workshop/components/ReceptionOrdersSection";
import ManagerDashboard from "@features/workshop/components/ManagerDashboard";
import ForemanDashboard from "@features/workshop/components/ForemanDashboard";
import TechnicianDashboard from "@features/workshop/components/TechnicianDashboard";
import QCDashboard from "@features/workshop/components/QCDashboard";
import CreateServiceOrderPage from "@features/workshop/pages/CreateServiceOrderPage";
import ServiceOrderDiagnosisPage from "@features/workshop/pages/ServiceOrderDiagnosisPage";
import ServiceOrderItemsPage from "@features/workshop/pages/ServiceOrderItemsPage";
import ServiceOrderQuotePage from "@features/workshop/pages/ServiceOrderQuotePage";
import VehicleConditionLogsPage from "@features/workshop/pages/VehicleConditionLogsPage";
import TechnicianConditionLogPage from "@features/workshop/pages/TechnicianConditionLogPage";
import ServiceOrderEditPage from "@features/workshop/pages/ServiceOrderEditPage";
import ReceptionCreateAppointmentPage from "@features/workshop/pages/ReceptionCreateAppointmentPage";
import ReceptionAppointmentsPage from "@features/workshop/pages/ReceptionAppointmentsPage";
import HorizonMain from "@horizon/Main";
import ProtectedRoute from "@shared/core/components/auth/ProtectedRoute";
import { useAuth } from "@shared/core/context/AuthContext";
import { BranchProvider } from "@shared/core/context/BranchContext";
import useIdleTimeout from "@shared/core/hooks/useIdleTimeout";
import CustomerLayout from "@shared/layouts/CustomerLayout";
import BookingPage from "@features/appointments/pages/BookingPage";
import BookingIntroPage from "@features/appointments/pages/BookingIntroPage";
import MyAppointmentsPage from "@features/appointments/pages/MyAppointmentsPage";
import PayosCheckoutPage from "@features/payment/pages/PayosCheckoutPage";
import PaymentSuccessPage from "@features/payment/pages/PaymentSuccessPage";
import PaymentCancelPage from "@features/payment/pages/PaymentCancelPage";
import PartsSalesPage from "@features/retail/pages/PartsSalesPage";
import MyRetailOrdersPage from "@features/retail/pages/MyRetailOrdersPage";
import AccountingMain from "@horizon/AccountingMain";

function CustomerRoute({ children }) {
  return <CustomerLayout>{children}</CustomerLayout>;
}

function App() {
  const { logout, isAuthenticated } = useAuth();

  useIdleTimeout({
    timeoutMinutes: 60,
    enabled: isAuthenticated,
    onIdle: () => {
      if (isAuthenticated) {
        console.log("Session expired due to inactivity.");
        logout();
      }
    }
  });

  return (
    <BranchProvider>
      <Routes>
        <Route path="/" element={<Navigate to="/home" replace />} />
        <Route path="/login" element={<AuthPage />} />
        <Route path="/home" element={<HomePage />} />
        
        {/* Customer Appointment Routes */}
        <Route 
          path="/booking" 
          element={
            <CustomerRoute><BookingIntroPage /></CustomerRoute>
          } 
        />
        <Route 
          path="/booking/form" 
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><BookingPage /></CustomerRoute>
            </ProtectedRoute>
          } 
        />
        <Route 
          path="/my-appointments" 
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><MyAppointmentsPage /></CustomerRoute>
            </ProtectedRoute>
          } 
        />

        <Route path="/parts-shop" element={<CustomerRoute><PartsSalesPage /></CustomerRoute>} />
        <Route
          path="/my-retail-orders"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><MyRetailOrdersPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />
        <Route
          path="/payment"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><PayosCheckoutPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />
        <Route
          path="/payment/success"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><PaymentSuccessPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />
        <Route
          path="/payment/cancel"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><PaymentCancelPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />

        <Route 
          path="/admin/*" 
          element={
            <ProtectedRoute allowedRoles={["ADMIN"]}>
              <HorizonMain />
            </ProtectedRoute>
          } 
        >
          {/* Admin Appointment Management can be nested here if using Horizon sidebar, 
              or kept as a separate route inside Horizon layout. 
              For now, let's assume it's part of Horizon navigation. */}
        </Route>

        <Route
          path="/accounting/*"
          element={
            <ProtectedRoute allowedRoles={["ADMIN", "MANAGER", "ACCOUNTANT"]}>
              <AccountingMain />
            </ProtectedRoute>
          }
        />

        <Route 
          path="/admin-legacy" 
          element={
            <ProtectedRoute allowedRoles={["ADMIN"]}>
              <AdminPage />
            </ProtectedRoute>
          } 
        />

        <Route
          path="/dispatch-center"
          element={
            <ProtectedRoute allowedRoles={["ADMIN", "MANAGER", "RECEPTIONIST", "FOREMAN"]}>
              <DispatchCenterPage />
            </ProtectedRoute>
          }
        />
        
        <Route 
          path="/workshop" 
          element={
            <ProtectedRoute allowedRoles={["RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN"]}>
              <WorkshopPage />
            </ProtectedRoute>
          } 
        >
          <Route path="reception" element={<ProtectedRoute allowedRoles={["RECEPTIONIST"]}><ReceptionOrdersSection /></ProtectedRoute>} />
          <Route path="reception/create" element={<ProtectedRoute allowedRoles={["RECEPTIONIST"]}><CreateServiceOrderPage /></ProtectedRoute>} />
          <Route path="reception/appointments" element={<ProtectedRoute allowedRoles={["RECEPTIONIST"]}><ReceptionAppointmentsPage /></ProtectedRoute>} />
          <Route path="reception/appointments/new" element={<ProtectedRoute allowedRoles={["RECEPTIONIST"]}><ReceptionCreateAppointmentPage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/diagnosis" element={<ProtectedRoute allowedRoles={["RECEPTIONIST"]}><ServiceOrderDiagnosisPage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/items" element={<ProtectedRoute allowedRoles={["RECEPTIONIST"]}><ServiceOrderItemsPage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/quote" element={<ProtectedRoute allowedRoles={["RECEPTIONIST"]}><ServiceOrderQuotePage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/vehicleconditionlogs" element={<ProtectedRoute allowedRoles={["RECEPTIONIST"]}><VehicleConditionLogsPage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/edit" element={<ProtectedRoute allowedRoles={["RECEPTIONIST"]}><ServiceOrderEditPage /></ProtectedRoute>} />
          <Route path="manager" element={<ProtectedRoute allowedRoles={["MANAGER"]}><ManagerDashboard /></ProtectedRoute>} />
          <Route path="foreman" element={<ProtectedRoute allowedRoles={["FOREMAN"]}><ForemanDashboard /></ProtectedRoute>} />
          <Route path="technician" element={<ProtectedRoute allowedRoles={["TECH"]}><TechnicianDashboard /></ProtectedRoute>} />
          <Route path="technician/orders/:orderId/condition" element={<ProtectedRoute allowedRoles={["TECH"]}><TechnicianConditionLogPage /></ProtectedRoute>} />
          <Route path="technician/orders/:orderId/diagnosis" element={<ProtectedRoute allowedRoles={["TECH"]}><ServiceOrderDiagnosisPage /></ProtectedRoute>} />
          <Route path="qc" element={<ProtectedRoute allowedRoles={["QUALITY_CONTROL"]}><QCDashboard /></ProtectedRoute>} />
          
        </Route>

        <Route path="/horizon/*" element={<Navigate to="/admin/default" replace />} />
        <Route path="*" element={<Navigate to="/home" replace />} />
      </Routes>
    </BranchProvider>
  );
}

export default App;




