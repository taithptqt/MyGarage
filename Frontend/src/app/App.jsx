import { lazy, Suspense } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import ErrorBoundary from "@shared/core/components/ErrorBoundary";
import ProtectedRoute from "@shared/core/components/auth/ProtectedRoute";
import { useAuth } from "@shared/core/context/AuthContext";
import { BranchProvider } from "@shared/core/context/BranchContext";
import { SceneTransitionProvider } from "@shared/core/context/SceneTransitionContext";
import useIdleTimeout from "@shared/core/hooks/useIdleTimeout";
import CustomerLayout from "@shared/layouts/CustomerLayout";
import PublicBrandTheme from "@shared/brand/PublicBrandTheme";
import HomeChatVisibility from "@shared/components/HomeChatVisibility";

// Lazy-loaded route components
const AuthPage = lazy(() => import("@features/auth/pages/AuthPage"));
const ForgotPasswordPage = lazy(() => import("@features/auth/recovery/ForgotPasswordPage"));
const ResetPasswordPage = lazy(() => import("@features/auth/recovery/ResetPasswordPage"));
const HomePage = lazy(() => import("@features/home/pages/HomePage"));
const ReferencePage = lazy(() => import("@shared/brand/ReferencePage"));
const AdminPage = lazy(() => import("@features/admin/pages/AdminPage"));
const DispatchCenterPage = lazy(() => import("@features/admin/pages/DispatchCenterPage"));
const WorkshopPage = lazy(() => import("@features/workshop/pages/WorkshopPage"));
const ReceptionOrdersSection = lazy(() => import("@features/workshop/components/ReceptionOrdersSection"));
const ManagerDashboard = lazy(() => import("@features/workshop/components/ManagerDashboard"));
const ForemanDashboard = lazy(() => import("@features/workshop/components/ForemanDashboard"));
const TeamLeaderDashboard = lazy(() => import("@features/workshop/components/TeamLeaderDashboard"));
const TechnicianDashboard = lazy(() => import("@features/workshop/components/TechnicianDashboard"));
const QCDashboard = lazy(() => import("@features/workshop/components/QCDashboard"));
const CreateServiceOrderPage = lazy(() => import("@features/workshop/pages/CreateServiceOrderPage"));
const ServiceOrderDiagnosisPage = lazy(() => import("@features/workshop/pages/ServiceOrderDiagnosisPage"));
const ServiceOrderDiagnosisReportPage = lazy(() => import("@features/workshop/pages/ServiceOrderDiagnosisReportPage"));
const ServiceOrderItemsPage = lazy(() => import("@features/workshop/pages/ServiceOrderItemsPage"));
const ServiceOrderQuotePage = lazy(() => import("@features/workshop/pages/ServiceOrderQuotePage"));
const TeamLeaderDiagnosisApprovalPage = lazy(() => import("@features/workshop/pages/TeamLeaderDiagnosisApprovalPage"));
const ForemanDiagnosisApprovalPage = lazy(() => import("@features/workshop/pages/ForemanDiagnosisApprovalPage"));
const ManagerDiagnosisApprovalPage = lazy(() => import("@features/workshop/pages/ManagerDiagnosisApprovalPage"));
const VehicleConditionLogsPage = lazy(() => import("@features/workshop/pages/VehicleConditionLogsPage"));
const ServiceOrderEditPage = lazy(() => import("@features/workshop/pages/ServiceOrderEditPage"));
const ReceptionCreateAppointmentPage = lazy(() => import("@features/workshop/pages/ReceptionCreateAppointmentPage"));
const ReceptionEditAppointmentPage = lazy(() => import("@features/workshop/pages/ReceptionEditAppointmentPage"));
const ReceptionAppointmentsPage = lazy(() => import("@features/workshop/pages/ReceptionAppointmentsPage"));
const WorkshopRoleHistoryPage = lazy(() => import("@features/workshop/pages/WorkshopRoleHistoryPage"));
const ForemanBoardPage = lazy(() => import("@features/workshop/pages/ForemanBoardPage"));
const BayTimelinePage = lazy(() => import("@features/workshop/pages/BayTimelinePage"));
const MyTasksPage = lazy(() => import("@features/workshop/pages/MyTasksPage"));
const TechnicianAssignmentPage = lazy(() => import("@features/workshop/pages/TechnicianAssignmentPage"));
const QcChecklistPage = lazy(() => import("@features/workshop/pages/QcChecklistPage"));
const HandoverPage = lazy(() => import("@features/workshop/pages/HandoverPage"));
const MaterialRequestQueuePage = lazy(() => import("@features/inventory/pages/MaterialRequestQueuePage"));
const MaterialReturnPage = lazy(() => import("@features/inventory/pages/MaterialReturnPage"));
const StockAlertPage = lazy(() => import("@features/inventory/pages/StockAlertPage"));
const PurchaseOrderPage = lazy(() => import("@features/inventory/pages/PurchaseOrderPage"));
const GoodsReceiptPage = lazy(() => import("@features/inventory/pages/GoodsReceiptPage"));
const TimesheetPage = lazy(() => import("@features/technician/pages/TimesheetPage"));
const PayrollRunPage = lazy(() => import("@features/hr/pages/PayrollRunPage"));
const CommissionBreakdownPage = lazy(() => import("@features/hr/pages/CommissionBreakdownPage"));
const MyPayslipPage = lazy(() => import("@features/payslip/pages/MyPayslipPage"));
const QcTemplateAdminPage = lazy(() => import("@features/qc/pages/QcTemplateAdminPage"));
const StockHistoryPage = lazy(() => import("@features/inventory/pages/StockHistoryPage"));
const HorizonMain = lazy(() => import("@horizon/Main"));
const BookingPage = lazy(() => import("@features/appointments/pages/BookingPage"));
const BookingIntroPage = lazy(() => import("@features/appointments/pages/BookingIntroPage"));
const MyAppointmentsPage = lazy(() => import("@features/appointments/pages/MyAppointmentsPage"));
const PayosCheckoutPage = lazy(() => import("@features/payment/pages/PayosCheckoutPage"));
const PaymentSuccessPage = lazy(() => import("@features/payment/pages/PaymentSuccessPage"));
const PaymentCancelPage = lazy(() => import("@features/payment/pages/PaymentCancelPage"));
const PartsSalesPage = lazy(() => import("@features/retail/pages/PartsSalesPage"));
const RetailCartPage = lazy(() => import("@features/retail/pages/RetailCartPage"));
const PartProductDetailPage = lazy(() => import("@features/retail/pages/PartProductDetailPage"));
const MyRetailOrdersPage = lazy(() => import("@features/retail/pages/MyRetailOrdersPage"));
const GarageDashboardPage = lazy(() => import("@features/customerPortal/pages/GarageDashboardPage"));
const MyVehiclesPage = lazy(() => import("@features/customerPortal/pages/MyVehiclesPage"));
const VehicleDetailPage = lazy(() => import("@features/customerPortal/pages/VehicleDetailPage"));
const CustomerVehicleConditionReportPage = lazy(() => import("@features/customerPortal/pages/CustomerVehicleConditionReportPage"));
const MyNotificationsPage = lazy(() => import("@features/customerPortal/pages/MyNotificationsPage"));
const QuotationDetailPage = lazy(() => import("@features/customerPortal/pages/QuotationDetailPage"));
const PublicQuotationAccessPage = lazy(() => import("@features/customerPortal/pages/PublicQuotationAccessPage"));
const ReceptionChatPage = lazy(() => import("@features/chat/pages/ReceptionChatPage"));
const CustomerMessengerWidget = lazy(() => import("@features/chat/components/CustomerMessengerWidget"));
const CustomerChatbotWidget = lazy(() => import("@features/chatbot/components/CustomerChatbotWidget"));
const AccountingMain = lazy(() => import("@horizon/AccountingMain"));
const CustomerServicesPage = lazy(() => import("@features/services/pages/CustomerServicesPage"));
const ContactPage = lazy(() => import("@features/contact/pages/ContactPage"));
const StorageServicesPage = lazy(() => import("@features/storage/pages/StorageServicesPage"));
const Garage3DPage = lazy(() => import("@features/storage/garage3d/Garage3DPage"));
const CustomerGarage3DPage = lazy(() => import("@features/storage/garage3d/CustomerGarage3DPage"));
const CustomRequestsPage = lazy(() => import("@features/storage/garage3d/CustomRequestsPage"));
const NewsJournalPage = lazy(() => import("@features/news/pages/NewsJournalPage"));

