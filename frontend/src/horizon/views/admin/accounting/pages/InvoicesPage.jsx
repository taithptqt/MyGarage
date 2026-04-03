import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
  Button,
  Flex,
  FormControl,
  FormLabel,
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
import { MdAttachMoney, MdCheckCircle, MdReceipt, MdRequestQuote } from "react-icons/md";
import {
  getInvoiceDetails,
  getInvoices,
  getPayments,
  updateInvoice
} from "../api/accountingApi";
import {
  formatCurrency,
  formatDateTime,
  normalizeText,
  sumBy,
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
  if (status) return status;

  const total = toNumber(fieldOf(invoice, "totalAmount", "TotalAmount"));
  const paid = toNumber(fieldOf(invoice, "paidAmount", "PaidAmount"));
  if (paid <= 0) return "UNPAID";
  if (paid < total) return "PARTIAL";
  return "PAID";
}

function invoicePayloadFrom(existing, overrides = {}) {
  return {
    serviceOrderId: fieldOf(existing, "serviceOrderId", "ServiceOrderId"),
    retailOrderId: fieldOf(existing, "retailOrderId", "RetailOrderId"),
    vehicleId: fieldOf(existing, "vehicleId", "VehicleId"),
    phone: fieldOf(existing, "phone", "Phone"),
    address: fieldOf(existing, "address", "Address"),
    invoiceUrl: fieldOf(existing, "invoiceUrl", "InvoiceUrl"),
    companyName: fieldOf(existing, "companyName", "CompanyName"),
    companyAddress: fieldOf(existing, "companyAddress", "CompanyAddress"),
    receiveEmail: fieldOf(existing, "receiveEmail", "ReceiveEmail"),
    taxCode: fieldOf(existing, "taxCode", "TaxCode"),
    subTotalAmount: fieldOf(existing, "subTotalAmount", "SubTotalAmount"),
    taxAmount: fieldOf(existing, "taxAmount", "TaxAmount"),
    totalAmount: fieldOf(existing, "totalAmount", "TotalAmount"),
    payerType: fieldOf(existing, "payerType", "PayerType"),
    payerId: fieldOf(existing, "payerId", "PayerId"),
    paidAmount: fieldOf(existing, "paidAmount", "PaidAmount"),
    paymentStatus: fieldOf(existing, "paymentStatus", "PaymentStatus"),
    creditApprovedByStaffId: fieldOf(existing, "creditApprovedByStaffId", "CreditApprovedByStaffId"),
    creditNote: fieldOf(existing, "creditNote", "CreditNote"),
    issuedAt: fieldOf(existing, "issuedAt", "IssuedAt"),
    createdBy: fieldOf(existing, "createdBy", "CreatedBy"),
    updatedBy: fieldOf(existing, "updatedBy", "UpdatedBy"),
    ...overrides
  };
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

  const [saving, setSaving] = useState(false);
  const [editForm, setEditForm] = useState({
    companyName: "",
    companyAddress: "",
    receiveEmail: "",
    taxCode: "",
    creditNote: ""
  });

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
          const totalAmount = toNumber(fieldOf(item, "totalAmount", "TotalAmount"));
          const taxAmount = toNumber(fieldOf(item, "taxAmount", "TaxAmount"));
          const paidAmount = toNumber(fieldOf(item, "paidAmount", "PaidAmount"));
          const status = paymentStatusLabel(item);
          const source = resolveSource(item);
          return {
            raw: item,
            id,
            source,
            status,
            totalAmount,
            taxAmount,
            paidAmount,
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

  useEffect(() => {
    if (!selectedInvoice) return;
    setEditForm({
      companyName: fieldOf(selectedInvoice.raw, "companyName", "CompanyName") || "",
      companyAddress: fieldOf(selectedInvoice.raw, "companyAddress", "CompanyAddress") || "",
      receiveEmail: fieldOf(selectedInvoice.raw, "receiveEmail", "ReceiveEmail") || "",
      taxCode: fieldOf(selectedInvoice.raw, "taxCode", "TaxCode") || "",
      creditNote: fieldOf(selectedInvoice.raw, "creditNote", "CreditNote") || ""
    });
  }, [selectedInvoice]);

  const summary = useMemo(() => {
    return {
      totalInvoice: filteredRows.length,
      totalVat: sumBy(filteredRows, (item) => item.taxAmount),
      totalOutstanding: sumBy(filteredRows, (item) => Math.max(0, item.totalAmount - item.paidAmount)),
      issuedCount: filteredRows.filter((item) => item.issuedAt).length
    };
  }, [filteredRows]);

  async function handleIssue(invoiceRow) {
    if (!invoiceRow) return;
    setSaving(true);
    try {
      const payload = invoicePayloadFrom(invoiceRow.raw, {
        issuedAt: new Date().toISOString()
      });
      await updateInvoice(invoiceRow.id, payload);
      toast({
        status: "success",
        title: `Đã issue hóa đơn #${invoiceRow.id}`
      });
      await loadData();
      setSelectedInvoiceId(invoiceRow.id);
    } catch (err) {
      toast({
        status: "error",
        title: "Issue hóa đơn thất bại",
        description: err?.message
      });
    } finally {
      setSaving(false);
    }
  }

  async function handleSaveAdjustment() {
    if (!selectedInvoice) return;
    setSaving(true);
    try {
      const payload = invoicePayloadFrom(selectedInvoice.raw, {
        companyName: editForm.companyName,
        companyAddress: editForm.companyAddress,
        receiveEmail: editForm.receiveEmail,
        taxCode: editForm.taxCode,
        creditNote: editForm.creditNote
      });
      await updateInvoice(selectedInvoice.id, payload);
      toast({
        status: "success",
        title: `Đã cập nhật hóa đơn #${selectedInvoice.id}`
      });
      await loadData();
      setSelectedInvoiceId(selectedInvoice.id);
    } catch (err) {
      toast({
        status: "error",
        title: "Cập nhật hóa đơn thất bại",
        description: err?.message
      });
    } finally {
      setSaving(false);
    }
  }

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
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, lg: 4 }} gap='20px' mb='20px'>
        <MiniStatistics
          startContent={<MdReceipt size='24px' />}
          name='Tổng hóa đơn'
          value={String(summary.totalInvoice)}
        />
        <MiniStatistics
          startContent={<MdRequestQuote size='24px' />}
          name='VAT đầu ra'
          value={formatCurrency(summary.totalVat)}
        />
        <MiniStatistics
          startContent={<MdAttachMoney size='24px' />}
          name='Chưa thu đủ'
          value={formatCurrency(summary.totalOutstanding)}
        />
        <MiniStatistics
          startContent={<MdCheckCircle size='24px' />}
          name='Đã issue'
          value={String(summary.issuedCount)}
        />
      </SimpleGrid>

      <Card p='20px' mb='20px'>
        <Flex gap='12px' wrap='wrap'>
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
          </Select>
          <Select value={sourceFilter} onChange={(e) => setSourceFilter(e.target.value)} maxW='220px'>
            <option value='ALL'>Tất cả nguồn</option>
            <option value='SERVICE'>Service Order</option>
            <option value='RETAIL'>Retail Order</option>
          </Select>
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
                  <Td color={textColor} fontWeight='600'>#{row.id}</Td>
                  <Td color={textColorSecondary}>{row.source}</Td>
                  <Td color={textColor} isNumeric>{formatCurrency(row.totalAmount)}</Td>
                  <Td color={textColor} isNumeric>{formatCurrency(row.paidAmount)}</Td>
                  <Td>
                    <Badge colorScheme={row.status === "PAID" ? "green" : row.status === "PARTIAL" ? "orange" : "red"}>
                      {row.status}
                    </Badge>
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
                      <Button
                        size='xs'
                        colorScheme='green'
                        onClick={(e) => {
                          e.stopPropagation();
                          handleIssue(row);
                        }}
                        isDisabled={saving}>
                        Issue
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
                <Box p='10px' border='1px solid' borderColor={borderColor} borderRadius='12px'>
                  <Text fontSize='xs' color={textColorSecondary}>Total</Text>
                  <Text fontWeight='700'>{formatCurrency(selectedInvoice.totalAmount)}</Text>
                </Box>
                <Box p='10px' border='1px solid' borderColor={borderColor} borderRadius='12px'>
                  <Text fontSize='xs' color={textColorSecondary}>VAT</Text>
                  <Text fontWeight='700'>{formatCurrency(selectedInvoice.taxAmount)}</Text>
                </Box>
                <Box p='10px' border='1px solid' borderColor={borderColor} borderRadius='12px'>
                  <Text fontSize='xs' color={textColorSecondary}>Paid</Text>
                  <Text fontWeight='700'>{formatCurrency(selectedInvoice.paidAmount)}</Text>
                </Box>
                <Box p='10px' border='1px solid' borderColor={borderColor} borderRadius='12px'>
                  <Text fontSize='xs' color={textColorSecondary}>Source</Text>
                  <Text fontWeight='700'>{selectedInvoice.source}</Text>
                </Box>
              </SimpleGrid>

              <Text color={textColor} fontWeight='600' mb='8px'>Dòng hóa đơn</Text>
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
                        <Text fontSize='sm' fontWeight='600'>{formatCurrency(finalAmount)}</Text>
                      </Flex>
                    );
                  })
                ) : (
                  <Text color={textColorSecondary} fontSize='sm'>Không có dòng chi tiết.</Text>
                )}
              </Box>

              <Text color={textColor} fontWeight='600' mb='8px'>Payment liên quan</Text>
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
                        <Text fontSize='sm'>#{paymentId} · {type || "--"}</Text>
                        <Text fontSize='sm' fontWeight='600'>{formatCurrency(amount)} · {status || "--"}</Text>
                      </Flex>
                    );
                  })
                ) : (
                  <Text color={textColorSecondary} fontSize='sm'>Chưa có payment liên quan.</Text>
                )}
              </Box>

              <Text color={textColor} fontWeight='600' mb='8px'>Điều chỉnh / thay thế invoice</Text>
              <SimpleGrid columns={1} gap='10px'>
                <FormControl>
                  <FormLabel mb='4px' fontSize='xs'>Company name</FormLabel>
                  <Input
                    value={editForm.companyName}
                    onChange={(e) => setEditForm((prev) => ({ ...prev, companyName: e.target.value }))}
                  />
                </FormControl>
                <FormControl>
                  <FormLabel mb='4px' fontSize='xs'>Company address</FormLabel>
                  <Input
                    value={editForm.companyAddress}
                    onChange={(e) => setEditForm((prev) => ({ ...prev, companyAddress: e.target.value }))}
                  />
                </FormControl>
                <FormControl>
                  <FormLabel mb='4px' fontSize='xs'>Receive email</FormLabel>
                  <Input
                    value={editForm.receiveEmail}
                    onChange={(e) => setEditForm((prev) => ({ ...prev, receiveEmail: e.target.value }))}
                  />
                </FormControl>
                <SimpleGrid columns={2} gap='10px'>
                  <FormControl>
                    <FormLabel mb='4px' fontSize='xs'>Tax code</FormLabel>
                    <Input
                      value={editForm.taxCode}
                      onChange={(e) => setEditForm((prev) => ({ ...prev, taxCode: e.target.value }))}
                    />
                  </FormControl>
                  <FormControl>
                    <FormLabel mb='4px' fontSize='xs'>Trạng thái thanh toán (tự động)</FormLabel>
                    <Input value={selectedInvoice.status || "--"} isReadOnly />
                  </FormControl>
                </SimpleGrid>
                <FormControl>
                  <FormLabel mb='4px' fontSize='xs'>Số tiền đã thu (tự động)</FormLabel>
                  <Input value={formatCurrency(selectedInvoice.paidAmount)} isReadOnly />
                </FormControl>
                <FormControl>
                  <FormLabel mb='4px' fontSize='xs'>Credit note</FormLabel>
                  <Input
                    value={editForm.creditNote}
                    onChange={(e) => setEditForm((prev) => ({ ...prev, creditNote: e.target.value }))}
                  />
                </FormControl>
                <Button colorScheme='blue' onClick={handleSaveAdjustment} isLoading={saving}>
                  Lưu điều chỉnh
                </Button>
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
