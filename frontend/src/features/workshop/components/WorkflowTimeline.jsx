import React from "react";

const STAGES = [
  { key: "RECEPTION", label: "Tiếp nhận" },
  { key: "DIAGNOSING", label: "Chẩn đoán" },
  { key: "WAITING_APPROVAL", label: "Báo giá" },
  { key: "REPAIRING", label: "Sửa chữa" },
  { key: "QC_PENDING", label: "Quality Control" },
  { key: "READY", label: "Sẵn sàng" }
];

export default function WorkflowTimeline({ currentStatus }) {
  const currentIndex = STAGES.findIndex(s => s.key === currentStatus);
  
  return (
    <div className="workflow-timeline">
      {STAGES.map((stage, index) => {
        const isCompleted = index < currentIndex;
        const isActive = index === currentIndex;
        
        return (
          <div key={stage.key} className={`timeline-step ${isCompleted ? "completed" : ""} ${isActive ? "active" : ""}`}>
            <div className="step-circle">
                {isCompleted ? "✓" : index + 1}
            </div>
            <div className="step-label">{stage.label}</div>
            {index < STAGES.length - 1 && <div className="step-line" />}
          </div>
        );
      })}

      <style>{`
        .workflow-timeline {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 20px;
          background: #fff;
          border-radius: 16px;
          box-shadow: 0 4px 12px rgba(0,0,0,0.05);
          margin-bottom: 24px;
        }
        .timeline-step {
          display: flex;
          flex-direction: column;
          align-items: center;
          position: relative;
          flex: 1;
        }
        .step-circle {
          width: 32px;
          height: 32px;
          border-radius: 50%;
          background: #e9edf7;
          color: #a3aed0;
          display: flex;
          align-items: center;
          justify-content: center;
          font-weight: 700;
          font-size: 14px;
          z-index: 2;
          border: 2px solid transparent;
          transition: all 0.3s ease;
        }
        .timeline-step.active .step-circle {
          background: #4318ff;
          color: #fff;
          box-shadow: 0 0 0 4px rgba(67, 24, 255, 0.1);
        }
        .timeline-step.completed .step-circle {
          background: #05cd99;
          color: #fff;
        }
        .step-label {
          margin-top: 8px;
          font-size: 12px;
          font-weight: 600;
          color: #a3aed0;
        }
        .timeline-step.active .step-label {
          color: #2b3674;
        }
        .timeline-step.completed .step-label {
          color: #05cd99;
        }
        .step-line {
          position: absolute;
          top: 16px;
          left: 50%;
          width: 100%;
          height: 2px;
          background: #e9edf7;
          z-index: 1;
        }
        .timeline-step.completed .step-line {
          background: #05cd99;
        }
      `}</style>
    </div>
  );
}



