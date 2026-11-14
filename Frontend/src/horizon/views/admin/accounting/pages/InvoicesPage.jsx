import React, { useEffect, useMemo, useRef, useState } from "react";
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
import { getInvoiceListPage, getInvoiceViewDetail } from "../api/invoicePagingApi";
import {
  formatCurrency,
  formatDateTime
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
    const amount = Number(explicitOutstanding);
    return Number.isFinite(amount) ? amount : null;
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

function moneyOrMissing(value) {
  return value === null || value === undefined || value === "" || !Number.isFinite(Number(value)) ? "--" : formatCurrency(value);
}

function Pagination({ total, page, pageSize, onPageChange, onPageSizeChange }) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <Flex align='center' justify='center' gap='8px' mt='12px' wrap='wrap'>
      {onPageSizeChange ? <Select aria-label='Số dòng mỗi trang' value={pageSize} maxW='90px' onChange={(event) => onPageSizeChange(Number(event.target.value))}>
        {[10, 20, 50].map((size) => <option key={size} value={size}>{size}</option>)}
      </Select> : null}
      <Button size='sm' onClick={() => onPageChange(page - 1)} isDisabled={page <= 1}>Trước</Button>
      <Text fontSize='sm'>{page} / {totalPages} ({total})</Text>
      <Button size='sm' onClick={() => onPageChange(page + 1)} isDisabled={page >= totalPages}>Sau</Button>
    </Flex>
  );
}

