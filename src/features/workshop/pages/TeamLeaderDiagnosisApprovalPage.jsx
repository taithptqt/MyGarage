import React from "react";
import DiagnosisApprovalBoardPage from "./DiagnosisApprovalBoardPage";

export default function TeamLeaderDiagnosisApprovalPage() {
  return (
    <DiagnosisApprovalBoardPage
      mode="TEAM_LEADER"
      title="Duyệt chuẩn đoán - Tổ trưởng"
      backPath="/workshop/team-leader"
      backLabel="Quay lại"
    />
  );
}
