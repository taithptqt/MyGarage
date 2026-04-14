import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
  Flex,
  Input,
  Select,
  SimpleGrid,
  Spinner,
  Table,
  Tbody,
  Td,
  Text,
  Th,
  Thead,
  Tr,
  useColorModeValue,
} from "@chakra-ui/react";
import Card from "components/card/Card";
import { getAllStaff } from "../../../../../features/admin/api/staffApi";
import { useBranch } from "../../../../../context/BranchContext";
import {
  getPayrollItems,
  getPayrollPeriods,
  getPayrollRuns,
  getPayslips,
} from "../api/accountingApi";
import {
  formatCurrency,
  formatDateTime,
  normalizeText,
  sumBy,
  toNumber,
} from "../utils/accountingHelpers";

function pick(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

function toId(value) {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? Math.trunc(n) : null;
}

function safeTime(value) {
  const ts = Date.parse(String(value || ""));
  return Number.isNaN(ts) ? 0 : ts;
}

function toDateKey(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function calcNetPay(item) {
  const explicitNetPay = toNumber(pick(item, "netPay", "NetPay"));
  if (explicitNetPay > 0) return explicitNetPay;

  const baseSalary = toNumber(pick(item, "baseSalary", "BaseSalary"));
  const overtimeAmount = toNumber(pick(item, "overtimeAmount", "OvertimeAmount"));
  const allowanceAmount = toNumber(pick(item, "allowanceAmount", "AllowanceAmount"));
  const commissionAmount = toNumber(pick(item, "commissionAmount", "CommissionAmount"));
  const bonusAmount = toNumber(pick(item, "bonusAmount", "BonusAmount"));

  const attendanceDeduction = toNumber(pick(item, "attendanceDeduction", "AttendanceDeduction"));
  const penaltyAmount = toNumber(pick(item, "penaltyAmount", "PenaltyAmount"));
  const advanceDeduction = toNumber(pick(item, "advanceDeduction", "AdvanceDeduction"));
  const loanDeduction = toNumber(pick(item, "loanDeduction", "LoanDeduction"));
  const insuranceEmployeeAmount = toNumber(pick(item, "insuranceEmployeeAmount", "InsuranceEmployeeAmount"));
  const pitAmount = toNumber(pick(item, "pitAmount", "PitAmount"));
  const otherDeduction = toNumber(pick(item, "otherDeduction", "OtherDeduction"));

  const grossPay = baseSalary + overtimeAmount + allowanceAmount + commissionAmount + bonusAmount;
  const totalDeduction =
    attendanceDeduction +
    penaltyAmount +
    advanceDeduction +
    loanDeduction +
    insuranceEmployeeAmount +
    pitAmount +
    otherDeduction;
  return Math.max(0, grossPay - totalDeduction);
}

export default function PayslipHistoryPage() {
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");
  const { selectedBranchId, isBranchLocked } = useBranch();
  const enforcedBranchId = isBranchLocked ? toId(selectedBranchId) : null;
  const targetBranchId = enforcedBranchId || toId(selectedBranchId);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [staffs, setStaffs] = useState([]);
  const [periods, setPeriods] = useState({ available: false, items: [] });
  const [runs, setRuns] = useState({ available: false, items: [] });
  const [items, setItems] = useState({ available: false, items: [] });
  const [payslips, setPayslips] = useState({ available: false, items: [] });

  const [search, setSearch] = useState("");
  const [periodFilter, setPeriodFilter] = useState("ALL");
  const [viewFilter, setViewFilter] = useState("ALL");

  useEffect(() => {
    let mounted = true;

    async function loadData() {
      setLoading(true);
      setError("");

      const [staffRes, periodRes, runRes, itemRes, payslipRes] = await Promise.allSettled([
        getAllStaff(),
        getPayrollPeriods(),
        getPayrollRuns(),
        getPayrollItems(),
        getPayslips(),
      ]);

      if (!mounted) return;

      setStaffs(
        staffRes.status === "fulfilled" && Array.isArray(staffRes.value)
          ? staffRes.value
          : [],
      );
      setPeriods(periodRes.status === "fulfilled" ? periodRes.value : { available: false, items: [] });
      setRuns(runRes.status === "fulfilled" ? runRes.value : { available: false, items: [] });
      setItems(itemRes.status === "fulfilled" ? itemRes.value : { available: false, items: [] });
      setPayslips(payslipRes.status === "fulfilled" ? payslipRes.value : { available: false, items: [] });

      if (
        periodRes.status === "rejected" &&
        runRes.status === "rejected" &&
        itemRes.status === "rejected" &&
        payslipRes.status === "rejected"
      ) {
        setError("Không tải được dữ liệu lịch sử phiếu lương từ backend.");
      }

      setLoading(false);
    }

    loadData();
    return () => {
      mounted = false;
    };
  }, [targetBranchId]);

  const staffNameById = useMemo(() => {
    const map = new Map();
    (staffs || []).forEach((staff) => {
      const id = toId(pick(staff, "id", "Id"));
      if (!id) return;
      map.set(id, pick(staff, "fullName", "FullName", "name", "Name", "email", "Email") || `Nhân sự #${id}`);
    });
    return map;
  }, [staffs]);

  const periodCodeById = useMemo(() => {
    const map = new Map();
    (periods.items || []).forEach((period) => {
      const id = toId(pick(period, "id", "Id"));
      if (!id) return;
      const periodCode = String(pick(period, "periodCode", "PeriodCode") || "").trim();
      if (!periodCode) return;
      map.set(id, periodCode);
    });
    return map;
  }, [periods.items]);

  const runById = useMemo(() => {
    const map = new Map();
    (runs.items || []).forEach((run, idx) => {
      const id = toId(pick(run, "id", "Id")) || idx + 1;
      const payrollPeriodId = toId(pick(run, "payrollPeriodId", "PayrollPeriodId"));
      const branchId = toId(pick(run, "branchId", "BranchId"));
      const periodCodeFromPeriod = payrollPeriodId ? periodCodeById.get(payrollPeriodId) : "";
      const periodCode =
        periodCodeFromPeriod ||
        pick(run, "periodCode", "PeriodCode") ||
        (payrollPeriodId ? `KY-${payrollPeriodId}` : "--");
      map.set(id, {
        id,
        payrollPeriodId,
        branchId,
        periodCode,
        runNo: pick(run, "runNo", "RunNo", "runCode", "RunCode") || id,
      });
    });
    return map;
  }, [periodCodeById, runs.items]);

  const payrollItemById = useMemo(() => {
    const map = new Map();
    (items.items || []).forEach((item, idx) => {
      const id = toId(pick(item, "id", "Id")) || idx + 1;
      const payrollRunId = toId(pick(item, "payrollRunId", "PayrollRunId"));
      const staffId = toId(pick(item, "staffId", "StaffId"));
      const runRow = payrollRunId ? runById.get(payrollRunId) || null : null;
      if (targetBranchId && (!runRow?.branchId || runRow.branchId !== targetBranchId)) return;

      map.set(id, {
        id,
        payrollRunId,
        staffId,
        staffName: staffId ? staffNameById.get(staffId) || `Nhân sự #${staffId}` : "--",
        periodCode: runRow?.periodCode || "--",
        runNo: runRow?.runNo || "--",
        netPay: calcNetPay(item),
      });
    });
    return map;
  }, [items.items, runById, staffNameById, targetBranchId]);

  const historyRows = useMemo(() => {
    const rows = (payslips.items || []).map((slip, idx) => {
      const id = toId(pick(slip, "id", "Id")) || idx + 1;
      const payrollItemId = toId(pick(slip, "payrollItemId", "PayrollItemId"));
      const itemRow = payrollItemId ? payrollItemById.get(payrollItemId) || null : null;

      return {
        id,
        slipNo: pick(slip, "slipNo", "SlipNo") || `PS-${id}`,
        payrollItemId,
        staffName: itemRow?.staffName || "--",
        periodCode: itemRow?.periodCode || "--",
        runNo: itemRow?.runNo || "--",
        netPay: toNumber(itemRow?.netPay),
        issuedAt: pick(slip, "issuedAt", "IssuedAt"),
        issuedById: toId(pick(slip, "issuedBy", "IssuedBy")),
        issuedByName: toId(pick(slip, "issuedBy", "IssuedBy"))
          ? staffNameById.get(toId(pick(slip, "issuedBy", "IssuedBy"))) || `#${toId(pick(slip, "issuedBy", "IssuedBy"))}`
          : "--",
        viewedAt: pick(slip, "viewedAt", "ViewedAt"),
        fileUrl: String(pick(slip, "fileUrl", "FileUrl") || "").trim(),
      };
    });

    const scoped = targetBranchId
      ? rows.filter((row) => !row.payrollItemId || payrollItemById.has(row.payrollItemId))
      : rows;

    return scoped.sort((a, b) => {
      const byIssuedTime = safeTime(b.issuedAt) - safeTime(a.issuedAt);
      if (byIssuedTime !== 0) return byIssuedTime;
      return Number(b.id) - Number(a.id);
    });
  }, [payrollItemById, payslips.items, staffNameById, targetBranchId]);

  const periodOptions = useMemo(() => {
    const unique = new Set();
    historyRows.forEach((row) => {
      const code = String(row.periodCode || "").trim();
      if (code && code !== "--") unique.add(code);
    });
    return Array.from(unique).sort((a, b) => a.localeCompare(b, "vi"));
  }, [historyRows]);

  useEffect(() => {
    if (periodFilter !== "ALL" && !periodOptions.includes(periodFilter)) {
      setPeriodFilter("ALL");
    }
  }, [periodFilter, periodOptions]);

  const filteredRows = useMemo(() => {
    return historyRows.filter((row) => {
      const isViewed = Boolean(row.viewedAt);
      const passPeriod = periodFilter === "ALL" || row.periodCode === periodFilter;
      const passView =
        viewFilter === "ALL" ||
        (viewFilter === "VIEWED" && isViewed) ||
        (viewFilter === "UNVIEWED" && !isViewed);
      const searchTarget = normalizeText(
        [
          row.slipNo,
          row.staffName,
          row.periodCode,
          row.runNo,
          row.issuedByName,
          row.payrollItemId ? `#${row.payrollItemId}` : "",
        ].join(" "),
      );
      const passSearch = !search || searchTarget.includes(normalizeText(search));
      return passPeriod && passView && passSearch;
    });
  }, [historyRows, periodFilter, search, viewFilter]);

  const summary = useMemo(() => {
    const total = historyRows.length;
    const viewed = historyRows.filter((row) => Boolean(row.viewedAt)).length;
    const unviewed = Math.max(0, total - viewed);
    const issuedToday = historyRows.filter((row) => toDateKey(row.issuedAt) === toDateKey(new Date())).length;
    const totalNetPay = sumBy(historyRows, (row) => row.netPay);
    return { total, viewed, unviewed, issuedToday, totalNetPay };
  }, [historyRows]);

  if (loading) {
    return (
      <Flex minH='260px' align='center' justify='center' direction='column' gap='10px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải lịch sử phiếu lương...</Text>
      </Flex>
    );
  }

  return (
    <Box>
      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, xl: 4 }} gap='20px' mb='20px'>
        <Card p='16px'>
          <Text color={textColorSecondary} fontSize='xs'>Tổng phiếu đã phát hành</Text>
          <Text color={textColor} fontSize='2xl' fontWeight='800'>{summary.total}</Text>
        </Card>
        <Card p='16px'>
          <Text color={textColorSecondary} fontSize='xs'>Phiếu đã xem</Text>
          <Text color='green.500' fontSize='2xl' fontWeight='800'>{summary.viewed}</Text>
        </Card>
        <Card p='16px'>
          <Text color={textColorSecondary} fontSize='xs'>Phiếu chưa xem</Text>
          <Text color='orange.500' fontSize='2xl' fontWeight='800'>{summary.unviewed}</Text>
        </Card>
        <Card p='16px'>
          <Text color={textColorSecondary} fontSize='xs'>Tổng thực nhận đã phát hành</Text>
          <Text color={textColor} fontSize='xl' fontWeight='800'>{formatCurrency(summary.totalNetPay)}</Text>
          <Text color={textColorSecondary} fontSize='xs'>Phát hành hôm nay: {summary.issuedToday}</Text>
        </Card>
      </SimpleGrid>

      <Card p='20px'>
        <Flex justify='space-between' align='center' mb='12px' wrap='wrap' gap='10px'>
          <Text color={textColor} fontWeight='700'>Chi tiết lịch sử phát hành</Text>
          <Badge colorScheme='purple'>Kết quả: {filteredRows.length}</Badge>
        </Flex>

        <Flex wrap='wrap' gap='10px' mb='12px'>
          <Select value={periodFilter} onChange={(e) => setPeriodFilter(e.target.value)} maxW='220px'>
            <option value='ALL'>Tất cả kỳ lương</option>
            {periodOptions.map((periodCode) => (
              <option value={periodCode} key={periodCode}>{periodCode}</option>
            ))}
          </Select>
          <Select value={viewFilter} onChange={(e) => setViewFilter(e.target.value)} maxW='220px'>
            <option value='ALL'>Tất cả trạng thái xem</option>
            <option value='VIEWED'>Đã xem</option>
            <option value='UNVIEWED'>Chưa xem</option>
          </Select>
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder='Tìm mã phiếu / nhân sự / kỳ / người phát hành'
            maxW='360px'
          />
        </Flex>

        {filteredRows.length ? (
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Mã phiếu</Th>
                <Th>Nhân sự</Th>
                <Th>Mã kỳ</Th>
                <Th>Dòng lương</Th>
                <Th isNumeric>Thực nhận</Th>
                <Th>Thời gian phát hành</Th>
                <Th>Người phát hành</Th>
                <Th>Trạng thái xem</Th>
              </Tr>
            </Thead>
            <Tbody>
              {filteredRows.slice(0, 80).map((row) => {
                const isViewed = Boolean(row.viewedAt);
                return (
                  <Tr key={row.id}>
                    <Td color={textColor} fontWeight='700'>
                      {row.slipNo}
                      <Text color={textColorSecondary} fontSize='xs'>
                        {row.fileUrl ? "Có tệp phiếu" : "Chưa có tệp đính kèm"}
                      </Text>
                    </Td>
                    <Td color={textColorSecondary}>{row.staffName}</Td>
                    <Td color={textColorSecondary}>{row.periodCode}</Td>
                    <Td color={textColorSecondary}>
                      <Text>#{row.payrollItemId || "--"}</Text>
                      <Text fontSize='xs'>{row.runNo}</Text>
                    </Td>
                    <Td color={textColor} isNumeric>{formatCurrency(row.netPay)}</Td>
                    <Td color={textColorSecondary}>{formatDateTime(row.issuedAt)}</Td>
                    <Td color={textColorSecondary}>{row.issuedByName}</Td>
                    <Td>
                      <Badge colorScheme={isViewed ? "green" : "gray"}>
                        {isViewed ? "Đã xem" : "Chưa xem"}
                      </Badge>
                      {isViewed ? (
                        <Text color={textColorSecondary} fontSize='xs' mt='4px'>
                          {formatDateTime(row.viewedAt)}
                        </Text>
                      ) : null}
                    </Td>
                  </Tr>
                );
              })}
            </Tbody>
          </Table>
        ) : historyRows.length ? (
          <Text color={textColorSecondary}>Không có phiếu lương phù hợp bộ lọc.</Text>
        ) : (
          <Text color={textColorSecondary}>Chưa có phiếu lương nào được phát hành.</Text>
        )}
      </Card>
    </Box>
  );
}