function normalizeInvoice(item) {
  return {
    raw: item,
    id: fieldOf(item, "id", "Id"),
    source: resolveSource(item),
    status: paymentStatusLabel(item),
    totalAmount: fieldOf(item, "totalAmount", "TotalAmount"),
    taxAmount: fieldOf(item, "taxAmount", "TaxAmount"),
    paidAmount: fieldOf(item, "paidAmount", "PaidAmount"),
    outstandingAmount: readOutstandingAmount(item),
    issuedAt: fieldOf(item, "issuedAt", "IssuedAt"),
    companyName: fieldOf(item, "companyName", "CompanyName"),
    receiveEmail: fieldOf(item, "receiveEmail", "ReceiveEmail"),
    serviceOrderId: fieldOf(item, "serviceOrderId", "ServiceOrderId"),
    retailOrderId: fieldOf(item, "retailOrderId", "RetailOrderId")
  };
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
  const searchRef = useRef("");
  const searchChanged = useRef(false);
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [searchRevision, setSearchRevision] = useState(0);
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [sourceFilter, setSourceFilter] = useState("ALL");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [listData, setListData] = useState(null);
  const [selectedInvoiceId, setSelectedInvoiceId] = useState(null);
  const [detailData, setDetailData] = useState(null);
  const [detailError, setDetailError] = useState("");
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailsPage, setDetailsPage] = useState(1);
  const [paymentsPage, setPaymentsPage] = useState(1);
  const detailPageSize = 10;

  useEffect(() => {
    if (!searchChanged.current) return undefined;
    const timer = setTimeout(() => {
      setPage(1);
      setDebouncedSearch(search);
      setSearchRevision((value) => value + 1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let current = true;
    setLoading(true);
    setError("");
    setListData(null);
    getInvoiceListPage({ search: debouncedSearch || undefined, status: statusFilter, source: sourceFilter, page, pageSize })
      .then((result) => {
        if (!current || searchRef.current !== debouncedSearch) return;
        const lastPage = Math.max(1, Math.ceil(result.total / result.pageSize));
        if (page > lastPage) {
          setPage(lastPage);
          return;
        }
        setListData(result);
        setSelectedInvoiceId((previous) => result.items.some((item) => String(fieldOf(item, "id", "Id")) === String(previous))
          ? previous : (fieldOf(result.items[0], "id", "Id") ?? null));
      })
      .catch((err) => {
        if (!current || searchRef.current !== debouncedSearch) return;
        setListData(null);
        setSelectedInvoiceId(null);
        setError(err?.message || "Không tải được danh sách hóa đơn.");
      })
      .finally(() => { if (current && searchRef.current === debouncedSearch) setLoading(false); });
    return () => { current = false; };
  }, [debouncedSearch, searchRevision, statusFilter, sourceFilter, page, pageSize]);

  useEffect(() => {
    setDetailsPage(1);
    setPaymentsPage(1);
    setDetailData(null);
    setDetailError("");
  }, [selectedInvoiceId]);

  useEffect(() => {
    if (selectedInvoiceId == null) { setDetailLoading(false); return undefined; }
    let current = true;
    setDetailLoading(true);
    setDetailData(null);
    setDetailError("");
    getInvoiceViewDetail(selectedInvoiceId, { detailsPage, paymentsPage, pageSize: detailPageSize })
      .then((result) => {
        if (!current) return;
        const lastDetailsPage = Math.max(1, Math.ceil(result.detailsTotal / result.pageSize));
        const lastPaymentsPage = Math.max(1, Math.ceil(result.paymentsTotal / result.pageSize));
        if (detailsPage > lastDetailsPage || paymentsPage > lastPaymentsPage) {
          setDetailsPage(Math.min(detailsPage, lastDetailsPage));
          setPaymentsPage(Math.min(paymentsPage, lastPaymentsPage));
          return;
        }
        setDetailData({ ...result, requestedId: selectedInvoiceId });
      })
      .catch((err) => { if (current) setDetailError(err?.message || "Không tải được chi tiết hóa đơn."); })
      .finally(() => { if (current) setDetailLoading(false); });
    return () => { current = false; };
  }, [selectedInvoiceId, detailsPage, paymentsPage]);

  const filteredRows = useMemo(() => (listData?.items || []).map(normalizeInvoice), [listData]);
  const selectedInvoice = useMemo(() => detailData && String(detailData.requestedId) === String(selectedInvoiceId)
    ? normalizeInvoice(detailData.invoice) : null, [detailData, selectedInvoiceId]);
  const selectedDetails = selectedInvoice ? detailData.details : [];
  const selectedPayments = selectedInvoice ? detailData.payments : [];
  const summary = listData?.summary;
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
        <MiniStatistics startContent={<MdReceipt size='24px' />} name='Tổng hóa đơn' value={summary ? String(summary.totalInvoice) : "--"} />
        <MiniStatistics startContent={<MdCheckCircle size='24px' />} name='Đã thanh toán' value={summary ? String(summary.paidCount) : "--"} />
        <MiniStatistics startContent={<MdCheckCircle size='24px' />} name='Đã phát hành' value={summary ? String(summary.issuedCount) : "--"} />
        <MiniStatistics startContent={<MdRequestQuote size='24px' />} name='Thiếu trạng thái thanh toán' value={summary ? String(summary.unknownStatusCount) : "--"} />
      </SimpleGrid>

      <Card p='20px' mb='20px'>
        <Flex gap='12px' wrap='wrap' align='center'>
          <Input
            placeholder='Tìm theo mã invoice / SO / RO / email / công ty'
            value={search}
            onChange={(e) => { searchChanged.current = true; searchRef.current = e.target.value; setSearch(e.target.value); setLoading(true); setListData(null); setSelectedInvoiceId(null); setError(""); }}
            maxW='360px'
          />
          <Select value={statusFilter} onChange={(e) => { setPage(1); setStatusFilter(e.target.value); }} maxW='220px'>
            <option value='ALL'>Tất cả trạng thái</option>
            <option value='UNPAID'>UNPAID</option>
            <option value='PARTIAL'>PARTIAL</option>
            <option value='PAID'>PAID</option>
            <option value='REFUNDED'>REFUNDED</option>
            <option value='UNKNOWN'>UNKNOWN</option>
          </Select>
          <Select value={sourceFilter} onChange={(e) => { setPage(1); setSourceFilter(e.target.value); }} maxW='220px'>
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
                    {moneyOrMissing(row.totalAmount)}
                  </Td>
                  <Td color={textColor} isNumeric>
                    {moneyOrMissing(row.paidAmount)}
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
          {loading ? <Flex align='center' gap='8px' mt='12px'><Spinner size='sm' /><Text>Đang tải dữ liệu hóa đơn...</Text></Flex> : null}
          {listData ? <Pagination total={listData.total} page={page} pageSize={pageSize} onPageChange={setPage} onPageSizeChange={(size) => { setPage(1); setPageSize(size); }} /> : null}
        </Card>

        <Card p='20px' gridColumn={{ base: "span 1", xl: "span 2" }}>
          {detailLoading ? <Flex align='center' gap='8px'><Spinner size='sm' /><Text>Đang tải dữ liệu hóa đơn...</Text></Flex> : detailError ? <Text color='red.400'>{detailError}</Text> : selectedInvoice ? (
            <>
              <Text color={textColor} fontWeight='700' mb='14px'>
                Chi tiết invoice #{selectedInvoice.id}
              </Text>

              <SimpleGrid columns={2} gap='10px' mb='14px'>
                <ReadonlyField
                  label='Total'
                  value={moneyOrMissing(selectedInvoice.totalAmount)}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <ReadonlyField
                  label='VAT'
                  value={moneyOrMissing(selectedInvoice.taxAmount)}
                  borderColor={borderColor}
                  textColorSecondary={textColorSecondary}
                />
                <ReadonlyField
                  label='Paid (backend)'
                  value={moneyOrMissing(selectedInvoice.paidAmount)}
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
                    const finalAmount = fieldOf(detail, "finalAmount", "FinalAmount");
                    return (
                      <Flex
                        key={detailId}
                        justify='space-between'
                        p='8px'
                        borderBottom='1px solid'
                        borderColor={borderColor}>
                        <Text fontSize='sm'>{itemName}</Text>
                        <Text fontSize='sm' fontWeight='600'>
                          {moneyOrMissing(finalAmount)}
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
              <Pagination total={detailData.detailsTotal} page={detailsPage} pageSize={detailPageSize} onPageChange={setDetailsPage} />

              <Text color={textColor} fontWeight='600' mb='8px'>
                Payment liên quan
              </Text>
              <Box mb='16px' maxH='130px' overflowY='auto'>
                {selectedPayments.length ? (
                  selectedPayments.map((payment) => {
                    const paymentId = fieldOf(payment, "id", "Id");
                    const type = fieldOf(payment, "type", "Type");
                    const amount = fieldOf(payment, "amount", "Amount");
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
                          {moneyOrMissing(amount)} • {status || "--"}
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
              <Pagination total={detailData.paymentsTotal} page={paymentsPage} pageSize={detailPageSize} onPageChange={setPaymentsPage} />

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
                  value={moneyOrMissing(selectedInvoice.paidAmount)}
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
