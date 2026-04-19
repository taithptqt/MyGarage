import React from "react";
import DiagnosisApprovalBoardPage from "./DiagnosisApprovalBoardPage";

export default function ManagerDiagnosisApprovalPage() {
  return (
    <DiagnosisApprovalBoardPage
      mode="MANAGER"
      title="Duyệt tài chính báo giá - Quản lý"
      backPath="/workshop/manager"
      backLabel="Quay lại"
    />
  );
}

