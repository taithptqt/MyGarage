import React from "react";
import DiagnosisApprovalBoardPage from "./DiagnosisApprovalBoardPage";

export default function TeamLeaderDiagnosisApprovalPage() {
  return (
    <DiagnosisApprovalBoardPage
      mode="TEAM_LEADER"
      title="Duyệt chẩn đoán"
      backPath="/workshop/team-leader"
      backLabel="Quay lại"
    />
  );
}
