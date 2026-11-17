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
  getAccountingDashboardV2,
  getPayrollRunSummaryReport,
  getSupplierPayableSummaryReport,
  getTrialBalanceReport
} from "../api/accountingApi";
import { formatCurrency } from "../utils/accountingHelpers";

function fieldOf(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

function safeTime(value) {
  const ts = Date.parse(String(value || ""));
  return Number.isNaN(ts) ? 0 : ts;
}

export default function ReportsPage() {
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [trialBalance, setTrialBalance] = useState({ available: false, items: [] });
  const [supplierPayableSummary, setSupplierPayableSummary] = useState({ available: false, items: [] });
  const [payrollRunSummary, setPayrollRunSummary] = useState({ available: false, items: [] });
  const [dashboard, setDashboard] = useState(null);

  useEffect(() => {
    let mounted = true;
    async function load() {
      setLoading(true);
      setError("");
      try {
        const [trialData, supplierData, payrollData, dashboardData] = await Promise.all([
          getTrialBalanceReport(),
          getSupplierPayableSummaryReport(),
          getPayrollRunSummaryReport(),
          getAccountingDashboardV2()
        ]);
        if (!mounted) return;
        setTrialBalance(trialData);
        setSupplierPayableSummary(supplierData);
        setPayrollRunSummary(payrollData);
        setDashboard(dashboardData || null);
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

  const summary = dashboard?.summary || {};
  const agingBuckets = Array.isArray(dashboard?.agingBuckets) ? dashboard.agingBuckets : [];
  const generatedAt = dashboard?.generatedAt || null;

  const kpiRows = useMemo(() => {
    return [
      { metric: "Doanh thu", value: formatCurrency(summary.revenue) },
      { metric: "Đã thu", value: formatCurrency(summary.collected) },
      { metric: "Net profit", value: formatCurrency(summary.netProfit) },
      { metric: "Net cash flow", value: formatCurrency(summary.cashFlow) },
      { metric: "Generated at", value: generatedAt ? String(generatedAt) : "--" }
    ];
  }, [summary, generatedAt]);

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
      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, lg: 4 }} gap='20px' mb='20px'>
        <MiniStatistics
          name='Doanh thu (dashboard-v2)'
          value={formatCurrency(summary.revenue)}
        />
        <MiniStatistics
          name='Net profit (dashboard-v2)'
          value={formatCurrency(summary.netProfit)}
        />
        <MiniStatistics
          name='Cash flow (dashboard-v2)'
          value={formatCurrency(summary.cashFlow)}
        />
        <MiniStatistics name='Pending approvals' value={summary.pendingApprovalCount == null ? "Chưa có dữ liệu" : String(summary.pendingApprovalCount)} />
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
            <Text color={textColorSecondary} fontSize='xs' maxW='460px'>
              KPI trên trang này dùng cùng source `dashboard-v2` với Dashboard và Export CSV.
            </Text>
          </Flex>
        </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>KPI từ snapshot gần nhất</Text>
          {kpiRows.length ? (
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
          ) : (
            <Text color={textColorSecondary}>Chưa có revenue snapshot để hiển thị KPI.</Text>
          )}
        </Card>

        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>Nguồn dữ liệu báo cáo</Text>
          <SimpleGrid columns={1} gap='8px'>
            <Text color={textColorSecondary}>- Dashboard-v2 aging buckets: {dashboard?.detailsAvailable === true ? `${agingBuckets.length} bucket` : "Chưa tích hợp"}</Text>
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
