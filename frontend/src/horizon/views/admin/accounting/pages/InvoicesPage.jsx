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
  useColorModeValue,
  useToast
} from "@chakra-ui/react";
import Card from "components/card/Card";
import MiniStatistics from "components/card/MiniStatistics";
import { MdCheckCircle, MdReceipt, MdRequestQuote } from "react-icons/md";
import { getInvoiceDetails, getInvoices, getPayments } from "../api/accountingApi";
import {
  formatCurrency,
  formatDateTime,
  normalizeText,
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

function resolveSource(invoice) {
  const serviceOrderId = fieldOf(invoice, "serviceOrderId", "ServiceOrderId");
  const retailOrderId = fieldOf(invoice, "retailOrderId", "RetailOrderId");
  if (serviceOrderId) return `SO-${serviceOrderId}`;
  if (retailOrderId) return `RO-${retailOrderId}`;
  return "N/A";
}

function paymentStatusLabel(invoice) {
  const status = String(fieldOf(invoice, "paymentStatus", "PaymentStatus") || "").toUpperCase();
  return status || "UNKNOWN";
}

function readOutstandingAmount(invoice) {
  const explicitOutstanding = fieldOf(
    invoice,
    "outstandingAmount",
    "OutstandingAmount",
    "remainingAmount",
    "RemainingAmount"
  );
  if (explicitOutstanding !== undefined && explicitOutstanding !== null) {
    return toNumber(explicitOutstanding);
  }
  return null;
}

function statusColor(status) {
  if (status === "PAID") return "green";
  if (status === "PARTIAL") return "orange";
  if (status === "REFUNDED") return "purple";
  if (status === "UNKNOWN") return "gray";
  return "red";
}

function ReadonlyField({ label, value, borderColor, textColorSecondary }) {
  return (
    <Box p='10px' border='1px solid' borderColor={borderColor} borderRadius='12px'>
      <Text fontSize='xs' color={textColorSecondary}>
        {label}
      </Text>
      <Text fontWeight='600'>{value || "--"}</Text>
    </Box>
  );
}

export default function InvoicesPage() {
  const toast = useToast();
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");
  const borderColor = useColorModeValue("secondaryGray.200", "whiteAlpha.100");
  const selectedBg = useColorModeValue("secondaryGray.100", "whiteAlpha.100");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [sourceFilter, setSourceFilter] = useState("ALL");

  const [invoices, setInvoices] = useState([]);
  const [invoiceDetails, setInvoiceDetails] = useState([]);
  const [payments, setPayments] = useState([]);
  const [selectedInvoiceId, setSelectedInvoiceId] = useState(null);

  async function loadData() {
    setLoading(true);
    setError("");
    try {
      const [invoiceData, detailData, paymentData] = await Promise.all([
        getInvoices(),
        getInvoiceDetails(),
        getPayments()
      ]);
      setInvoices(invoiceData || []);
      setInvoiceDetails(detailData || []);
      setPayments(paymentData || []);
      if ((invoiceData || []).length) {
        setSelectedInvoiceId(fieldOf(invoiceData[0], "id", "Id"));
      }
    } catch (err) {
      setError(err?.message || "Không tải được danh sách hóa đơn.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  const normalizedRows = useMemo(
    () =>
      (invoices || [])
        .map((item) => {
          const id = fieldOf(item, "id", "Id");
          return {
            raw: item,
            id,
            source: resolveSource(item),
            status: paymentStatusLabel(item),
            totalAmount: toNumber(fieldOf(item, "totalAmount", "TotalAmount")),
            taxAmount: toNumber(fieldOf(item, "taxAmount", "TaxAmount")),
            paidAmount: toNumber(fieldOf(item, "paidAmount", "PaidAmount")),
            outstandingAmount: readOutstandingAmount(item),
            issuedAt: fieldOf(item, "issuedAt", "IssuedAt"),
            companyName: fieldOf(item, "companyName", "CompanyName"),
            receiveEmail: fieldOf(item, "receiveEmail", "ReceiveEmail"),
            serviceOrderId: fieldOf(item, "serviceOrderId", "ServiceOrderId"),
            retailOrderId: fieldOf(item, "retailOrderId", "RetailOrderId")
          };
        })
        .sort((a, b) => Number(b.id || 0) - Number(a.id || 0)),
    [invoices]
  );

  const filteredRows = useMemo(() => {
    return normalizedRows.filter((item) => {
      const searchTarget = normalizeText(
        [
          item.id,
          item.source,
          item.companyName,
          item.receiveEmail,
          item.serviceOrderId,
          item.retailOrderId
        ].join(" ")
      );
      const searchPass = !search || searchTarget.includes(normalizeText(search));
      const statusPass = statusFilter === "ALL" || item.status === statusFilter;
      const sourcePass =
        sourceFilter === "ALL" ||
        (sourceFilter === "SERVICE" && item.serviceOrderId) ||
        (sourceFilter === "RETAIL" && item.retailOrderId);
      return searchPass && statusPass && sourcePass;
    });
  }, [normalizedRows, search, statusFilter, sourceFilter]);

  const selectedInvoice = useMemo(
    () => normalizedRows.find((item) => String(item.id) === String(selectedInvoiceId)),
    [normalizedRows, selectedInvoiceId]
  );

  const selectedDetails = useMemo(
    () =>
      (invoiceDetails || []).filter(
        (item) => String(fieldOf(item, "invoiceId", "InvoiceId")) === String(selectedInvoiceId)
      ),
    [invoiceDetails, selectedInvoiceId]
  );

  const selectedPayments = useMemo(
    () =>
      (payments || []).filter(
        (item) => String(fieldOf(item, "invoiceId", "InvoiceId")) === String(selectedInvoiceId)
      ),
    [payments, selectedInvoiceId]
  );

  const summary = useMemo(
    () => ({
      totalInvoice: filteredRows.length,
      issuedCount: filteredRows.filter((item) => item.issuedAt).length,
      paidCount: filteredRows.filter((item) => item.status === "PAID").length,
      unknownStatusCount: filteredRows.filter((item) => item.status === "UNKNOWN").length
    }),
    [filteredRows]
  );

  function handleResend(invoiceRow) {
    const email = invoiceRow?.receiveEmail;
    const invoiceUrl = fieldOf(invoiceRow?.raw, "invoiceUrl", "InvoiceUrl");
    if (invoiceUrl && typeof window !== "undefined") {
      window.open(invoiceUrl, "_blank", "noopener,noreferrer");
      toast({
        status: "success",
        title: "Đã mở link hóa đơn để gửi lại"
      });
      return;
    }
    if (email && typeof window !== "undefined") {
      window.location.href = `mailto:${email}?subject=Gui%20lai%20hoa%20don%20#${invoiceRow.id}`;
      return;
    }
    toast({
      status: "warning",
      title: "Hóa đơn chưa có email nhận hoặc invoice URL"
    });
  }

  if (loading) {
    return (
      <Flex minH='280px' align='center' justify='center' direction='column' gap='12px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dữ liệu hóa đơn...</Text>
      </Flex>
    );
  }

  return (
    <Box>
      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>
            {error}
          </Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, lg: 4 }} gap='20px' mb='20px'>
        <MiniStatistics startContent={<MdReceipt size='24px' />} name='Tổng hóa đơn' value={String(summary.totalInvoice)} />
        <MiniStatistics startContent={<MdCheckCircle size='24px' />} name='Đã thanh toán' value={String(summary.paidCount)} />
        <MiniStatistics startContent={<MdCheckCircle size='24px' />} name='Đã phát hành' value={String(summary.issuedCount)} />
        <MiniStatistics startContent={<MdRequestQuote size='24px' />} name='Thiếu trạng thái thanh toán' value={String(summary.unknownStatusCount)} />
      </SimpleGrid>

      <Card p='20px' mb='20px'>
        <Flex gap='12px' wrap='wrap' align='center'>
          <Input
            placeholder='Tìm theo mã invoice / SO / RO / email / công ty'
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            maxW='360px'
          />
          <Select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} maxW='220px'>
            <option value='ALL'>Tất cả trạng thái</option>
            <option value='UNPAID'>UNPAID</option>
            <option value='PARTIAL'>PARTIAL</option>
            <option value='PAID'>PAID</option>
            <option value='REFUNDED'>REFUNDED</option>
            <option value='UNKNOWN'>UNKNOWN</option>
          </Select>
          <Select value={sourceFilter} onChange={(e) => setSourceFilter(e.target.value)} maxW='220px'>
            <option value='ALL'>Tất cả nguồn</option>
            <option value='SERVICE'>Service Order</option>
            <option value='RETAIL'>Retail Order</option>
          </Select>
          <Text color={textColorSecondary} fontSize='xs' maxW='420px'>
            Payment status và công nợ trên màn hình này chỉ hiển thị theo field backend trả về, UI không tự suy diễn thay backend.
          </Text>
        </Flex>
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 5 }} gap='20px'>
        <Card p='20px' gridColumn={{ base: "span 1", xl: "span 3" }}>
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Mã</Th>
                <Th>Nguồn</Th>
                <Th isNumeric>Tổng tiền</Th>
                <Th isNumeric>Đã thu</Th>
                <Th>Trạng thái</Th>
                <Th>Issued</Th>
                <Th textAlign='right'>Thao tác</Th>
              </Tr>
            </Thead>
            <Tbody>
              {filteredRows.map((row) => (
                <Tr
                  key={row.id}
                  bg={String(row.id) === String(selectedInvoiceId) ? selectedBg : "transparent"}
                  onClick={() => setSelectedInvoiceId(row.id)}
                  cursor='pointer'>
                  <Td color={textColor} fontWeight='600'>
                    #{row.id}
                  </Td>
                  <Td color={textColorSecondary}>{row.source}</Td>
                  <Td color={textColor} isNumeric>
                    {formatCurrency(row.totalAmount)}
                  </Td>
                  <Td color={textColor} isNumeric>
                    {formatCurrency(row.paidAmount)}
                  </Td>
                  <Td>
                    <Badge colorScheme={statusColor(row.status)}>{row.status}</Badge>
                  </Td>
                  <Td color={textColorSecondary}>{row.issuedAt ? formatDateTime(row.issuedAt) : "--"}</Td>
                  <Td textAlign='right'>
                    <Flex justify='flex-end' gap='8px'>
                      <Button
                        size='xs'
                        colorScheme='blue'
                        variant='outline'
                        onClick={(e) => {
                          e.stopPropagation();
                          handleResend(row);
                        }}>
                        Gửi lại
                      </Button>
                    </Flex>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Card>

        <Card p='20px' gridColumn={{ base: "span 1", xl: "span 2" }}>
          {selectedInvoice ? (
            <>
              <Text color={textColor} fontWeight='700' mb='14px'>
                Chi tiết invoice #{selectedInvoice.id}
              </Text>

              <SimpleGrid columns={2} gap='10px' mb='14px'>
                <ReadonlyField
                  label='Total'
                  value={formatCurrency(selectedInvoice.totalAmount)}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <ReadonlyField
                  label='VAT'
                  value={formatCurrency(selectedInvoice.taxAmount)}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <ReadonlyField
                  label='Paid (backend)'
                  value={formatCurrency(selectedInvoice.paidAmount)}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <ReadonlyField
                  label='Source'
                  value={selectedInvoice.source}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <ReadonlyField
                  label='Outstanding (backend)'
                  value={
                    selectedInvoice.outstandingAmount === null
                      ? "Backend chưa trả field"
                      : formatCurrency(selectedInvoice.outstandingAmount)
                  }
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
              </SimpleGrid>

              <Text color={textColor} fontWeight='600' mb='8px'>
                Dòng hóa đơn
              </Text>
              <Box mb='12px' maxH='150px' overflowY='auto'>
                {selectedDetails.length ? (
                  selectedDetails.map((detail) => {
                    const detailId = fieldOf(detail, "id", "Id");
                    const itemName = fieldOf(detail, "itemName", "ItemName") || `Item #${detailId}`;
                    const finalAmount = toNumber(fieldOf(detail, "finalAmount", "FinalAmount"));
                    return (
                      <Flex
                        key={detailId}
                        justify='space-between'
                        p='8px'
                        borderBottom='1px solid'
                        borderColor={borderColor}>
                        <Text fontSize='sm'>{itemName}</Text>
                        <Text fontSize='sm' fontWeight='600'>
                          {formatCurrency(finalAmount)}
                        </Text>
                      </Flex>
                    );
                  })
                ) : (
                  <Text color={textColorSecondary} fontSize='sm'>
                    Không có dòng chi tiết.
                  </Text>
                )}
              </Box>

              <Text color={textColor} fontWeight='600' mb='8px'>
                Payment liên quan
              </Text>
              <Box mb='16px' maxH='130px' overflowY='auto'>
                {selectedPayments.length ? (
                  selectedPayments.map((payment) => {
                    const paymentId = fieldOf(payment, "id", "Id");
                    const type = fieldOf(payment, "type", "Type");
                    const amount = toNumber(fieldOf(payment, "amount", "Amount"));
                    const status = fieldOf(payment, "status", "Status");
                    return (
                      <Flex
                        key={paymentId}
                        justify='space-between'
                        p='8px'
                        borderBottom='1px solid'
                        borderColor={borderColor}>
                        <Text fontSize='sm'>
                          #{paymentId} • {type || "--"}
                        </Text>
                        <Text fontSize='sm' fontWeight='600'>
                          {formatCurrency(amount)} • {status || "--"}
                        </Text>
                      </Flex>
                    );
                  })
                ) : (
                  <Text color={textColorSecondary} fontSize='sm'>
                    Chưa có payment liên quan.
                  </Text>
                )}
              </Box>

              <Text color={textColor} fontWeight='600' mb='8px'>
                Thông tin invoice
              </Text>
              <SimpleGrid columns={1} gap='10px'>
                <ReadonlyField
                  label='Company name'
                  value={fieldOf(selectedInvoice.raw, "companyName", "CompanyName")}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <ReadonlyField
                  label='Company address'
                  value={fieldOf(selectedInvoice.raw, "companyAddress", "CompanyAddress")}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <ReadonlyField
                  label='Receive email'
                  value={fieldOf(selectedInvoice.raw, "receiveEmail", "ReceiveEmail")}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <SimpleGrid columns={2} gap='10px'>
                  <ReadonlyField
                    label='Tax code'
                    value={fieldOf(selectedInvoice.raw, "taxCode", "TaxCode")}
                    borderColor={borderColor}
                    textColorSecondary={textColorSecondary}
                  />
                  <ReadonlyField
                    label='Trạng thái thanh toán'
                    value={selectedInvoice.status}
                    borderColor={borderColor}
                    textColorSecondary={textColorSecondary}
                  />
                </SimpleGrid>
                <ReadonlyField
                  label='Số tiền đã thu (backend)'
                  value={formatCurrency(selectedInvoice.paidAmount)}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <ReadonlyField
                  label='Credit note'
                  value={fieldOf(selectedInvoice.raw, "creditNote", "CreditNote")}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <Text fontSize='sm' color={textColorSecondary}>
                  Invoice là chứng từ bất biến. Mọi điều chỉnh phải đi qua quy trình điều chỉnh/thay thế riêng, không sửa trực tiếp trên màn hình kế toán.
                </Text>
              </SimpleGrid>
            </>
          ) : (
            <Text color={textColorSecondary}>Chọn một invoice để xem chi tiết.</Text>
          )}
        </Card>
      </SimpleGrid>
    </Box>
  );
}
