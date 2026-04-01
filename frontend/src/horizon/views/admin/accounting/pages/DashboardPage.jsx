import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
  Flex,
  Icon,
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
  useColorModeValue
} from "@chakra-ui/react";
import MiniStatistics from "components/card/MiniStatistics";
import Card from "components/card/Card";
import IconBox from "components/icons/IconBox";
import { useBranch } from "../../../../../context/BranchContext";
import {
  MdAccountBalanceWallet,
  MdAttachMoney,
  MdBarChart,
  MdCalendarToday,
  MdErrorOutline,
  MdReceiptLong,
  MdTrendingDown,
  MdTrendingUp,
  MdWarning
} from "react-icons/md";
import {
  getAccountsPayable,
  getAccountingPeriods,
  getBranches,
  getFinanceApprovalRequests,
  getInvoices,
  getPayments,
  getPayrollRuns,
  getRevenueSnapshots
} from "../api/accountingApi";
import {
  countCompletedInflowWithoutInvoice,
  formatCurrency,
  formatDate,
  sumCompletedCollectedPayments,
  sumCompletedPaymentByTypes,
  sumBy,
  toMonthKey,
  toNumber
} from "../utils/accountingHelpers";

function fieldOf(item, ...candidates) {
  for (const key of candidates) {
    if (item && item[key] !== undefined && item[key] !== null) {
      return item[key];
    }
  }
  return null;
}

function snapshotDate(item) {
  return fieldOf(item, "snapshotDate", "SnapshotDate", "createdAt", "CreatedAt");
}

function toTs(value) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 0 : date.getTime();
}

