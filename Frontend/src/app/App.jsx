import { Navigate, Route, Routes } from "react-router-dom";
import AuthPage from "@features/auth/pages/AuthPage";
import ForgotPasswordPage from "@features/auth/recovery/ForgotPasswordPage";
import ResetPasswordPage from "@features/auth/recovery/ResetPasswordPage";
import HomePage from "@features/home/pages/HomePage";
import AdminPage from "@features/admin/pages/AdminPage";
import DispatchCenterPage from "@features/admin/pages/DispatchCenterPage";
import WorkshopPage from "@features/workshop/pages/WorkshopPage";
import ReceptionOrdersSection from "@features/workshop/components/ReceptionOrdersSection";
import ManagerDashboard from "@features/workshop/components/ManagerDashboard";
import ForemanDashboard from "@features/workshop/components/ForemanDashboard";
import TeamLeaderDashboard from "@features/workshop/components/TeamLeaderDashboard";
import TechnicianDashboard from "@features/workshop/components/TechnicianDashboard";
import QCDashboard from "@features/workshop/components/QCDashboard";
import CreateServiceOrderPage from "@features/workshop/pages/CreateServiceOrderPage";
import ServiceOrderDiagnosisPage from "@features/workshop/pages/ServiceOrderDiagnosisPage";
import ServiceOrderDiagnosisReportPage from "@features/workshop/pages/ServiceOrderDiagnosisReportPage";
import ServiceOrderItemsPage from "@features/workshop/pages/ServiceOrderItemsPage";
import ServiceOrderQuotePage from "@features/workshop/pages/ServiceOrderQuotePage";
import TeamLeaderDiagnosisApprovalPage from "@features/workshop/pages/TeamLeaderDiagnosisApprovalPage";
import ForemanDiagnosisApprovalPage from "@features/workshop/pages/ForemanDiagnosisApprovalPage";
import ManagerDiagnosisApprovalPage from "@features/workshop/pages/ManagerDiagnosisApprovalPage";
import VehicleConditionLogsPage from "@features/workshop/pages/VehicleConditionLogsPage";
import ServiceOrderEditPage from "@features/workshop/pages/ServiceOrderEditPage";
import ReceptionCreateAppointmentPage from "@features/workshop/pages/ReceptionCreateAppointmentPage";
import ReceptionEditAppointmentPage from "@features/workshop/pages/ReceptionEditAppointmentPage";
import ReceptionAppointmentsPage from "@features/workshop/pages/ReceptionAppointmentsPage";
import WorkshopRoleHistoryPage from "@features/workshop/pages/WorkshopRoleHistoryPage";
import ForemanBoardPage from "@features/workshop/pages/ForemanBoardPage";
import BayTimelinePage from "@features/workshop/pages/BayTimelinePage";
import MyTasksPage from "@features/workshop/pages/MyTasksPage";
import TechnicianAssignmentPage from "@features/workshop/pages/TechnicianAssignmentPage";
import QcChecklistPage from "@features/workshop/pages/QcChecklistPage";
import HandoverPage from "@features/workshop/pages/HandoverPage";
import MaterialRequestQueuePage from "@features/inventory/pages/MaterialRequestQueuePage";
import MaterialReturnPage from "@features/inventory/pages/MaterialReturnPage";
import StockAlertPage from "@features/inventory/pages/StockAlertPage";
import PurchaseOrderPage from "@features/inventory/pages/PurchaseOrderPage";
import GoodsReceiptPage from "@features/inventory/pages/GoodsReceiptPage";
import TimesheetPage from "@features/technician/pages/TimesheetPage";
import PayrollRunPage from "@features/hr/pages/PayrollRunPage";
import CommissionBreakdownPage from "@features/hr/pages/CommissionBreakdownPage";
import MyPayslipPage from "@features/payslip/pages/MyPayslipPage";
import QcTemplateAdminPage from "@features/qc/pages/QcTemplateAdminPage";
import StockHistoryPage from "@features/inventory/pages/StockHistoryPage";
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
import RetailCartPage from "@features/retail/pages/RetailCartPage";
import PartProductDetailPage from "@features/retail/pages/PartProductDetailPage";
import MyRetailOrdersPage from "@features/retail/pages/MyRetailOrdersPage";
import GarageDashboardPage from "@features/customerPortal/pages/GarageDashboardPage";
import MyVehiclesPage from "@features/customerPortal/pages/MyVehiclesPage";
import VehicleDetailPage from "@features/customerPortal/pages/VehicleDetailPage";
import MyNotificationsPage from "@features/customerPortal/pages/MyNotificationsPage";
import QuotationDetailPage from "@features/customerPortal/pages/QuotationDetailPage";
import PublicQuotationAccessPage from "@features/customerPortal/pages/PublicQuotationAccessPage";
import ReceptionChatPage from "@features/chat/pages/ReceptionChatPage";
import CustomerMessengerWidget from "@features/chat/components/CustomerMessengerWidget";
import CustomerChatbotWidget from "@features/chatbot/components/CustomerChatbotWidget";
import AccountingMain from "@horizon/AccountingMain";
import ContactPage from "@features/contact/pages/ContactPage";
import StorageServicesPage from "@features/storage/pages/StorageServicesPage";
import NewsJournalPage from "@features/news/pages/NewsJournalPage";

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
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="/quote-access/:token" element={<PublicQuotationAccessPage />} />
        <Route path="/auth/forgot-password" element={<Navigate to="/forgot-password" replace />} />
        <Route path="/auth/reset-password" element={<Navigate to="/reset-password" replace />} />
        <Route path="/home" element={<HomePage />} />
        <Route path="/contact" element={<ContactPage />} />
        <Route path="/storage" element={<StorageServicesPage />} />
        <Route path="/news" element={<NewsJournalPage />} />
        
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

        <Route
          path="/my-garage-dashboard"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><GarageDashboardPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />
        <Route
          path="/my-vehicles"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><MyVehiclesPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />
        <Route
          path="/my-vehicles/:vehicleId"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><VehicleDetailPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />
        <Route
          path="/my-notifications"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><MyNotificationsPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />
        <Route
          path="/my-quotations/:quotationId"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><QuotationDetailPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />

        <Route
          path="/parts-shop"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><PartsSalesPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />
        <Route
          path="/parts-cart"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><RetailCartPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />
        <Route
          path="/parts-shop/products/:productId"
          element={
            <ProtectedRoute allowedRoles={["CUSTOMER"]}>
              <CustomerRoute><PartProductDetailPage /></CustomerRoute>
            </ProtectedRoute>
          }
        />
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
            <ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN", "TEAM_LEADER"]}>
              <WorkshopPage />
            </ProtectedRoute>
          } 
        >
          <Route path="reception" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ReceptionOrdersSection /></ProtectedRoute>} />
          <Route path="reception/create" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><CreateServiceOrderPage /></ProtectedRoute>} />
          <Route path="reception/appointments" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ReceptionAppointmentsPage /></ProtectedRoute>} />
          <Route path="reception/chat" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ReceptionChatPage /></ProtectedRoute>} />
          <Route path="reception/appointments/new" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ReceptionCreateAppointmentPage /></ProtectedRoute>} />
          <Route path="reception/appointments/:appointmentId/edit" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ReceptionEditAppointmentPage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/diagnosis" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ServiceOrderDiagnosisPage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/diagnosis-report" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ServiceOrderDiagnosisReportPage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/items" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ServiceOrderItemsPage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/quote" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ServiceOrderQuotePage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/vehicleconditionlogs" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><VehicleConditionLogsPage /></ProtectedRoute>} />
          <Route path="reception/orders/:orderId/edit" element={<ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ServiceOrderEditPage /></ProtectedRoute>} />
          <Route path="manager" element={<ProtectedRoute allowedRoles={["MANAGER"]}><ManagerDashboard /></ProtectedRoute>} />
          <Route path="manager/dispatch/:orderId" element={<ProtectedRoute allowedRoles={["MANAGER"]}><ManagerDashboard /></ProtectedRoute>} />
          <Route path="manager/orders/:orderId/quote" element={<ProtectedRoute allowedRoles={["MANAGER"]}><ManagerDiagnosisApprovalPage /></ProtectedRoute>} />
          <Route path="foreman" element={<ProtectedRoute allowedRoles={["FOREMAN"]}><ForemanDashboard /></ProtectedRoute>} />
          <Route path="foreman/dispatch/:orderId" element={<ProtectedRoute allowedRoles={["FOREMAN"]}><ForemanDashboard /></ProtectedRoute>} />
          <Route path="foreman/orders/:orderId/quote" element={<ProtectedRoute allowedRoles={["FOREMAN"]}><ForemanDiagnosisApprovalPage /></ProtectedRoute>} />
          <Route path="team-leader" element={<ProtectedRoute allowedRoles={["TEAM_LEADER"]}><TeamLeaderDashboard /></ProtectedRoute>} />
          <Route path="team-leader/dispatch/:orderId" element={<ProtectedRoute allowedRoles={["TEAM_LEADER"]}><TeamLeaderDashboard /></ProtectedRoute>} />
          <Route path="team-leader/orders/:orderId/quote" element={<ProtectedRoute allowedRoles={["TEAM_LEADER"]}><TeamLeaderDiagnosisApprovalPage /></ProtectedRoute>} />
          <Route path="technician" element={<ProtectedRoute allowedRoles={["TECH"]}><TechnicianDashboard /></ProtectedRoute>} />
          <Route path="technician/orders/:orderId/diagnosis" element={<ProtectedRoute allowedRoles={["TECH"]}><ServiceOrderDiagnosisPage /></ProtectedRoute>} />
          <Route path="technician/history" element={<ProtectedRoute allowedRoles={["TECH"]}><WorkshopRoleHistoryPage /></ProtectedRoute>} />
          <Route path="qc" element={<ProtectedRoute allowedRoles={["QUALITY_CONTROL"]}><QCDashboard /></ProtectedRoute>} />
          <Route path="team-leader/history" element={<ProtectedRoute allowedRoles={["TEAM_LEADER"]}><WorkshopRoleHistoryPage /></ProtectedRoute>} />
          <Route path="foreman/history" element={<ProtectedRoute allowedRoles={["FOREMAN"]}><WorkshopRoleHistoryPage /></ProtectedRoute>} />
          {/* A17 – Foreman Kanban Board */}
          <Route path="foreman/board" element={<ProtectedRoute allowedRoles={["FOREMAN"]}><ForemanBoardPage /></ProtectedRoute>} />
          {/* A14 – Bay Timeline */}
          <Route path="foreman/bay-timeline" element={<ProtectedRoute allowedRoles={["FOREMAN"]}><BayTimelinePage /></ProtectedRoute>} />
          {/* A16 – Technician My Tasks */}
          <Route path="technician/my-tasks" element={<ProtectedRoute allowedRoles={["TECH"]}><MyTasksPage /></ProtectedRoute>} />
          {/* A15 – Technician Assignment */}
          <Route path="foreman/orders/:orderId/assign" element={<ProtectedRoute allowedRoles={["FOREMAN","TEAM_LEADER"]}><TechnicianAssignmentPage /></ProtectedRoute>} />
          {/* A18 – QC Checklist */}
          <Route path="qc/orders/:orderId/checklist" element={<ProtectedRoute allowedRoles={["QUALITY_CONTROL","FOREMAN"]}><QcChecklistPage /></ProtectedRoute>} />
          {/* Admin QC Templates */}
          <Route path="qc/templates" element={<ProtectedRoute allowedRoles={["ADMIN","MANAGER"]}><QcTemplateAdminPage /></ProtectedRoute>} />
          {/* A19 – Handover */}
          <Route path="reception/orders/:orderId/handover" element={<ProtectedRoute allowedRoles={["RECEPTIONIST","FOREMAN","ADMIN"]}><HandoverPage /></ProtectedRoute>} />
          {/* B04 – Material Request Queue */}
          <Route path="inventory/material-requests" element={<ProtectedRoute allowedRoles={["STOREKEEPER","FOREMAN","ADMIN"]}><MaterialRequestQueuePage /></ProtectedRoute>} />
          {/* B06 – Material Returns */}
          <Route path="inventory/material-returns" element={<ProtectedRoute allowedRoles={["STOREKEEPER","FOREMAN","ADMIN"]}><MaterialReturnPage /></ProtectedRoute>} />
          {/* B07 – Stock Alerts */}
          <Route path="inventory/stock-alerts" element={<ProtectedRoute allowedRoles={["STOREKEEPER","MANAGER","ADMIN"]}><StockAlertPage /></ProtectedRoute>} />
          {/* B08 – Purchase Orders */}
          <Route path="inventory/purchase-orders" element={<ProtectedRoute allowedRoles={["STOREKEEPER","MANAGER","ADMIN"]}><PurchaseOrderPage /></ProtectedRoute>} />
          <Route path="inventory/purchase-orders/new" element={<ProtectedRoute allowedRoles={["STOREKEEPER","MANAGER","ADMIN"]}><PurchaseOrderPage /></ProtectedRoute>} />
          {/* B09 – Goods Receipt Notes (GRN) */}
          <Route path="inventory/goods-receipts" element={<ProtectedRoute allowedRoles={["STOREKEEPER","ADMIN"]}><GoodsReceiptPage /></ProtectedRoute>} />
          <Route path="inventory/goods-receipt/new" element={<ProtectedRoute allowedRoles={["STOREKEEPER","ADMIN"]}><GoodsReceiptPage /></ProtectedRoute>} />
          {/* Stock History */}
          <Route path="inventory/history" element={<ProtectedRoute allowedRoles={["STOREKEEPER","MANAGER","ADMIN"]}><StockHistoryPage /></ProtectedRoute>} />

          {/* C03 – Timesheet (Technician) */}
          <Route path="technician/timesheet" element={<ProtectedRoute allowedRoles={["TECH","FOREMAN","TEAM_LEADER"]}><TimesheetPage /></ProtectedRoute>} />

          {/* C05 - Payroll Run */}
          <Route path="hr/payroll-run" element={<ProtectedRoute allowedRoles={["ADMIN","MANAGER"]}><PayrollRunPage /></ProtectedRoute>} />
          
          {/* C06 - Commission Breakdown */}
          <Route path="hr/commissions" element={<ProtectedRoute allowedRoles={["ADMIN","MANAGER","ACCOUNTANT"]}><CommissionBreakdownPage /></ProtectedRoute>} />
          
          {/* C07 - My Payslip */}
          <Route path="payslip" element={<ProtectedRoute allowedRoles={["TECH","FOREMAN","TEAM_LEADER","RECEPTIONIST","MANAGER","STOREKEEPER","QUALITY_CONTROL"]}><MyPayslipPage /></ProtectedRoute>} />

        </Route>

        <Route path="/horizon/*" element={<Navigate to="/admin/default" replace />} />
        <Route path="*" element={<Navigate to="/home" replace />} />
      </Routes>
      <CustomerChatbotWidget />
      <CustomerMessengerWidget />
    </BranchProvider>
  );
}

export default App;



