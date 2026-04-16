import React from "react";
import DiagnosisApprovalBoardPage from "./DiagnosisApprovalBoardPage";

export default function ManagerDiagnosisApprovalPage() {
  return (
    <DiagnosisApprovalBoardPage
      mode="FOREMAN"
      title="Duyệt chuẩn đoán - Quản lý xưởng"
      backPath="/workshop/manager"
      backLabel="Quay lại"
    />
  );
}