export default function DashboardPage() {
  const { selectedBranchId, isBranchLocked } = useBranch();
  const brandColor = useColorModeValue("brand.500", "white");
  const boxBg = useColorModeValue("secondaryGray.300", "whiteAlpha.100");
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");
  const borderColor = useColorModeValue("secondaryGray.200", "whiteAlpha.100");
  const enforcedBranchId = isBranchLocked && selectedBranchId ? String(selectedBranchId) : "";

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [branchFilter, setBranchFilter] = useState("all");
  const [periodMode, setPeriodMode] = useState("month");
  const [selectedMonth, setSelectedMonth] = useState("");

  const [branches, setBranches] = useState([]);
  const [snapshots, setSnapshots] = useState([]);
  const [invoices, setInvoices] = useState([]);
  const [payments, setPayments] = useState([]);
  const [approvals, setApprovals] = useState([]);
  const [accountsPayable, setAccountsPayable] = useState([]);
  const [accountingPeriods, setAccountingPeriods] = useState([]);
  const [payrollRuns, setPayrollRuns] = useState([]);

  useEffect(() => {
    if (!enforcedBranchId) return;
    setBranchFilter((prev) => (prev === enforcedBranchId ? prev : enforcedBranchId));
  }, [enforcedBranchId]);

  useEffect(() => {
    let mounted = true;
    async function load() {
      setLoading(true);
      setLoadError("");
      try {
        const [
          branchData,
          snapshotData,
          invoiceData,
          paymentData,
          approvalData,
          apData,
          periodData,
          payrollData
        ] = await Promise.all([
          getBranches(),
          getRevenueSnapshots(),
          getInvoices(),
          getPayments(),
          getFinanceApprovalRequests(),
          getAccountsPayable(),
          getAccountingPeriods(),
          getPayrollRuns()
        ]);

        if (!mounted) return;
        setBranches(Array.isArray(branchData) ? branchData : []);
        setSnapshots(Array.isArray(snapshotData) ? snapshotData : []);
        setInvoices(Array.isArray(invoiceData) ? invoiceData : []);
        setPayments(Array.isArray(paymentData) ? paymentData : []);
        setApprovals(approvalData?.items || []);
        setAccountsPayable(apData?.items || []);
        setAccountingPeriods(periodData?.items || []);
        setPayrollRuns(payrollData?.items || []);

        const monthKeys = [...new Set((snapshotData || []).map((item) => toMonthKey(snapshotDate(item))).filter(Boolean))]
          .sort((a, b) => b.localeCompare(a));
        setSelectedMonth(monthKeys[0] || "");
      } catch (error) {
        if (!mounted) return;
        setLoadError(error?.message || "Không tải được dữ liệu dashboard kế toán.");
      } finally {
        if (mounted) setLoading(false);
      }
    }
    load();
    return () => {
      mounted = false;
    };
  }, []);

  const scopedBranches = useMemo(() => {
    if (!enforcedBranchId) return branches || [];
    return (branches || []).filter((item) => String(fieldOf(item, "id", "Id")) === enforcedBranchId);
  }, [branches, enforcedBranchId]);

  const branchOptions = useMemo(
    () =>
      scopedBranches.map((item) => ({
        id: String(fieldOf(item, "id", "Id")),
        name: fieldOf(item, "name", "Name") || `Branch #${fieldOf(item, "id", "Id")}`
      })),
    [scopedBranches]
  );

  const monthOptions = useMemo(
    () =>
      [...new Set((snapshots || []).map((item) => toMonthKey(snapshotDate(item))).filter(Boolean))]
        .sort((a, b) => b.localeCompare(a)),
    [snapshots]
  );

  const effectiveBranchFilter = enforcedBranchId || branchFilter;

  const filteredSnapshots = useMemo(() => {
    const byBranch = (snapshots || []).filter((item) => {
      if (effectiveBranchFilter === "all") return true;
      return String(fieldOf(item, "branchId", "BranchId")) === String(effectiveBranchFilter);
    });

    if (!byBranch.length) return [];

    if (periodMode === "day") {
      const latestTs = Math.max(...byBranch.map((item) => toTs(snapshotDate(item))));
      return byBranch.filter((item) => toTs(snapshotDate(item)) === latestTs);
    }

    if (!selectedMonth) return byBranch;
    return byBranch.filter((item) => toMonthKey(snapshotDate(item)) === selectedMonth);
  }, [snapshots, effectiveBranchFilter, periodMode, selectedMonth]);

  const paymentStats = useMemo(() => {
    const normalized = (payments || []).filter((item) => {
      if (effectiveBranchFilter === "all") return true;
      const paymentBranchId = fieldOf(item, "branchId", "BranchId");
      if (paymentBranchId === null || paymentBranchId === undefined) return false;
      return String(paymentBranchId) === String(effectiveBranchFilter);
    });
    const totalCollected = sumCompletedCollectedPayments(normalized);
    const inflowNoInvoice = countCompletedInflowWithoutInvoice(normalized);

    const duplicates = normalized.reduce((acc, item) => {
      const code = String(fieldOf(item, "transactionCode", "TransactionCode") || "").trim();
      if (!code) return acc;
      acc[code] = (acc[code] || 0) + 1;
      return acc;
    }, {});

    const duplicateCount = Object.values(duplicates).filter((count) => count > 1).length;
    const refunds = sumCompletedPaymentByTypes(normalized, ["REFUND"]);
    const reversals = sumCompletedPaymentByTypes(normalized, ["REVERSAL"]);

    return {
      totalCollected,
      inflowNoInvoice,
      duplicateCount,
      refunds,
      reversals
    };
  }, [payments, effectiveBranchFilter]);

  const financeSnapshot = useMemo(() => {
    const totalRevenue = sumBy(filteredSnapshots, (item) => fieldOf(item, "totalRevenue", "TotalRevenue"));
    const totalCollected = sumBy(filteredSnapshots, (item) => fieldOf(item, "totalCollected", "TotalCollected"));
    const totalDebt = sumBy(filteredSnapshots, (item) => fieldOf(item, "totalDebt", "TotalDebt"));
    const totalExpense = sumBy(filteredSnapshots, (item) => fieldOf(item, "totalExpense", "TotalExpense"));
    const netProfit = sumBy(filteredSnapshots, (item) => fieldOf(item, "netProfit", "NetProfit"));
    const cashFlow = sumBy(filteredSnapshots, (item) => fieldOf(item, "netCashFlow", "NetCashFlow"));

    const grossMargin = totalRevenue > 0 ? ((totalRevenue - totalExpense) / totalRevenue) * 100 : 0;
    const netMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;

    return {
      totalRevenue,
      totalCollected,
      totalDebt,
      totalExpense,
      netProfit,
      cashFlow,
      grossMargin,
      netMargin
    };
  }, [filteredSnapshots]);

  const displayedCollected = useMemo(() => {
    if (filteredSnapshots.length) return financeSnapshot.totalCollected;
    return paymentStats.totalCollected;
  }, [filteredSnapshots.length, financeSnapshot.totalCollected, paymentStats.totalCollected]);

  const approvalsPending = useMemo(
    () =>
      (approvals || []).filter((item) => {
        const status = String(fieldOf(item, "status", "Status") || "").toUpperCase();
        return status === "PENDING" || status === "WAITING";
      }),
    [approvals]
  );

  const apDueSummary = useMemo(() => {
    const now = new Date();
    const rows = (accountsPayable || []).map((item) => {
      const dueDateValue = fieldOf(item, "dueDate", "DueDate");
      const dueDate = dueDateValue ? new Date(dueDateValue) : null;
      const outstanding = toNumber(fieldOf(item, "outstandingAmount", "OutstandingAmount", "outstanding", "Outstanding"));
      const overdue = dueDate ? dueDate.getTime() < now.getTime() && outstanding > 0 : false;
      const dueSoon = dueDate
        ? dueDate.getTime() >= now.getTime() && dueDate.getTime() - now.getTime() <= 7 * 24 * 60 * 60 * 1000
        : false;
      return { ...item, overdue, dueSoon, outstanding };
    });

    return {
      totalOutstanding: sumBy(rows, (item) => item.outstanding),
      overdueAmount: sumBy(rows.filter((item) => item.overdue), (item) => item.outstanding),
      dueSoonAmount: sumBy(rows.filter((item) => item.dueSoon), (item) => item.outstanding),
      rows: rows.slice(0, 5)
    };
  }, [accountsPayable]);

  const openPeriodLabel = useMemo(() => {
    const openPeriod = (accountingPeriods || []).find((item) => {
      const status = String(fieldOf(item, "status", "Status") || "").toUpperCase();
      return status.includes("OPEN");
    });
    if (!openPeriod) return "Chưa tích hợp";

    const periodCode = fieldOf(openPeriod, "periodCode", "PeriodCode", "name", "Name");
    const fromDate = fieldOf(openPeriod, "startDate", "StartDate");
    const toDate = fieldOf(openPeriod, "endDate", "EndDate");
    if (periodCode) return String(periodCode);
    return `${formatDate(fromDate)} - ${formatDate(toDate)}`;
  }, [accountingPeriods]);

  const currentPayrollLabel = useMemo(() => {
    if (!payrollRuns.length) return "Chưa tích hợp";
    const sorted = [...payrollRuns].sort(
      (a, b) => toTs(fieldOf(b, "createdAt", "CreatedAt", "runDate", "RunDate")) - toTs(fieldOf(a, "createdAt", "CreatedAt", "runDate", "RunDate"))
    );
    const latest = sorted[0];
    const code = fieldOf(latest, "runCode", "RunCode", "name", "Name") || `Run #${fieldOf(latest, "id", "Id")}`;
    const status = String(fieldOf(latest, "status", "Status") || "PENDING");
    return `${code} (${status})`;
  }, [payrollRuns]);

  const warningItems = useMemo(() => {
    const items = [];
    if (paymentStats.duplicateCount > 0) {
      items.push({
        level: "high",
        title: `${paymentStats.duplicateCount} mã giao dịch bị trùng`,
        detail: "Kiểm tra duplicate payment để tránh ghi nhận doanh thu trùng."
      });
    }
    if (paymentStats.inflowNoInvoice > 0) {
      items.push({
        level: "medium",
        title: `${paymentStats.inflowNoInvoice} payment inflow chưa gắn invoice`,
        detail: "Có nguy cơ vi phạm CHK_Payments_InflowRequiresInvoice."
      });
    }
    if (financeSnapshot.netProfit < 0) {
      items.push({
        level: "high",
        title: "Net profit đang âm",
        detail: "Cần rà soát tỷ trọng chi phí và tỷ lệ refund/reversal."
      });
    }
    if (!items.length) {
      items.push({
        level: "low",
        title: "Không phát hiện cảnh báo nghiêm trọng",
        detail: "Dữ liệu hiện tại đang ổn định."
      });
    }
    return items;
  }, [paymentStats, financeSnapshot]);

  const topPendingApprovals = approvalsPending.slice(0, 4);

  if (loading) {
    return (
      <Flex pt='20px' align='center' justify='center' minH='280px' direction='column' gap='12px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dữ liệu dashboard kế toán...</Text>
      </Flex>
    );
  }

  return (
    <Box pt={{ base: "10px", md: "0px", xl: "0px" }}>
      <Flex
        mb='20px'
        gap='12px'
        wrap='wrap'
        align={{ base: "stretch", lg: "center" }}
        justify='space-between'>
        <Box>
          <Text color={textColor} fontSize='2xl' fontWeight='700'>
            Dashboard kế toán
          </Text>
          <Text color={textColorSecondary} fontSize='sm'>
            Theo dõi doanh thu, công nợ, approval và cảnh báo lệch số liệu theo thời gian thực.
          </Text>
        </Box>
        <Flex gap='12px' wrap='wrap'>
          <Select value={periodMode} onChange={(e) => setPeriodMode(e.target.value)} w='150px'>
            <option value='day'>Theo ngày</option>
            <option value='month'>Theo tháng</option>
          </Select>
          <Select
            value={selectedMonth}
            onChange={(e) => setSelectedMonth(e.target.value)}
            isDisabled={periodMode !== "month"}
            w='150px'>
            {monthOptions.length ? (
              monthOptions.map((monthKey) => (
                <option value={monthKey} key={monthKey}>
                  {monthKey}
                </option>
              ))
            ) : (
              <option value=''>Không có dữ liệu</option>
            )}
          </Select>
          <Select
            value={enforcedBranchId || branchFilter}
            onChange={(e) => setBranchFilter(e.target.value)}
            isDisabled={Boolean(enforcedBranchId)}
            w='180px'>
            {!enforcedBranchId ? <option value='all'>Tất cả chi nhánh</option> : null}
            {branchOptions.map((branch) => (
              <option value={branch.id} key={branch.id}>
                {branch.name}
              </option>
            ))}
          </Select>
        </Flex>
      </Flex>

      {loadError ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>
            {loadError}
          </Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, lg: 3, "2xl": 6 }} gap='20px' mb='20px'>
        <MiniStatistics
          startContent={
            <IconBox
              w='56px'
              h='56px'
              bg={boxBg}
              icon={<Icon w='30px' h='30px' as={MdBarChart} color={brandColor} />}
            />
          }
          name='Doanh thu'
          value={formatCurrency(financeSnapshot.totalRevenue)}
        />
        <MiniStatistics
          startContent={
            <IconBox
              w='56px'
              h='56px'
              bg={boxBg}
              icon={<Icon w='30px' h='30px' as={MdAttachMoney} color={brandColor} />}
            />
          }
          name='Đã thu'
          value={formatCurrency(displayedCollected)}
        />
        <MiniStatistics
          startContent={
            <IconBox
              w='56px'
              h='56px'
              bg={boxBg}
              icon={<Icon w='30px' h='30px' as={MdReceiptLong} color={brandColor} />}
            />
          }
          name='Chưa thu đủ'
          value={formatCurrency(financeSnapshot.totalDebt)}
        />
        <MiniStatistics
          startContent={
            <IconBox
              w='56px'
              h='56px'
              bg={boxBg}
              icon={<Icon w='30px' h='30px' as={MdWarning} color={brandColor} />}
            />
          }
          name='AP đến hạn/quá hạn'
          value={formatCurrency(apDueSummary.overdueAmount + apDueSummary.dueSoonAmount)}
        />
        <MiniStatistics
          startContent={
            <IconBox
              w='56px'
              h='56px'
              bg='linear-gradient(90deg, #4481EB 0%, #04BEFE 100%)'
              icon={<Icon w='28px' h='28px' as={MdCalendarToday} color='white' />}
            />
          }
          name='Kỳ kế toán mở'
          value={openPeriodLabel}
        />
        <MiniStatistics
          startContent={
            <IconBox
              w='56px'
              h='56px'
              bg={boxBg}
              icon={<Icon w='30px' h='30px' as={MdAccountBalanceWallet} color={brandColor} />}
            />
          }
          name='Payroll kỳ hiện tại'
          value={currentPayrollLabel}
        />
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' fontSize='lg' mb='14px'>
            Snapshot tài chính
          </Text>
          <SimpleGrid columns={{ base: 1, md: 2 }} gap='12px'>
            <Flex p='12px' border='1px solid' borderColor={borderColor} borderRadius='14px' align='center' gap='10px'>
              <Icon as={MdTrendingUp} color='green.400' w='22px' h='22px' />
              <Box>
                <Text fontSize='xs' color={textColorSecondary}>Gross margin</Text>
                <Text color={textColor} fontWeight='700'>{financeSnapshot.grossMargin.toFixed(2)}%</Text>
              </Box>
            </Flex>
            <Flex p='12px' border='1px solid' borderColor={borderColor} borderRadius='14px' align='center' gap='10px'>
              <Icon as={financeSnapshot.netProfit >= 0 ? MdTrendingUp : MdTrendingDown} color={financeSnapshot.netProfit >= 0 ? "green.400" : "red.400"} w='22px' h='22px' />
              <Box>
                <Text fontSize='xs' color={textColorSecondary}>Net profit</Text>
                <Text color={textColor} fontWeight='700'>{formatCurrency(financeSnapshot.netProfit)}</Text>
              </Box>
            </Flex>
            <Flex p='12px' border='1px solid' borderColor={borderColor} borderRadius='14px' align='center' gap='10px'>
              <Icon as={MdAttachMoney} color='teal.400' w='22px' h='22px' />
              <Box>
                <Text fontSize='xs' color={textColorSecondary}>Cash flow</Text>
                <Text color={textColor} fontWeight='700'>{formatCurrency(financeSnapshot.cashFlow)}</Text>
              </Box>
            </Flex>
            <Flex p='12px' border='1px solid' borderColor={borderColor} borderRadius='14px' align='center' gap='10px'>
              <Icon as={MdErrorOutline} color='orange.400' w='22px' h='22px' />
              <Box>
                <Text fontSize='xs' color={textColorSecondary}>Refund + reversal</Text>
                <Text color={textColor} fontWeight='700'>
                  {formatCurrency(paymentStats.refunds + paymentStats.reversals)}
                </Text>
              </Box>
            </Flex>
          </SimpleGrid>
        </Card>

        <Card p='20px'>
          <Flex justify='space-between' align='center' mb='14px'>
            <Text color={textColor} fontWeight='700' fontSize='lg'>
              Approval tài chính chờ xử lý
            </Text>
            <Badge colorScheme='red'>{approvalsPending.length} pending</Badge>
          </Flex>
          {topPendingApprovals.length ? (
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Loại</Th>
                  <Th>Lý do</Th>
                  <Th>Trạng thái</Th>
                </Tr>
              </Thead>
              <Tbody>
                {topPendingApprovals.map((item) => (
                  <Tr key={String(fieldOf(item, "id", "Id"))}>
                    <Td color={textColor} fontWeight='600'>
                      {fieldOf(item, "requestType", "RequestType") || "N/A"}
                    </Td>
                    <Td color={textColorSecondary}>{fieldOf(item, "reason", "Reason") || "--"}</Td>
                    <Td>
                      <Badge colorScheme='orange'>{fieldOf(item, "status", "Status") || "PENDING"}</Badge>
                    </Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
          ) : (
            <Text color={textColorSecondary}>Không có yêu cầu chờ duyệt.</Text>
          )}
        </Card>
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' fontSize='lg' mb='14px'>
            AP đến hạn / quá hạn
          </Text>
          {apDueSummary.rows.length ? (
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Supplier/AP</Th>
                  <Th>Due date</Th>
                  <Th isNumeric>Outstanding</Th>
                  <Th>Trạng thái</Th>
                </Tr>
              </Thead>
              <Tbody>
                {apDueSummary.rows.map((item, index) => {
                  const dueDate = fieldOf(item, "dueDate", "DueDate");
                  const title =
                    fieldOf(item, "supplierName", "SupplierName", "apCode", "ApCode", "name", "Name") || `AP #${index + 1}`;
                  return (
                    <Tr key={`${title}-${index}`}>
                      <Td color={textColor} fontWeight='600'>{title}</Td>
                      <Td color={textColorSecondary}>{formatDate(dueDate)}</Td>
                      <Td color={textColor} isNumeric>{formatCurrency(item.outstanding)}</Td>
                      <Td>
                        <Badge colorScheme={item.overdue ? "red" : item.dueSoon ? "orange" : "green"}>
                          {item.overdue ? "Quá hạn" : item.dueSoon ? "Sắp đến hạn" : "Ổn định"}
                        </Badge>
                      </Td>
                    </Tr>
                  );
                })}
              </Tbody>
            </Table>
          ) : (
            <Text color={textColorSecondary}>
              Chưa có dữ liệu `accounts_payable` từ API. Trang AP vẫn đã sẵn sàng để gắn khi backend mở endpoint.
            </Text>
          )}
        </Card>

        <Card p='20px'>
          <Text color={textColor} fontWeight='700' fontSize='lg' mb='14px'>
            Cảnh báo thất thoát / lệch số liệu
          </Text>
          <Flex direction='column' gap='12px'>
            {warningItems.map((item) => (
              <Flex key={item.title} align='flex-start' gap='10px'>
                <Box
                  mt='6px'
                  w='10px'
                  h='10px'
                  borderRadius='full'
                  bg={item.level === "high" ? "red.400" : item.level === "medium" ? "orange.400" : "green.400"}
                />
                <Box>
                  <Text color={textColor} fontWeight='600' fontSize='sm'>{item.title}</Text>
                  <Text color={textColorSecondary} fontSize='xs'>{item.detail}</Text>
                </Box>
              </Flex>
            ))}
          </Flex>
        </Card>
      </SimpleGrid>
    </Box>
  );
}
