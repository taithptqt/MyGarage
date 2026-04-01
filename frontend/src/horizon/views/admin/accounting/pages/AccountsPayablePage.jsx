import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
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
  getAccountsPayable,
  getSupplierInvoices,
  getSupplierPayments
} from "../api/accountingApi";
import {
  formatCurrency,
  formatDate,
  normalizeText,
  sumBy,
  toNumber
} from "../utils/accountingHelpers";

function fieldOf(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

export default function AccountsPayablePage() {
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");

  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [error, setError] = useState("");

  const [ap, setAp] = useState({ available: false, items: [] });
  const [supplierInvoices, setSupplierInvoices] = useState({ available: false, items: [] });
  const [supplierPayments, setSupplierPayments] = useState({ available: false, items: [] });

  useEffect(() => {
    let mounted = true;
    async function load() {
      setLoading(true);
      setError("");
      try {
        const [apData, supplierInvoiceData, supplierPaymentData] = await Promise.all([
          getAccountsPayable(),
          getSupplierInvoices(),
          getSupplierPayments()
        ]);
        if (!mounted) return;
        setAp(apData);
        setSupplierInvoices(supplierInvoiceData);
        setSupplierPayments(supplierPaymentData);
      } catch (err) {
        if (!mounted) return;
        setError(err?.message || "Không tải được dữ liệu AP.");
      } finally {
        if (mounted) setLoading(false);
      }
    }
    load();
    return () => {
      mounted = false;
    };
  }, []);

  const rows = useMemo(
    () =>
      (ap.items || []).map((item, index) => {
        const id = fieldOf(item, "id", "Id", "apId", "ApId") || index + 1;
        const outstanding = toNumber(fieldOf(item, "outstandingAmount", "OutstandingAmount", "outstanding", "Outstanding"));
        const paid = toNumber(fieldOf(item, "paidAmount", "PaidAmount", "paid", "Paid"));
        const dueDate = fieldOf(item, "dueDate", "DueDate");
        const now = new Date();
        const dueTs = dueDate ? new Date(dueDate).getTime() : 0;
        const overdue = dueTs > 0 && dueTs < now.getTime() && outstanding > 0;
        return {
          id,
          supplierName: fieldOf(item, "supplierName", "SupplierName", "supplierCode", "SupplierCode") || `Supplier #${id}`,
          apCode: fieldOf(item, "apCode", "ApCode", "code", "Code") || `AP-${id}`,
          branchName: fieldOf(item, "branchName", "BranchName") || "--",
          dueDate,
          paid,
          outstanding,
          status: fieldOf(item, "status", "Status") || (overdue ? "OVERDUE" : outstanding <= 0 ? "CLOSED" : "OPEN"),
          overdue
        };
      }),
    [ap.items]
  );

  const filteredRows = useMemo(() => {
    return rows.filter((item) => {
      const target = normalizeText([item.apCode, item.supplierName, item.branchName, item.status].join(" "));
      return !search || target.includes(normalizeText(search));
    });
  }, [rows, search]);

  const summary = useMemo(() => {
    return {
      totalOutstanding: sumBy(filteredRows, (item) => item.outstanding),
      totalPaid: sumBy(filteredRows, (item) => item.paid),
      overdueAmount: sumBy(filteredRows.filter((item) => item.overdue), (item) => item.outstanding)
    };
  }, [filteredRows]);

  if (loading) {
    return (
      <Flex minH='260px' align='center' justify='center' direction='column' gap='10px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dữ liệu AP...</Text>
      </Flex>
    );
  }

  return (
    <Box>
      <Box mb='20px'>
        <Text color={textColor} fontSize='2xl' fontWeight='700'>Phải trả nhà cung cấp (AP)</Text>
        <Text color={textColorSecondary} fontSize='sm'>
          Theo dõi due date, outstanding, aging nợ NCC và đối soát trạng thái thanh toán.
        </Text>
      </Box>

      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} gap='20px' mb='20px'>
        <MiniStatistics name='Outstanding AP' value={formatCurrency(summary.totalOutstanding)} />
        <MiniStatistics name='Đã thanh toán' value={formatCurrency(summary.totalPaid)} />
        <MiniStatistics name='Quá hạn' value={formatCurrency(summary.overdueAmount)} />
      </SimpleGrid>

      <Card p='16px' mb='20px'>
        <Flex wrap='wrap' gap='10px' align='center'>
          <Input
            placeholder='Tìm AP theo code/NCC/chi nhánh/trạng thái'
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            maxW='360px'
          />
          <Badge colorScheme={ap.available ? "green" : "orange"}>
            API AccountsPayable: {ap.available ? "OK" : "Chưa có"}
          </Badge>
          <Badge colorScheme={supplierInvoices.available ? "green" : "orange"}>
            API SupplierInvoice: {supplierInvoices.available ? "OK" : "Chưa có"}
          </Badge>
          <Badge colorScheme={supplierPayments.available ? "green" : "orange"}>
            API SupplierPayment: {supplierPayments.available ? "OK" : "Chưa có"}
          </Badge>
        </Flex>
      </Card>

      <Card p='20px'>
        {filteredRows.length ? (
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>AP code</Th>
                <Th>Supplier</Th>
                <Th>Branch</Th>
                <Th>Due date</Th>
                <Th isNumeric>Paid</Th>
                <Th isNumeric>Outstanding</Th>
                <Th>Status</Th>
              </Tr>
            </Thead>
            <Tbody>
              {filteredRows.map((row) => (
                <Tr key={row.id}>
                  <Td color={textColor} fontWeight='600'>{row.apCode}</Td>
                  <Td color={textColorSecondary}>{row.supplierName}</Td>
                  <Td color={textColorSecondary}>{row.branchName}</Td>
                  <Td color={textColorSecondary}>{formatDate(row.dueDate)}</Td>
                  <Td color={textColor} isNumeric>{formatCurrency(row.paid)}</Td>
                  <Td color={textColor} isNumeric>{formatCurrency(row.outstanding)}</Td>
                  <Td>
                    <Badge colorScheme={row.overdue ? "red" : row.status === "CLOSED" ? "green" : "orange"}>
                      {row.status}
                    </Badge>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        ) : (
          <Text color={textColorSecondary}>
            Chưa có dữ liệu AP khả dụng từ backend. Khi endpoint AP được mở, trang này sẽ hiển thị đầy đủ aging, due date và outstanding theo branch/supplier.
          </Text>
        )}
      </Card>
    </Box>
  );
}
