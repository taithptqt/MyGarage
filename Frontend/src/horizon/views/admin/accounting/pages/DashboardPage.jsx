import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
  Button,
  Flex,
  Input,
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
import Card from "components/card/Card";
import MiniStatistics from "components/card/MiniStatistics";
import {
  exportAccountingDashboardV2Csv,
  getAccountingDashboardV2
} from "../api/accountingApi";
import { formatCurrency, formatDate, formatDateTime, toNumber } from "../utils/accountingHelpers";

function fieldOf(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

function toIsoDate(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

function downloadBlob(filename, blob) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

export default function DashboardPage() {
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");
  const borderColor = useColorModeValue("secondaryGray.200", "whiteAlpha.100");
  const panelBg = useColorModeValue("white", "navy.700");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [fromDate, setFromDate] = useState(toIsoDate(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)));
  const [toDate, setToDate] = useState(toIsoDate(new Date()));
  const [dashboard, setDashboard] = useState(null);
  const [exporting, setExporting] = useState(false);

  async function loadDashboard() {
    setLoading(true);
    setError("");
    try {
      const payload = await getAccountingDashboardV2({
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        historyLimit: 200
      });
      setDashboard(payload || null);
    } catch (err) {
      setError(err?.message || "Không tải được dashboard kế toán.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadDashboard();
  }, []);

  const summary = dashboard?.summary || {};
  const agingBuckets = Array.isArray(dashboard?.agingBuckets) ? dashboard.agingBuckets : [];
  const invoiceWatchlist = Array.isArray(dashboard?.invoiceWatchlist) ? dashboard.invoiceWatchlist : [];
  const apWatchlist = Array.isArray(dashboard?.apWatchlist) ? dashboard.apWatchlist : [];
  const paymentHistory = Array.isArray(dashboard?.paymentHistory) ? dashboard.paymentHistory : [];
  const refundTrail = Array.isArray(dashboard?.refundTrail) ? dashboard.refundTrail : [];
  const approvalTrail = Array.isArray(dashboard?.approvalTrail) ? dashboard.approvalTrail : [];
  const lockPeriodBadge = dashboard?.lockPeriodBadge || {};

  const accountingPeriod = lockPeriodBadge?.accountingPeriod || {};
  const payrollPeriod = lockPeriodBadge?.payrollPeriod || {};

  const warningRows = useMemo(() => {
    const rows = [];
    const overdueArCount = toNumber(summary.overdueArCount);
    const overdueApAmount = toNumber(summary.overdueApAmount);
    const pendingApprovalCount = toNumber(summary.pendingApprovalCount);
    if (overdueArCount > 0) {
      rows.push({
        level: "HIGH",
        title: `${overdueArCount} hóa đơn quá hạn`,
        detail: "Cần ưu tiên xử lý thu hồi công nợ."
      });
    }
    if (overdueApAmount > 0) {
      rows.push({
        level: "MEDIUM",
        title: `AP quá hạn ${formatCurrency(overdueApAmount)}`,
        detail: "Theo dõi chặt để tránh lệch dòng tiền và phạt chậm thanh toán."
      });
    }
    if (pendingApprovalCount > 0) {
      rows.push({
        level: "MEDIUM",
        title: `${pendingApprovalCount} approval đang chờ`,
        detail: "Approval backlog có thể chặn dòng xử lý nghiệp vụ kế toán."
      });
    }
    if (!rows.length) {
      rows.push({
        level: "OK",
        title: "Không có cảnh báo trọng yếu",
        detail: "Snapshot hiện tại ổn định."
      });
    }
    return rows;
  }, [summary]);

  async function handleExportCsv() {
    setExporting(true);
    try {
      const blob = await exportAccountingDashboardV2Csv({
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        historyLimit: 300
      });
      downloadBlob(`dashboard-ke-toan-v2-${toIsoDate(new Date())}.csv`, blob);
    } catch (err) {
      setError(err?.message || "Xuất CSV thất bại.");
    } finally {
      setExporting(false);
    }
  }

  if (loading) {
    return (
      <Flex minH='280px' align='center' justify='center' direction='column' gap='12px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dashboard kế toán v2...</Text>
      </Flex>
    );
  }

  return (
    <Box pt={{ base: "10px", md: "0px" }}>
      <Card p='18px' mb='20px'>
        <Flex justify='space-between' align='center' wrap='wrap' gap='12px'>
          <Box>
            <Text color={textColor} fontSize='xl' fontWeight='800'>Dashboard kế toán (nguồn chuẩn backend)</Text>
            <Text color={textColorSecondary} fontSize='sm'>
              On-screen, report, và CSV export đều lấy từ cùng nguồn dữ liệu `dashboard-v2`.
            </Text>
          </Box>
          <Flex gap='8px' align='center' wrap='wrap'>
            <Input size='sm' type='date' value={fromDate} onChange={(e) => setFromDate(e.target.value)} maxW='170px' />
            <Input size='sm' type='date' value={toDate} onChange={(e) => setToDate(e.target.value)} maxW='170px' />
            <Button size='sm' variant='outline' onClick={loadDashboard}>Làm mới</Button>
            <Button size='sm' colorScheme='blue' onClick={handleExportCsv} isLoading={exporting}>Xuất CSV</Button>
          </Flex>
        </Flex>
        <Flex mt='10px' gap='8px' wrap='wrap'>
          <Badge colorScheme={lockPeriodBadge?.isLocked ? "red" : "green"}>
            {lockPeriodBadge?.badgeText || "OPEN PERIOD"}
          </Badge>
          <Badge colorScheme='blue'>
            Kỳ kế toán: {fieldOf(accountingPeriod, "periodCode", "period_code") || "N/A"}
          </Badge>
          <Badge colorScheme='purple'>
            Payroll period: {fieldOf(payrollPeriod, "periodCode", "period_code") || "N/A"}
          </Badge>
        </Flex>
      </Card>

      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, lg: 3, xl: 6 }} gap='20px' mb='20px'>
        <MiniStatistics name='Doanh thu' value={formatCurrency(summary.revenue)} />
        <MiniStatistics name='Đã thu' value={formatCurrency(summary.collected)} />
        <MiniStatistics name='Công nợ AR' value={formatCurrency(summary.totalOutstandingAr)} />
        <MiniStatistics name='Công nợ AP' value={formatCurrency(summary.totalOutstandingAp)} />
        <MiniStatistics name='Refund + reversal' value={formatCurrency(toNumber(summary.refund) + toNumber(summary.reversal))} />
        <MiniStatistics name='Approval chờ xử lý' value={String(toNumber(summary.pendingApprovalCount))} />
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' mb='12px'>Aging buckets (AR)</Text>
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Bucket</Th>
                <Th isNumeric>Số lượng</Th>
                <Th isNumeric>Giá trị</Th>
              </Tr>
            </Thead>
            <Tbody>
              {agingBuckets.map((bucket) => (
                <Tr key={String(fieldOf(bucket, "bucket", "label"))}>
                  <Td color={textColor}>{fieldOf(bucket, "label", "bucket")}</Td>
                  <Td isNumeric color={textColorSecondary}>{toNumber(fieldOf(bucket, "count"))}</Td>
                  <Td isNumeric color={textColor}>{formatCurrency(fieldOf(bucket, "amount"))}</Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Card>

        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' mb='12px'>Cảnh báo vận hành</Text>
          <SimpleGrid columns={1} gap='10px'>
            {warningRows.map((warning) => (
              <Box key={warning.title} p='10px' border='1px solid' borderColor={borderColor} borderRadius='12px'>
                <Flex justify='space-between' align='center' mb='4px'>
                  <Text color={textColor} fontWeight='700' fontSize='sm'>{warning.title}</Text>
                  <Badge colorScheme={warning.level === "HIGH" ? "red" : warning.level === "MEDIUM" ? "orange" : "green"}>
                    {warning.level}
                  </Badge>
                </Flex>
                <Text color={textColorSecondary} fontSize='xs'>{warning.detail}</Text>
              </Box>
            ))}
          </SimpleGrid>
        </Card>
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' mb='12px'>AR watchlist</Text>
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Invoice</Th>
                <Th>Due date</Th>
                <Th isNumeric>Outstanding</Th>
                <Th isNumeric>DPD</Th>
              </Tr>
            </Thead>
            <Tbody>
              {invoiceWatchlist.map((item) => (
                <Tr key={String(fieldOf(item, "invoiceId", "invoiceCode"))}>
                  <Td color={textColor}>{fieldOf(item, "invoiceCode", "invoiceId") || "--"}</Td>
                  <Td color={textColorSecondary}>{formatDate(fieldOf(item, "dueDate"))}</Td>
                  <Td isNumeric color={textColor}>{formatCurrency(fieldOf(item, "outstandingAmount"))}</Td>
                  <Td isNumeric>
                    <Badge colorScheme={toNumber(fieldOf(item, "daysPastDue")) > 0 ? "red" : "green"}>
                      {toNumber(fieldOf(item, "daysPastDue"))}
                    </Badge>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Card>

        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' mb='12px'>AP watchlist</Text>
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>AP</Th>
                <Th>Due date</Th>
                <Th isNumeric>Outstanding</Th>
                <Th isNumeric>DPD</Th>
              </Tr>
            </Thead>
            <Tbody>
              {apWatchlist.map((item) => (
                <Tr key={String(fieldOf(item, "id"))}>
                  <Td color={textColor}>{fieldOf(item, "supplierInvoiceNo", "id") || "--"}</Td>
                  <Td color={textColorSecondary}>{formatDate(fieldOf(item, "dueDate"))}</Td>
                  <Td isNumeric color={textColor}>{formatCurrency(fieldOf(item, "outstandingAmount"))}</Td>
                  <Td isNumeric>
                    <Badge colorScheme={toNumber(fieldOf(item, "daysPastDue")) > 0 ? "red" : "green"}>
                      {toNumber(fieldOf(item, "daysPastDue"))}
                    </Badge>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Card>
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 3 }} gap='20px'>
        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' mb='12px'>Payment history</Text>
          <Box maxH='300px' overflowY='auto'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Txn</Th>
                  <Th>Type</Th>
                  <Th>Status</Th>
                  <Th isNumeric>Amount</Th>
                </Tr>
              </Thead>
              <Tbody>
                {paymentHistory.slice(0, 30).map((item) => (
                  <Tr key={String(fieldOf(item, "id"))}>
                    <Td color={textColorSecondary}>#{fieldOf(item, "id")}</Td>
                    <Td color={textColor}>{fieldOf(item, "type") || "--"}</Td>
                    <Td><Badge colorScheme='blue'>{fieldOf(item, "status") || "--"}</Badge></Td>
                    <Td isNumeric color={textColor}>{formatCurrency(fieldOf(item, "amount"))}</Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
          </Box>
        </Card>

        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' mb='12px'>Refund trail</Text>
          <Box maxH='300px' overflowY='auto'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>ID</Th>
                  <Th>Type</Th>
                  <Th>Status</Th>
                  <Th isNumeric>Amount</Th>
                </Tr>
              </Thead>
              <Tbody>
                {refundTrail.slice(0, 30).map((item) => (
                  <Tr key={String(fieldOf(item, "id"))}>
                    <Td color={textColorSecondary}>#{fieldOf(item, "id")}</Td>
                    <Td color={textColor}>{fieldOf(item, "type") || "--"}</Td>
                    <Td><Badge colorScheme='purple'>{fieldOf(item, "status") || "--"}</Badge></Td>
                    <Td isNumeric color={textColor}>{formatCurrency(fieldOf(item, "amount"))}</Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
          </Box>
        </Card>

        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' mb='12px'>Approval trail</Text>
          <Box maxH='300px' overflowY='auto'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>ID</Th>
                  <Th>Request</Th>
                  <Th>Status</Th>
                  <Th>Updated</Th>
                </Tr>
              </Thead>
              <Tbody>
                {approvalTrail.slice(0, 30).map((item) => (
                  <Tr key={String(fieldOf(item, "id"))}>
                    <Td color={textColorSecondary}>#{fieldOf(item, "id")}</Td>
                    <Td color={textColor}>{fieldOf(item, "requestType") || "--"}</Td>
                    <Td>
                      <Badge colorScheme={String(fieldOf(item, "status") || "").toUpperCase() === "PENDING" ? "orange" : "green"}>
                        {fieldOf(item, "status") || "--"}
                      </Badge>
                    </Td>
                    <Td color={textColorSecondary}>{formatDateTime(fieldOf(item, "updatedAt"))}</Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
          </Box>
        </Card>
      </SimpleGrid>
    </Box>
  );
}
