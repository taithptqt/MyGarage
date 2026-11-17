import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
  Button,
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
  useColorModeValue
} from "@chakra-ui/react";
import Card from "components/card/Card";
import MiniStatistics from "components/card/MiniStatistics";
import TablePagination from "@shared/components/TablePagination";
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
  const [historyLimit, setHistoryLimit] = useState(30);
  const [dashboard, setDashboard] = useState(null);
  const [exporting, setExporting] = useState(false);

  const [paymentPage, setPaymentPage] = useState(1);
  const [paymentPageSize, setPaymentPageSize] = useState(10);
  const [refundPage, setRefundPage] = useState(1);
  const [refundPageSize, setRefundPageSize] = useState(10);
  const [approvalPage, setApprovalPage] = useState(1);
  const [approvalPageSize, setApprovalPageSize] = useState(10);

  async function loadDashboard() {
    setLoading(true);
    setError("");
    try {
      const payload = await getAccountingDashboardV2({
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        historyLimit
      });
      setDashboard(payload || null);
      setPaymentPage(1);
      setRefundPage(1);
      setApprovalPage(1);
    } catch (err) {
      setError(err?.message || "Không tải được dashboard kế toán.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadDashboard();
  }, [historyLimit]);

  const summary = dashboard?.summary || {};
  const detailsAvailable = dashboard?.detailsAvailable === true;
  const payablesAvailable = dashboard?.payablesAvailable === true;
  const receivablesAvailable = dashboard?.receivablesAvailable === true;
  const paymentHistoryAvailable = dashboard?.paymentHistoryAvailable === true;
  const refundTrailAvailable = dashboard?.refundTrailAvailable === true;
  const approvalTrailAvailable = dashboard?.approvalTrailAvailable === true;
  const unavailableText = "Chưa có dữ liệu";
  const agingBuckets = Array.isArray(dashboard?.agingBuckets) ? dashboard.agingBuckets : [];
  const invoiceWatchlist = Array.isArray(dashboard?.invoiceWatchlist) ? dashboard.invoiceWatchlist : [];
  const apWatchlist = Array.isArray(dashboard?.apWatchlist) ? dashboard.apWatchlist : [];
  const paymentHistory = Array.isArray(dashboard?.paymentHistory) ? dashboard.paymentHistory : [];
  const refundTrail = Array.isArray(dashboard?.refundTrail) ? dashboard.refundTrail : [];
  const approvalTrail = Array.isArray(dashboard?.approvalTrail) ? dashboard.approvalTrail : [];

  const pagedPaymentHistory = useMemo(() => {
    if (!paymentHistory.length) return [];
    const start = (paymentPage - 1) * paymentPageSize;
    const end = Math.min(start + paymentPageSize, paymentHistory.length);
    const rows = [];
    for (let i = start; i < end; i++) rows.push(paymentHistory[i]);
    return rows;
  }, [paymentHistory, paymentPage, paymentPageSize]);

  const pagedRefundTrail = useMemo(() => {
    if (!refundTrail.length) return [];
    const start = (refundPage - 1) * refundPageSize;
    const end = Math.min(start + refundPageSize, refundTrail.length);
    const rows = [];
    for (let i = start; i < end; i++) rows.push(refundTrail[i]);
    return rows;
  }, [refundTrail, refundPage, refundPageSize]);

  const pagedApprovalTrail = useMemo(() => {
    if (!approvalTrail.length) return [];
    const start = (approvalPage - 1) * approvalPageSize;
    const end = Math.min(start + approvalPageSize, approvalTrail.length);
    const rows = [];
    for (let i = start; i < end; i++) rows.push(approvalTrail[i]);
    return rows;
  }, [approvalTrail, approvalPage, approvalPageSize]);

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
    if (!detailsAvailable) {
      rows.push({
        level: "MEDIUM",
        title: "Chưa đủ dữ liệu cảnh báo",
        detail: approvalTrailAvailable ? "Chưa có ngày đến hạn để tính công nợ quá hạn." : "Chưa có ngày đến hạn và dữ liệu phê duyệt theo phạm vi hiện tại."
      });
    } else if (!rows.length) {
      rows.push({
        level: "OK",
        title: "Không có cảnh báo trọng yếu",
        detail: "Snapshot hiện tại ổn định."
      });
    }
    return rows;
  }, [summary, detailsAvailable, approvalTrailAvailable]);

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
            <Select size='sm' value={historyLimit} onChange={(e) => setHistoryLimit(Number(e.target.value))} maxW='130px' aria-label='Giới hạn bản ghi'>
              <option value={10}>10 bản ghi</option>
              <option value={20}>20 bản ghi</option>
              <option value={30}>30 bản ghi</option>
              <option value={50}>50 bản ghi</option>
              <option value={100}>100 bản ghi</option>
            </Select>
            <Button size='sm' variant='outline' onClick={loadDashboard}>Làm mới</Button>
            <Button size='sm' colorScheme='blue' onClick={handleExportCsv} isLoading={exporting}>Xuất CSV</Button>
          </Flex>
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
        <MiniStatistics name='Công nợ AR' value={summary.totalOutstandingAr == null ? unavailableText : formatCurrency(summary.totalOutstandingAr)} />
        <MiniStatistics name='Công nợ AP' value={summary.totalOutstandingAp == null ? unavailableText : formatCurrency(summary.totalOutstandingAp)} />
        <MiniStatistics name='Refund + reversal' value={formatCurrency(toNumber(summary.refund) + toNumber(summary.reversal))} />
        <MiniStatistics name='Approval chờ xử lý' value={summary.pendingApprovalCount == null ? unavailableText : String(toNumber(summary.pendingApprovalCount))} />
      </SimpleGrid>

      {!detailsAvailable ? <Text color={textColorSecondary} mb="20px">Một số hóa đơn chưa có hạn thanh toán; số lượng quá hạn chưa thể xác định đầy đủ.</Text> : null}

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
          {!receivablesAvailable ? <Text color={textColorSecondary}>Chưa có nguồn dữ liệu công nợ khách hàng.</Text> : null}
          {receivablesAvailable && invoiceWatchlist.length === 0 ? <Text color={textColorSecondary}>Không có hóa đơn khách hàng còn nợ.</Text> : null}
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
                    {fieldOf(item, "daysPastDue") == null ? "--" : (
                      <Badge colorScheme={toNumber(fieldOf(item, "daysPastDue")) > 0 ? "red" : "green"}>
                        {toNumber(fieldOf(item, "daysPastDue"))}
                      </Badge>
                    )}
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Card>

        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' mb='12px'>AP watchlist</Text>
          {!payablesAvailable ? <Text color={textColorSecondary}>Chưa có nguồn dữ liệu công nợ nhà cung cấp.</Text> : null}
          {payablesAvailable && apWatchlist.length === 0 ? <Text color={textColorSecondary}>Không có công nợ nhà cung cấp đang mở.</Text> : null}
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
          <Text color={textColor} fontWeight='700' mb='12px'>Lịch sử thanh toán</Text>
          {!paymentHistoryAvailable ? <Text color={textColorSecondary}>Chưa có nguồn dữ liệu lịch sử thanh toán.</Text> : null}
          {paymentHistoryAvailable && paymentHistory.length === 0 ? <Text color={textColorSecondary}>Không có thanh toán trong khoảng thời gian đã chọn.</Text> : null}
          <Box maxH='300px' overflowY='auto'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Txn</Th>
                  <Th>Loại</Th>
                  <Th>Trạng thái</Th>
                  <Th isNumeric>Số tiền</Th>
                </Tr>
              </Thead>
              <Tbody>
                {pagedPaymentHistory.map((item) => (
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
          <TablePagination
            totalItems={paymentHistory.length}
            currentPage={paymentPage}
            pageSize={paymentPageSize}
            onPageChange={setPaymentPage}
            onPageSizeChange={(size) => {
              setPaymentPageSize(size);
              setPaymentPage(1);
            }}
          />
        </Card>

        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' mb='12px'>Truy vết hoàn tiền</Text>
          {!refundTrailAvailable ? <Text color={textColorSecondary}>Chưa có nguồn dữ liệu hoàn tiền.</Text> : null}
          {refundTrailAvailable && refundTrail.length === 0 ? <Text color={textColorSecondary}>Không có hoàn tiền trong khoảng thời gian đã chọn.</Text> : null}
          <Box maxH='300px' overflowY='auto'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>ID</Th>
                  <Th>Loại</Th>
                  <Th>Trạng thái</Th>
                  <Th isNumeric>Số tiền</Th>
                </Tr>
              </Thead>
              <Tbody>
                {pagedRefundTrail.map((item) => (
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
          <TablePagination
            totalItems={refundTrail.length}
            currentPage={refundPage}
            pageSize={refundPageSize}
            onPageChange={setRefundPage}
            onPageSizeChange={(size) => {
              setRefundPageSize(size);
              setRefundPage(1);
            }}
          />
        </Card>

        <Card p='20px' bg={panelBg}>
          <Text color={textColor} fontWeight='700' mb='12px'>Truy vết phê duyệt</Text>
          {!approvalTrailAvailable ? <Text color={textColorSecondary}>Chưa có dữ liệu phê duyệt theo phạm vi hiện tại.</Text> : null}
          {approvalTrailAvailable && approvalTrail.length === 0 ? <Text color={textColorSecondary}>Không có phê duyệt trong khoảng thời gian đã chọn.</Text> : null}
          <Box maxH='300px' overflowY='auto'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>ID</Th>
                  <Th>Yêu cầu</Th>
                  <Th>Trạng thái</Th>
                  <Th>Cập nhật</Th>
                </Tr>
              </Thead>
              <Tbody>
                {pagedApprovalTrail.map((item) => (
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
          <TablePagination
            totalItems={approvalTrail.length}
            currentPage={approvalPage}
            pageSize={approvalPageSize}
            onPageChange={setApprovalPage}
            onPageSizeChange={(size) => {
              setApprovalPageSize(size);
              setApprovalPage(1);
            }}
          />
        </Card>
      </SimpleGrid>
    </Box>
  );
}
