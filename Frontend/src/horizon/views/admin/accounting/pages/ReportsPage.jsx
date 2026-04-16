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
  getPayrollRunSummaryReport,
  getRevenueSnapshots,
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
  const [snapshots, setSnapshots] = useState([]);

  useEffect(() => {
    let mounted = true;
    async function load() {
      setLoading(true);
      setError("");
      try {
        const [trialData, supplierData, payrollData, snapshotData] = await Promise.all([
          getTrialBalanceReport(),
          getSupplierPayableSummaryReport(),
          getPayrollRunSummaryReport(),
          getRevenueSnapshots()
        ]);
        if (!mounted) return;
        setTrialBalance(trialData);
        setSupplierPayableSummary(supplierData);
        setPayrollRunSummary(payrollData);
        setSnapshots(snapshotData || []);
      } catch (err) {
        if (!mounted) return;
        setError(err?.message || "KhÃ´ng táº£i Ä‘Æ°á»£c dá»¯ liá»‡u bÃ¡o cÃ¡o.");
      } finally {
        if (mounted) setLoading(false);
      }
    }
    load();
    return () => {
      mounted = false;
    };
  }, []);

  const orderedSnapshots = useMemo(
    () =>
      [...(snapshots || [])].sort((a, b) => {
        const left = safeTime(fieldOf(a, "snapshotDate", "SnapshotDate", "createdAt", "CreatedAt"));
        const right = safeTime(fieldOf(b, "snapshotDate", "SnapshotDate", "createdAt", "CreatedAt"));
        return right - left;
      }),
    [snapshots]
  );

  const latestSnapshot = orderedSnapshots[0] || null;

  const kpiRows = useMemo(() => {
    if (!latestSnapshot) return [];
    return [
      { metric: "Doanh thu", value: formatCurrency(fieldOf(latestSnapshot, "totalRevenue", "TotalRevenue")) },
      { metric: "Tá»•ng chi phÃ­", value: formatCurrency(fieldOf(latestSnapshot, "totalExpense", "TotalExpense")) },
      { metric: "Net profit", value: formatCurrency(fieldOf(latestSnapshot, "netProfit", "NetProfit")) },
      { metric: "Net cash flow", value: formatCurrency(fieldOf(latestSnapshot, "netCashFlow", "NetCashFlow")) },
      {
        metric: "NgÃ y snapshot",
        value: String(fieldOf(latestSnapshot, "snapshotDate", "SnapshotDate", "createdAt", "CreatedAt") || "--")
      }
    ];
  }, [latestSnapshot]);

  if (loading) {
    return (
      <Flex minH='260px' align='center' justify='center' direction='column' gap='10px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Äang táº£i dá»¯ liá»‡u bÃ¡o cÃ¡o quáº£n trá»‹...</Text>
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
          name='Doanh thu snapshot gáº§n nháº¥t'
          value={latestSnapshot ? formatCurrency(fieldOf(latestSnapshot, "totalRevenue", "TotalRevenue")) : "--"}
        />
        <MiniStatistics
          name='Net profit snapshot'
          value={latestSnapshot ? formatCurrency(fieldOf(latestSnapshot, "netProfit", "NetProfit")) : "--"}
        />
        <MiniStatistics
          name='Cash flow snapshot'
          value={latestSnapshot ? formatCurrency(fieldOf(latestSnapshot, "netCashFlow", "NetCashFlow")) : "--"}
        />
        <MiniStatistics name='Sá»‘ snapshot' value={String(orderedSnapshots.length)} />
      </SimpleGrid>

      <Card p='16px' mb='20px'>
        <Flex wrap='wrap' gap='10px' align='center'>
          <Badge colorScheme={trialBalance.available ? "green" : "orange"}>
            vw_gl_trial_balance: {trialBalance.available ? "OK" : "ChÆ°a cÃ³ endpoint"}
          </Badge>
          <Badge colorScheme={supplierPayableSummary.available ? "green" : "orange"}>
            vw_supplier_payable_summary: {supplierPayableSummary.available ? "OK" : "ChÆ°a cÃ³ endpoint"}
          </Badge>
          <Badge colorScheme={payrollRunSummary.available ? "green" : "orange"}>
            vw_payroll_run_summary: {payrollRunSummary.available ? "OK" : "ChÆ°a cÃ³ endpoint"}
          </Badge>
          <Text color={textColorSecondary} fontSize='xs' maxW='460px'>
            Trang nÃ y chá»‰ hiá»ƒn thá»‹ snapshot vÃ  cÃ¡c report backend tráº£ vá». UI khÃ´ng tá»± gá»™p invoices/payments Ä‘á»ƒ táº¡o KPI káº¿ toÃ¡n.
          </Text>
        </Flex>
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>KPI tá»« snapshot gáº§n nháº¥t</Text>
          {kpiRows.length ? (
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Chá»‰ sá»‘</Th>
                  <Th>GiÃ¡ trá»‹</Th>
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
            <Text color={textColorSecondary}>ChÆ°a cÃ³ revenue snapshot Ä‘á»ƒ hiá»ƒn thá»‹ KPI.</Text>
          )}
        </Card>

        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>Nguá»“n dá»¯ liá»‡u bÃ¡o cÃ¡o</Text>
          <SimpleGrid columns={1} gap='8px'>
            <Text color={textColorSecondary}>- RevenueSnapshots: {orderedSnapshots.length} báº£n ghi</Text>
            <Text color={textColorSecondary}>
              - Trial balance view: {trialBalance.available ? `${trialBalance.items.length} báº£n ghi` : "ChÆ°a tÃ­ch há»£p API"}
            </Text>
            <Text color={textColorSecondary}>
              - Supplier payable summary view: {supplierPayableSummary.available ? `${supplierPayableSummary.items.length} báº£n ghi` : "ChÆ°a tÃ­ch há»£p API"}
            </Text>
            <Text color={textColorSecondary}>
              - Payroll run summary view: {payrollRunSummary.available ? `${payrollRunSummary.items.length} báº£n ghi` : "ChÆ°a tÃ­ch há»£p API"}
            </Text>
          </SimpleGrid>
        </Card>
      </SimpleGrid>
    </Box>
  );
}
