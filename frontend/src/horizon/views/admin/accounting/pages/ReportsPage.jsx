import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
  Flex,
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
  getInvoices,
  getPayments,
  getPayrollRunSummaryReport,
  getRevenueSnapshots,
  getSupplierPayableSummaryReport,
  getTrialBalanceReport
} from "../api/accountingApi";
import {
  formatCurrency,
  sumCompletedCollectedPayments,
  sumCompletedPaymentByTypes,
  sumBy
} from "../utils/accountingHelpers";

function fieldOf(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

export default function ReportsPage() {
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [trialBalance, setTrialBalance] = useState({ available: false, items: [] });
  const [supplierPayableSummary, setSupplierPayableSummary] = useState({ available: false, items: [] });
  const [payrollRunSummary, setPayrollRunSummary] = useState({ available: false, items: [] });
  const [snapshots, setSnapshots] = useState([]);
  const [payments, setPayments] = useState([]);
  const [invoices, setInvoices] = useState([]);

  useEffect(() => {
    let mounted = true;
    async function load() {
      setLoading(true);
      setError("");
      try {
        const [trialData, supplierData, payrollData, snapshotData, paymentData, invoiceData] = await Promise.all([
          getTrialBalanceReport(),
          getSupplierPayableSummaryReport(),
          getPayrollRunSummaryReport(),
          getRevenueSnapshots(),
          getPayments(),
          getInvoices()
        ]);
        if (!mounted) return;
        setTrialBalance(trialData);
        setSupplierPayableSummary(supplierData);
        setPayrollRunSummary(payrollData);
        setSnapshots(snapshotData || []);
        setPayments(paymentData || []);
        setInvoices(invoiceData || []);
      } catch (err) {
        if (!mounted) return;
        setError(err?.message || "Không tải được dữ liệu báo cáo.");
      } finally {
        if (mounted) setLoading(false);
      }
    }
    load();
    return () => {
      mounted = false;
    };
  }, []);

  const computedMetrics = useMemo(() => {
    const revenue = sumBy(snapshots || [], (item) => fieldOf(item, "totalRevenue", "TotalRevenue"));
    const netProfit = sumBy(snapshots || [], (item) => fieldOf(item, "netProfit", "NetProfit"));
    const cashFlow = sumBy(snapshots || [], (item) => fieldOf(item, "netCashFlow", "NetCashFlow"));
    const collected = sumCompletedCollectedPayments(payments || []);

    const refundTotal = sumCompletedPaymentByTypes(payments || [], ["REFUND"]);
    const reversalTotal = sumCompletedPaymentByTypes(payments || [], ["REVERSAL"]);

    const totalInvoiceAmount = sumBy(invoices || [], (item) => fieldOf(item, "totalAmount", "TotalAmount"));
    const totalInvoiceTax = sumBy(invoices || [], (item) => fieldOf(item, "taxAmount", "TaxAmount"));
    const totalInvoicePaid = sumBy(invoices || [], (item) => fieldOf(item, "paidAmount", "PaidAmount"));

    const refundRatio = revenue > 0 ? (refundTotal / revenue) * 100 : 0;
    const reversalRatio = revenue > 0 ? (reversalTotal / revenue) * 100 : 0;
    const collectionRatio = totalInvoiceAmount > 0 ? (totalInvoicePaid / totalInvoiceAmount) * 100 : 0;
    const grossMargin = revenue > 0
      ? ((revenue - sumBy(snapshots || [], (item) => fieldOf(item, "totalExpense", "TotalExpense"))) / revenue) * 100
      : 0;

    return {
      revenue,
      netProfit,
      cashFlow,
      collected,
      refundRatio,
      reversalRatio,
      collectionRatio,
      grossMargin,
      totalInvoiceTax
    };
  }, [snapshots, payments, invoices]);

  const kpiRows = [
    { metric: "Doanh thu", value: formatCurrency(computedMetrics.revenue) },
    { metric: "Đã thu (completed)", value: formatCurrency(computedMetrics.collected) },
    { metric: "Net profit", value: formatCurrency(computedMetrics.netProfit) },
    { metric: "Cash flow snapshot", value: formatCurrency(computedMetrics.cashFlow) },
    { metric: "Gross margin", value: `${computedMetrics.grossMargin.toFixed(2)}%` },
    { metric: "Collection ratio", value: `${computedMetrics.collectionRatio.toFixed(2)}%` },
    { metric: "Refund ratio", value: `${computedMetrics.refundRatio.toFixed(2)}%` },
    { metric: "Reversal ratio", value: `${computedMetrics.reversalRatio.toFixed(2)}%` },
    { metric: "VAT đầu ra", value: formatCurrency(computedMetrics.totalInvoiceTax) }
  ];

  if (loading) {
    return (
      <Flex minH='260px' align='center' justify='center' direction='column' gap='10px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dữ liệu báo cáo quản trị...</Text>
      </Flex>
    );
  }

  return (
    <Box>
      <Box mb='20px'>
        <Text color={textColor} fontSize='2xl' fontWeight='700'>Báo cáo quản trị</Text>
        <Text color={textColorSecondary} fontSize='sm'>
          Tổng hợp trial balance, AP summary, payroll summary và các chỉ số kiểm soát lợi nhuận.
        </Text>
      </Box>

      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, lg: 4 }} gap='20px' mb='20px'>
        <MiniStatistics name='Doanh thu' value={formatCurrency(computedMetrics.revenue)} />
        <MiniStatistics name='Net profit' value={formatCurrency(computedMetrics.netProfit)} />
        <MiniStatistics name='Cash flow' value={formatCurrency(computedMetrics.cashFlow)} />
        <MiniStatistics name='Gross margin' value={`${computedMetrics.grossMargin.toFixed(2)}%`} />
      </SimpleGrid>

      <Card p='16px' mb='20px'>
        <Flex wrap='wrap' gap='10px' align='center'>
          <Badge colorScheme={trialBalance.available ? "green" : "orange"}>
            vw_gl_trial_balance: {trialBalance.available ? "OK" : "Chưa có endpoint"}
          </Badge>
          <Badge colorScheme={supplierPayableSummary.available ? "green" : "orange"}>
            vw_supplier_payable_summary: {supplierPayableSummary.available ? "OK" : "Chưa có endpoint"}
          </Badge>
          <Badge colorScheme={payrollRunSummary.available ? "green" : "orange"}>
            vw_payroll_run_summary: {payrollRunSummary.available ? "OK" : "Chưa có endpoint"}
          </Badge>
        </Flex>
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>KPI tài chính</Text>
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Chỉ số</Th>
                <Th>Giá trị</Th>
              </Tr>
            </Thead>
            <Tbody>
              {kpiRows.map((row) => (
                <Tr key={row.metric}>
                  <Td color={textColor}>{row.metric}</Td>
                  <Td color={textColor} fontWeight='700'>{row.value}</Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Card>

        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>Nguồn dữ liệu báo cáo</Text>
          <SimpleGrid columns={1} gap='8px'>
            <Text color={textColorSecondary}>- Invoices: {invoices.length} bản ghi</Text>
            <Text color={textColorSecondary}>- Payments: {payments.length} bản ghi</Text>
            <Text color={textColorSecondary}>- RevenueSnapshots: {snapshots.length} bản ghi</Text>
            <Text color={textColorSecondary}>
              - Trial balance view: {trialBalance.available ? `${trialBalance.items.length} bản ghi` : "Chưa tích hợp API"}
            </Text>
            <Text color={textColorSecondary}>
              - Supplier payable summary view: {supplierPayableSummary.available ? `${supplierPayableSummary.items.length} bản ghi` : "Chưa tích hợp API"}
            </Text>
            <Text color={textColorSecondary}>
              - Payroll run summary view: {payrollRunSummary.available ? `${payrollRunSummary.items.length} bản ghi` : "Chưa tích hợp API"}
            </Text>
          </SimpleGrid>
        </Card>
      </SimpleGrid>
    </Box>
  );
}
