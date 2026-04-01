import React from "react";
import { Icon } from "@chakra-ui/react";
import {
  MdAccountBalance,
  MdAssessment,
  MdCalendarToday,
  MdFileCopy,
  MdHome,
  MdPeople,
  MdPolicy,
  MdVerifiedUser,
} from "react-icons/md";
import AccountingDashboard from "views/admin/accounting";
import InvoicesPage from "views/admin/accounting/pages/InvoicesPage";
import AccountsPayablePage from "views/admin/accounting/pages/AccountsPayablePage";
import LedgerPage from "views/admin/accounting/pages/LedgerPage";
import PayrollPage from "views/admin/accounting/pages/PayrollPage";
import TaxDocumentsPage from "views/admin/accounting/pages/TaxDocumentsPage";
import ApprovalsPage from "views/admin/accounting/pages/ApprovalsPage";
import ReportsPage from "views/admin/accounting/pages/ReportsPage";

const accountingRoutes = [
  {
    name: "Bảng điều khiển",
    layout: "/accounting",
    icon: <Icon as={MdHome} width="20px" height="20px" color="inherit" />,
    path: "/dashboard",
    component: <AccountingDashboard />,
  },
  {
    name: "Hóa đơn",
    layout: "/accounting",
    icon: <Icon as={MdFileCopy} width="20px" height="20px" color="inherit" />,
    path: "/invoices",
    component: <InvoicesPage />,
  },
  {
    name: "Phải trả NCC",
    layout: "/accounting",
    icon: <Icon as={MdPeople} width="20px" height="20px" color="inherit" />,
    path: "/accounts-payable",
    component: <AccountsPayablePage />,
  },
  {
    name: "Sổ cái",
    layout: "/accounting",
    icon: <Icon as={MdAccountBalance} width="20px" height="20px" color="inherit" />,
    path: "/ledger",
    component: <LedgerPage />,
  },
  {
    name: "Tiền lương",
    layout: "/accounting",
    icon: <Icon as={MdCalendarToday} width="20px" height="20px" color="inherit" />,
    path: "/payroll",
    component: <PayrollPage />,
  },
  {
    name: "Thuế & chứng từ",
    layout: "/accounting",
    icon: <Icon as={MdPolicy} width="20px" height="20px" color="inherit" />,
    path: "/tax-documents",
    component: <TaxDocumentsPage />,
  },
  {
    name: "Phê duyệt tài chính",
    layout: "/accounting",
    icon: <Icon as={MdVerifiedUser} width="20px" height="20px" color="inherit" />,
    path: "/approvals",
    component: <ApprovalsPage />,
  },
  {
    name: "Báo cáo quản trị",
    layout: "/accounting",
    icon: <Icon as={MdAssessment} width="20px" height="20px" color="inherit" />,
    path: "/reports",
    component: <ReportsPage />,
  },
];

export default accountingRoutes;
