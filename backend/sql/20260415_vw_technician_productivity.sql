CREATE OR ALTER VIEW dbo.vw_TechnicianProductivity
AS
SELECT
    ssa.Id AS AssignmentId,
    ssa.TechnicianId,
    s.FullName AS TechnicianName,
    s.Specialty,
    CAST(ssa.ActualStartTime AS date) AS WorkDate,
    sod.Id AS ServiceOrderDetailId,
    sod.ServiceOrderId,
    so.Code AS ServiceOrderCode,
    sod.StandardServiceId,
    COALESCE(ss.Name, sod.ItemName) AS ServiceName,
    COALESCE(sod.EstimatedMinutes, ss.StandardMinutes, 0) AS FlatRateMinutes,
    DATEDIFF(MINUTE, ssa.ActualStartTime, ssa.ActualEndTime) AS ActualMinutes,
    CASE
        WHEN DATEDIFF(MINUTE, ssa.ActualStartTime, ssa.ActualEndTime) > 0
        THEN CAST(COALESCE(sod.EstimatedMinutes, ss.StandardMinutes, 0) AS decimal(18, 4))
             * 100.0
             / CAST(DATEDIFF(MINUTE, ssa.ActualStartTime, ssa.ActualEndTime) AS decimal(18, 4))
        ELSE 0
    END AS EfficiencyPercent,
    ssa.RoleInTask,
    ssa.SplitRatio,
    sod.FinalAmount AS TaskRevenue,
    CAST(sod.FinalAmount * ISNULL(NULLIF(ssa.SplitRatio, 0), 1) AS decimal(18, 2)) AS AttributedRevenue,
    ssa.ActualStartTime,
    ssa.ActualEndTime
FROM dbo.ServiceStaffAssignments ssa
JOIN dbo.Staffs s ON s.Id = ssa.TechnicianId
JOIN dbo.ServiceOrderDetails sod ON sod.Id = ssa.ServiceOrderDetailId
JOIN dbo.ServiceOrders so ON so.Id = sod.ServiceOrderId
LEFT JOIN dbo.StandardServices ss ON ss.Id = sod.StandardServiceId
WHERE ssa.ActualStartTime IS NOT NULL
  AND ssa.ActualEndTime IS NOT NULL;
GO