function RouteLoadingSpinner() {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "50vh",
        width: "100%",
        padding: "2rem",
      }}
    >
      <div
        style={{
          width: "40px",
          height: "40px",
          border: "3px solid #e2e8f0",
          borderTopColor: "#3182ce",
          borderRadius: "50%",
          animation: "spin 0.8s linear infinite",
        }}
      />
      <style>{`
        @keyframes spin {
          0% { transform: rotate(0deg); }
          100% { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
}

function CustomerRoute({ children }) {
  return (
    <CustomerLayout>
      <Suspense fallback={<RouteLoadingSpinner />}>
        {children}
      </Suspense>
    </CustomerLayout>
  );
}

function WorkshopChildRoute({ allowedRoles, children }) {
  return (
    <ProtectedRoute allowedRoles={allowedRoles}>
      <Suspense fallback={<RouteLoadingSpinner />}>
        {children}
      </Suspense>
    </ProtectedRoute>
  );
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
    <ErrorBoundary>
      <PublicBrandTheme />
      <BranchProvider>
        <SceneTransitionProvider>
          <Suspense fallback={<RouteLoadingSpinner />}>
          <Routes>
            <Route path="/" element={<Navigate to="/home" replace />} />
            <Route path="/login" element={<AuthPage />} />
            <Route path="/forgot-password" element={<ForgotPasswordPage />} />
            <Route path="/reset-password" element={<ResetPasswordPage />} />
            <Route path="/quote-access/:token" element={<PublicQuotationAccessPage />} />
            <Route path="/auth/forgot-password" element={<Navigate to="/forgot-password" replace />} />
            <Route path="/auth/reset-password" element={<Navigate to="/reset-password" replace />} />
            <Route path="/home" element={<HomePage />} />
            <Route path="/services" element={<CustomerServicesPage />} />
            <Route path="/tracking" element={<Navigate to="/my-garage-dashboard" replace />} />
            <Route path="/contact" element={<ContactPage />} />
            <Route path="/storage" element={<Navigate to="/services" replace />} />
            <Route path="/storage/garage-3d" element={<Garage3DPage />} />
            <Route path="/my-garage-3d" element={<ProtectedRoute allowedRoles={["CUSTOMER"]}><CustomerRoute><CustomerGarage3DPage /></CustomerRoute></ProtectedRoute>} />
            <Route path="/news" element={<Navigate to="/services" replace />} />
            <Route path="/discover/about-my-garage" element={<Navigate to="/home" replace />} />
            <Route path="/events" element={<ReferencePage sourcePath="/en/meetings-events" />} />
            <Route path="/partners" element={<ReferencePage sourcePath="/en/partners" />} />
            <Route path="/discover/*" element={<ReferencePage />} />

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
              path="/my-vehicles/:vehicleId/condition-report"
              element={
                <ProtectedRoute allowedRoles={["CUSTOMER"]}>
                  <CustomerRoute><CustomerVehicleConditionReportPage /></CustomerRoute>
                </ProtectedRoute>
              }
            />
            <Route
              path="/my-condition-report/:serviceOrderId"
              element={
                <ProtectedRoute allowedRoles={["CUSTOMER"]}>
                  <CustomerRoute><CustomerVehicleConditionReportPage /></CustomerRoute>
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
                <CustomerRoute><PartsSalesPage /></CustomerRoute>
              }
            />
            <Route
              path="/parts-cart"
              element={
                <CustomerRoute><RetailCartPage /></CustomerRoute>
              }
            />
            <Route
              path="/parts-shop/products/:productId"
              element={
                <CustomerRoute><PartProductDetailPage /></CustomerRoute>
              }
            />
            <Route
              path="/my-retail-orders"
              element={
                <ProtectedRoute allowedRoles={["CUSTOMER", "ADMIN", "STAFF", "MANAGER"]}>
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
                <ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}>
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
                <ProtectedRoute allowedRoles={["ADMIN", "RECEPTIONIST", "TECH", "MANAGER", "QUALITY_CONTROL", "FOREMAN", "TEAM_LEADER", "STORE_KEEPER", "ACCOUNTANT"]}>
                  <WorkshopPage />
                </ProtectedRoute>
              }
            >
              <Route path="reception" element={<Navigate to="/admin/service-orders" replace />} />
              <Route path="reception/create" element={<WorkshopChildRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><CreateServiceOrderPage /></WorkshopChildRoute>} />
              <Route path="reception/appointments" element={<Navigate to="/admin/appointments" replace />} />
              <Route path="reception/chat" element={<Navigate to="/admin/chat" replace />} />
              <Route path="reception/appointments/new" element={<WorkshopChildRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ReceptionCreateAppointmentPage /></WorkshopChildRoute>} />
              <Route path="reception/appointments/:appointmentId/edit" element={<WorkshopChildRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ReceptionEditAppointmentPage /></WorkshopChildRoute>} />
              <Route path="reception/orders/:orderId/diagnosis" element={<WorkshopChildRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ServiceOrderDiagnosisPage /></WorkshopChildRoute>} />
              <Route path="reception/orders/:orderId/diagnosis-report" element={<WorkshopChildRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ServiceOrderDiagnosisReportPage /></WorkshopChildRoute>} />
              <Route path="reception/orders/:orderId/items" element={<WorkshopChildRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ServiceOrderItemsPage /></WorkshopChildRoute>} />
              <Route path="reception/orders/:orderId/quote" element={<WorkshopChildRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ServiceOrderQuotePage /></WorkshopChildRoute>} />
              <Route path="reception/orders/:orderId/vehicleconditionlogs" element={<WorkshopChildRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><VehicleConditionLogsPage /></WorkshopChildRoute>} />
              <Route path="reception/orders/:orderId/edit" element={<WorkshopChildRoute allowedRoles={["ADMIN", "RECEPTIONIST"]}><ServiceOrderEditPage /></WorkshopChildRoute>} />
              <Route path="manager" element={<WorkshopChildRoute allowedRoles={["MANAGER"]}><ManagerDashboard /></WorkshopChildRoute>} />
              <Route path="custom-requests" element={<WorkshopChildRoute allowedRoles={["ADMIN", "MANAGER"]}><CustomRequestsPage /></WorkshopChildRoute>} />
              <Route path="manager/dispatch/:orderId" element={<WorkshopChildRoute allowedRoles={["MANAGER"]}><ManagerDashboard /></WorkshopChildRoute>} />
              <Route path="manager/orders/:orderId/quote" element={<WorkshopChildRoute allowedRoles={["MANAGER"]}><ManagerDiagnosisApprovalPage /></WorkshopChildRoute>} />
              <Route path="foreman" element={<WorkshopChildRoute allowedRoles={["FOREMAN"]}><ForemanDashboard /></WorkshopChildRoute>} />
              <Route path="foreman/dispatch/:orderId" element={<WorkshopChildRoute allowedRoles={["FOREMAN"]}><ForemanDashboard /></WorkshopChildRoute>} />
              <Route path="foreman/orders/:orderId/quote" element={<WorkshopChildRoute allowedRoles={["FOREMAN"]}><ForemanDiagnosisApprovalPage /></WorkshopChildRoute>} />
              <Route path="team-leader" element={<WorkshopChildRoute allowedRoles={["TEAM_LEADER"]}><TeamLeaderDashboard /></WorkshopChildRoute>} />
              <Route path="team-leader/dispatch/:orderId" element={<WorkshopChildRoute allowedRoles={["TEAM_LEADER"]}><TeamLeaderDashboard /></WorkshopChildRoute>} />
              <Route path="team-leader/orders/:orderId/quote" element={<WorkshopChildRoute allowedRoles={["TEAM_LEADER"]}><TeamLeaderDiagnosisApprovalPage /></WorkshopChildRoute>} />
              <Route path="technician" element={<WorkshopChildRoute allowedRoles={["TECH"]}><TechnicianDashboard /></WorkshopChildRoute>} />
              <Route path="technician/orders/:orderId/diagnosis" element={<WorkshopChildRoute allowedRoles={["TECH"]}><ServiceOrderDiagnosisPage /></WorkshopChildRoute>} />
              <Route path="technician/history" element={<WorkshopChildRoute allowedRoles={["TECH"]}><WorkshopRoleHistoryPage /></WorkshopChildRoute>} />
              <Route path="qc" element={<WorkshopChildRoute allowedRoles={["QUALITY_CONTROL"]}><QCDashboard /></WorkshopChildRoute>} />
              <Route path="team-leader/history" element={<WorkshopChildRoute allowedRoles={["TEAM_LEADER"]}><WorkshopRoleHistoryPage /></WorkshopChildRoute>} />
              <Route path="foreman/history" element={<WorkshopChildRoute allowedRoles={["FOREMAN"]}><WorkshopRoleHistoryPage /></WorkshopChildRoute>} />
              {/* A17 – Foreman Kanban Board */}
              <Route path="foreman/board" element={<WorkshopChildRoute allowedRoles={["FOREMAN"]}><ForemanBoardPage /></WorkshopChildRoute>} />
              {/* A14 – Bay Timeline */}
              <Route path="foreman/bay-timeline" element={<WorkshopChildRoute allowedRoles={["FOREMAN"]}><BayTimelinePage /></WorkshopChildRoute>} />
              {/* A16 – Technician My Tasks */}
              <Route path="technician/my-tasks" element={<WorkshopChildRoute allowedRoles={["TECH"]}><MyTasksPage /></WorkshopChildRoute>} />
              {/* A15 – Technician Assignment */}
              <Route path="foreman/orders/:orderId/assign" element={<WorkshopChildRoute allowedRoles={["FOREMAN","TEAM_LEADER"]}><TechnicianAssignmentPage /></WorkshopChildRoute>} />
              {/* A18 – QC Checklist */}
              <Route path="qc/orders/:orderId/checklist" element={<WorkshopChildRoute allowedRoles={["QUALITY_CONTROL","FOREMAN"]}><QcChecklistPage /></WorkshopChildRoute>} />
              {/* Admin QC Templates */}
              <Route path="qc/templates" element={<WorkshopChildRoute allowedRoles={["ADMIN","MANAGER"]}><QcTemplateAdminPage /></WorkshopChildRoute>} />
              {/* A19 – Handover */}
              <Route path="reception/orders/:orderId/handover" element={<WorkshopChildRoute allowedRoles={["RECEPTIONIST","FOREMAN","ADMIN"]}><HandoverPage /></WorkshopChildRoute>} />
              {/* B04 – Material Request Queue */}
              <Route path="inventory/material-requests" element={<WorkshopChildRoute allowedRoles={["STORE_KEEPER","FOREMAN","ADMIN"]}><MaterialRequestQueuePage /></WorkshopChildRoute>} />
              {/* B06 – Material Returns */}
              <Route path="inventory/material-returns" element={<WorkshopChildRoute allowedRoles={["STORE_KEEPER","FOREMAN","ADMIN"]}><MaterialReturnPage /></WorkshopChildRoute>} />
              {/* B07 – Stock Alerts */}
              <Route path="inventory/stock-alerts" element={<WorkshopChildRoute allowedRoles={["STORE_KEEPER","MANAGER","ADMIN"]}><StockAlertPage /></WorkshopChildRoute>} />
              {/* B08 – Purchase Orders */}
              <Route path="inventory/purchase-orders" element={<WorkshopChildRoute allowedRoles={["STORE_KEEPER","MANAGER","ADMIN"]}><PurchaseOrderPage /></WorkshopChildRoute>} />
              <Route path="inventory/purchase-orders/new" element={<WorkshopChildRoute allowedRoles={["STORE_KEEPER","MANAGER","ADMIN"]}><PurchaseOrderPage /></WorkshopChildRoute>} />
              {/* B09 – Goods Receipt Notes (GRN) */}
              <Route path="inventory/goods-receipts" element={<WorkshopChildRoute allowedRoles={["STORE_KEEPER","ADMIN"]}><GoodsReceiptPage /></WorkshopChildRoute>} />
              <Route path="inventory/goods-receipt/new" element={<WorkshopChildRoute allowedRoles={["STORE_KEEPER","ADMIN"]}><GoodsReceiptPage /></WorkshopChildRoute>} />
              {/* Stock History */}
              <Route path="inventory/history" element={<WorkshopChildRoute allowedRoles={["STORE_KEEPER","MANAGER","ADMIN"]}><StockHistoryPage /></WorkshopChildRoute>} />

              {/* C03 – Timesheet (Technician) */}
              <Route path="technician/timesheet" element={<WorkshopChildRoute allowedRoles={["TECH","FOREMAN","TEAM_LEADER"]}><TimesheetPage /></WorkshopChildRoute>} />

              {/* C05 - Payroll Run */}
              <Route path="hr/payroll-run" element={<WorkshopChildRoute allowedRoles={["ADMIN","MANAGER"]}><PayrollRunPage /></WorkshopChildRoute>} />

              {/* C06 - Commission Breakdown */}
              <Route path="hr/commissions" element={<WorkshopChildRoute allowedRoles={["ADMIN","MANAGER","ACCOUNTANT"]}><CommissionBreakdownPage /></WorkshopChildRoute>} />

              {/* C07 - My Payslip */}
              <Route path="payslip" element={<WorkshopChildRoute allowedRoles={["TECH","FOREMAN","TEAM_LEADER","RECEPTIONIST","MANAGER","STORE_KEEPER","QUALITY_CONTROL","ACCOUNTANT"]}><MyPayslipPage /></WorkshopChildRoute>} />
            </Route>

            <Route path="/horizon/*" element={<Navigate to="/admin/default" replace />} />
            <Route path="*" element={<Navigate to="/home" replace />} />
          </Routes>
        </Suspense>
        <Suspense fallback={null}>
          <HomeChatVisibility>
            <CustomerChatbotWidget />
            <CustomerMessengerWidget />
          </HomeChatVisibility>
        </Suspense>
        </SceneTransitionProvider>
      </BranchProvider>
    </ErrorBoundary>
  );
}

export default App;
