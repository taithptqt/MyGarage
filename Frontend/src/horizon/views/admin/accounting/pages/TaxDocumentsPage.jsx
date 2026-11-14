import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
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
import { getDocumentAttachments, getInvoiceTaxSummary, getLegalDocuments, getTaxFilings } from "../api/accountingApi";
import { formatCurrency, formatDate, formatDateTime, normalizeDate, toNumber } from "../utils/accountingHelpers";

const TAX_TYPE_LABELS = {
  VAT: "VAT",
  PIT: "PIT",
  CIT: "CIT",
  INSURANCE: "BHXH/BHYT/BHTN"
};

const TAX_STATUS_META = {
  DRAFT: { label: "Nháp", colorScheme: "gray" },
  READY: { label: "Sẵn sàng nộp", colorScheme: "orange" },
  SUBMITTED: { label: "Đã nộp chờ phản hồi", colorScheme: "blue" },
  ACCEPTED: { label: "Đã chấp nhận", colorScheme: "green" },
  REJECTED: { label: "Bị từ chối", colorScheme: "red" }
};

const DOC_TYPE_LABELS = {
  PAYMENT_PROOF: "Chứng từ thanh toán",
  INVOICE_FILE: "File hóa đơn",
  REFUND_PROOF: "Chứng từ hoàn tiền",
  PAYROLL_APPROVAL: "Phê duyệt lương",
  TAX_RECEIPT: "Biên lai thuế",
  SUPPLIER_INVOICE: "Hóa đơn NCC",
  LABOR_CONTRACT: "Hợp đồng lao động",
  OTHER: "Khác"
};

const ENTITY_TYPE_LABELS = {
  TAX_FILING: "Hồ sơ thuế",
  INVOICE: "Hóa đơn",
  PAYMENT: "Thanh toán",
  PAYROLL_RUN: "Đợt lương",
  PAYROLL_ITEM: "Phiếu lương",
  SUPPLIER_INVOICE: "Hóa đơn NCC",
  FINANCE_APPROVAL_REQUEST: "Yêu cầu phê duyệt tài chính",
  OTHER: "Khác"
};

function fieldOf(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

function upper(value) {
  return String(value || "").trim().toUpperCase();
}

function text(value) {
  return String(value || "").trim();
}

function containsIgnoreCase(source, keyword) {
  return text(source).toLowerCase().includes(text(keyword).toLowerCase());
}

function taxTypeLabel(value) {
  const code = upper(value);
  return TAX_TYPE_LABELS[code] || code || "--";
}

function taxStatusMeta(value) {
  const code = upper(value);
  return TAX_STATUS_META[code] || { label: code || "Chưa xác định", colorScheme: "gray" };
}

function docTypeLabel(value) {
  const code = upper(value);
  return DOC_TYPE_LABELS[code] || code || "--";
}

function entityTypeLabel(value) {
  const code = upper(value);
  return ENTITY_TYPE_LABELS[code] || code || "--";
}

function formatPeriodRange(fromDate, toDate) {
  if (!fromDate && !toDate) return "--";
  return `${formatDate(fromDate)} - ${formatDate(toDate)}`;
}

function normalizeTaxFilings(items) {
  return (items || []).map((item, index) => {
    const id = Number(fieldOf(item, "id", "Id")) || index + 1;
    const taxType = upper(fieldOf(item, "taxType", "TaxType", "filingType", "FilingType"));
    const periodCode = text(fieldOf(item, "periodCode", "PeriodCode", "period", "Period"));
    const fromDate = fieldOf(item, "fromDate", "FromDate");
    const toDate = fieldOf(item, "toDate", "ToDate");
    const status = upper(fieldOf(item, "status", "Status")) || "DRAFT";
    const submittedAt = fieldOf(item, "submittedAt", "SubmittedAt");
    const attachmentUrl = text(fieldOf(item, "attachmentUrl", "AttachmentUrl"));
    const note = text(fieldOf(item, "note", "Note"));
    const preparedBy = fieldOf(item, "preparedBy", "PreparedBy");
    const approvedBy = fieldOf(item, "approvedBy", "ApprovedBy");
    const periodEnd = normalizeDate(toDate);
    const isOverdueSubmission = Boolean(periodEnd && !submittedAt && (status === "DRAFT" || status === "READY") && periodEnd.getTime() < Date.now());

    return {
      id,
      taxType,
      periodCode: periodCode || "--",
      fromDate,
      toDate,
      status,
      submittedAt,
      attachmentUrl,
      note,
      preparedBy,
      approvedBy,
      isOverdueSubmission
    };
  });
}

function normalizeLegalDocuments(items) {
  return (items || []).map((item, index) => ({
    id: Number(fieldOf(item, "id", "Id")) || index + 1,
    docType: upper(fieldOf(item, "docType", "DocType")),
    referenceNo: text(fieldOf(item, "referenceNo", "ReferenceNo")),
    title: text(fieldOf(item, "title", "Title")),
    relatedEntityType: upper(fieldOf(item, "relatedEntityType", "RelatedEntityType")),
    relatedEntityId: Number(fieldOf(item, "relatedEntityId", "RelatedEntityId")) || null,
    issuedDate: fieldOf(item, "issuedDate", "IssuedDate"),
    fileUrl: text(fieldOf(item, "fileUrl", "FileUrl")),
    uploadedBy: fieldOf(item, "uploadedBy", "UploadedBy"),
    createdAt: fieldOf(item, "createdAt", "CreatedAt")
  }));
}

function normalizeAttachments(items) {
  return (items || []).map((item, index) => ({
    id: Number(fieldOf(item, "id", "Id")) || index + 1,
    entityType: upper(fieldOf(item, "entityType", "EntityType")),
    entityId: Number(fieldOf(item, "entityId", "EntityId")) || null,
    fileName: text(fieldOf(item, "fileName", "FileName")),
    fileUrl: text(fieldOf(item, "fileUrl", "FileUrl")),
    uploadedBy: fieldOf(item, "uploadedBy", "UploadedBy"),
    uploadedAt: fieldOf(item, "uploadedAt", "UploadedAt")
  }));
}

function normalizeInvoices(items) {
  return (items || [])
    .map((item, index) => {
      const id = Number(fieldOf(item, "id", "Id")) || index + 1;
      const serviceOrderId = fieldOf(item, "serviceOrderId", "ServiceOrderId");
      const retailOrderId = fieldOf(item, "retailOrderId", "RetailOrderId");
      return {
        id,
        taxAmount: toNumber(fieldOf(item, "taxAmount", "TaxAmount")),
        issuedAt: fieldOf(item, "issuedAt", "IssuedAt"),
        sourceLabel: serviceOrderId ? `SO-${serviceOrderId}` : retailOrderId ? `RO-${retailOrderId}` : "--"
      };
    })
    .sort((a, b) => Number(b.id || 0) - Number(a.id || 0));
}

export default function TaxDocumentsPage() {
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [taxFilings, setTaxFilings] = useState({ available: false, items: [] });
  const [legalDocuments, setLegalDocuments] = useState({ available: false, items: [] });
  const [attachments, setAttachments] = useState({ available: false, items: [] });
  const [invoices, setInvoices] = useState({ available: false, items: [] });

  const [taxSearch, setTaxSearch] = useState("");
  const [taxTypeFilter, setTaxTypeFilter] = useState("ALL");
  const [taxStatusFilter, setTaxStatusFilter] = useState("ALL");
  const [docSearch, setDocSearch] = useState("");
  const [attachmentEntityFilter, setAttachmentEntityFilter] = useState("ALL");

  const [taxPage, setTaxPage] = useState(1);
  const [taxPageSize, setTaxPageSize] = useState(10);
  const [legalPage, setLegalPage] = useState(1);
  const [legalPageSize, setLegalPageSize] = useState(10);
  const [attachmentPage, setAttachmentPage] = useState(1);
  const [attachmentPageSize, setAttachmentPageSize] = useState(10);

  useEffect(() => {
    let mounted = true;
    async function load() {
      setLoading(true);
      setError("");
      try {
        const [taxData, legalData, attachmentData] = await Promise.all([
          getTaxFilings(),
          getLegalDocuments(),
          getDocumentAttachments()
        ]);

        let invoiceSummary = null;
        let invoiceAvailable = false;
        try {
          invoiceSummary = await getInvoiceTaxSummary();
          invoiceAvailable = true;
        } catch {
          invoiceSummary = null;
          invoiceAvailable = false;
        }

        if (!mounted) return;
        setTaxFilings(taxData);
        setLegalDocuments(legalData);
        setAttachments(attachmentData);
        setInvoices({ available: invoiceAvailable, items: invoiceSummary?.items || [], totalTax: invoiceSummary?.totalTax, taxedCount: invoiceSummary?.taxedCount });
      } catch (err) {
        if (!mounted) return;
        setError(err?.message || "Không tải được dữ liệu thuế/chứng từ.");
      } finally {
        if (mounted) setLoading(false);
      }
    }
    load();
    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    setTaxPage(1);
  }, [taxSearch, taxTypeFilter, taxStatusFilter]);

  useEffect(() => {
    setLegalPage(1);
  }, [docSearch]);

  useEffect(() => {
    setAttachmentPage(1);
  }, [attachmentEntityFilter]);

  const taxRows = useMemo(() => normalizeTaxFilings(taxFilings.items), [taxFilings.items]);
  const legalRows = useMemo(() => normalizeLegalDocuments(legalDocuments.items), [legalDocuments.items]);
  const attachmentRows = useMemo(() => normalizeAttachments(attachments.items), [attachments.items]);
  const invoiceRows = useMemo(() => normalizeInvoices(invoices.items), [invoices.items]);
  const invoiceRowsWithTax = useMemo(() => invoiceRows.filter((item) => item.taxAmount !== 0), [invoiceRows]);

  const attachmentCountByTaxFiling = useMemo(() => {
    const map = new Map();
    for (const file of attachmentRows) {
      if (file.entityType !== "TAX_FILING" || !file.entityId) continue;
      map.set(file.entityId, (map.get(file.entityId) || 0) + 1);
    }
    return map;
  }, [attachmentRows]);

  const legalDocCountByTaxFiling = useMemo(() => {
    const map = new Map();
    for (const doc of legalRows) {
      if (doc.relatedEntityType !== "TAX_FILING" || !doc.relatedEntityId) continue;
      map.set(doc.relatedEntityId, (map.get(doc.relatedEntityId) || 0) + 1);
    }
    return map;
  }, [legalRows]);

  const enrichedTaxRows = useMemo(
    () =>
      taxRows.map((item) => {
        const linkedAttachmentCount = attachmentCountByTaxFiling.get(item.id) || 0;
        const directAttachmentCount = item.attachmentUrl ? 1 : 0;
        const legalDocCount = legalDocCountByTaxFiling.get(item.id) || 0;
        return {
          ...item,
          attachmentCount: linkedAttachmentCount + directAttachmentCount,
          legalDocCount
        };
      }),
    [taxRows, attachmentCountByTaxFiling, legalDocCountByTaxFiling]
  );

  const filteredTaxRows = useMemo(() => {
    return enrichedTaxRows.filter((item) => {
      if (taxTypeFilter !== "ALL" && item.taxType !== taxTypeFilter) return false;
      if (taxStatusFilter !== "ALL" && item.status !== taxStatusFilter) return false;
      if (!taxSearch) return true;
      const bag = [
        item.id,
        taxTypeLabel(item.taxType),
        item.periodCode,
        item.status,
        item.note
      ].join(" ");
      return containsIgnoreCase(bag, taxSearch);
    });
  }, [enrichedTaxRows, taxSearch, taxTypeFilter, taxStatusFilter]);

  const filteredLegalRows = useMemo(() => {
    if (!docSearch) return legalRows;
    return legalRows.filter((item) => {
      const bag = [
        item.id,
        docTypeLabel(item.docType),
        item.referenceNo,
        item.title,
        entityTypeLabel(item.relatedEntityType),
        item.relatedEntityId
      ].join(" ");
      return containsIgnoreCase(bag, docSearch);
    });
  }, [legalRows, docSearch]);

  const filteredAttachmentRows = useMemo(() => {
    return attachmentRows.filter((item) => {
      if (attachmentEntityFilter !== "ALL" && item.entityType !== attachmentEntityFilter) return false;
      return true;
    });
  }, [attachmentRows, attachmentEntityFilter]);

  const pagedTaxRows = useMemo(() => {
    if (!filteredTaxRows.length) return [];
    const start = (taxPage - 1) * taxPageSize;
    const end = Math.min(start + taxPageSize, filteredTaxRows.length);
    const rows = [];
    for (let i = start; i < end; i++) rows.push(filteredTaxRows[i]);
    return rows;
  }, [filteredTaxRows, taxPage, taxPageSize]);

  const pagedLegalRows = useMemo(() => {
    if (!filteredLegalRows.length) return [];
    const start = (legalPage - 1) * legalPageSize;
    const end = Math.min(start + legalPageSize, filteredLegalRows.length);
    const rows = [];
    for (let i = start; i < end; i++) rows.push(filteredLegalRows[i]);
    return rows;
  }, [filteredLegalRows, legalPage, legalPageSize]);

  const pagedAttachmentRows = useMemo(() => {
    if (!filteredAttachmentRows.length) return [];
    const start = (attachmentPage - 1) * attachmentPageSize;
    const end = Math.min(start + attachmentPageSize, filteredAttachmentRows.length);
    const rows = [];
    for (let i = start; i < end; i++) rows.push(filteredAttachmentRows[i]);
    return rows;
  }, [filteredAttachmentRows, attachmentPage, attachmentPageSize]);

  const summary = useMemo(() => {
    const needActionCount = enrichedTaxRows.filter((item) => item.status === "DRAFT" || item.status === "READY" || item.status === "REJECTED").length;
    const waitingResultCount = enrichedTaxRows.filter((item) => item.status === "SUBMITTED").length;
    const acceptedCount = enrichedTaxRows.filter((item) => item.status === "ACCEPTED").length;
    const overdueSubmissionCount = enrichedTaxRows.filter((item) => item.isOverdueSubmission).length;
    const missingDocumentsCount = enrichedTaxRows.filter((item) => item.legalDocCount <= 0 || item.attachmentCount <= 0).length;
    const invoiceTaxTotal = invoices.available ? Number(invoices.totalTax) : null;
    return {
      totalTaxFilings: enrichedTaxRows.length,
      needActionCount,
      waitingResultCount,
      acceptedCount,
      overdueSubmissionCount,
      missingDocumentsCount,
      invoiceTaxTotal,
      invoiceTaxInvoiceCount: invoices.available ? Number(invoices.taxedCount) : null
    };
  }, [enrichedTaxRows, invoices.available, invoices.totalTax, invoices.taxedCount]);

  if (loading) {
    return (
      <Flex minH='260px' align='center' justify='center' direction='column' gap='10px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dữ liệu thuế/chứng từ...</Text>
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

      <SimpleGrid columns={{ base: 1, md: 2, xl: 5 }} gap='20px' mb='20px'>
        <MiniStatistics name='Hồ sơ kê khai' value={String(summary.totalTaxFilings)} />
        <MiniStatistics name='Cần xử lý' value={String(summary.needActionCount)} />
        <MiniStatistics name='Chờ phản hồi' value={String(summary.waitingResultCount)} />
        <MiniStatistics name='Đủ điều kiện quyết toán' value={String(summary.acceptedCount)} />
        <MiniStatistics name='Tổng thuế hóa đơn' value={invoices.available ? formatCurrency(summary.invoiceTaxTotal) : "--"} />
      </SimpleGrid>

      <Card p='16px' mb='20px'>
        <Flex wrap='wrap' gap='10px' align='center'>
          <Badge colorScheme={taxFilings.available ? "green" : "orange"}>
            API TaxFiling: {taxFilings.available ? "OK" : "Chưa có"}
          </Badge>
          <Badge colorScheme={legalDocuments.available ? "green" : "orange"}>
            API LegalDocument: {legalDocuments.available ? "OK" : "Chưa có"}
          </Badge>
          <Badge colorScheme={attachments.available ? "green" : "orange"}>
            API DocumentAttachment: {attachments.available ? "OK" : "Chưa có"}
          </Badge>
          <Badge colorScheme={invoices.available ? "green" : "orange"}>
            API Invoices: {invoices.available ? "OK" : "Chưa có"}
          </Badge>
        </Flex>
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>Cảnh báo nghiệp vụ</Text>
          <Flex direction='column' gap='8px'>
            <Text color={textColorSecondary}>- Hồ sơ quá hạn nộp: {summary.overdueSubmissionCount}</Text>
            <Text color={textColorSecondary}>- Hồ sơ thiếu chứng từ pháp lý hoặc tệp đính kèm: {summary.missingDocumentsCount}</Text>
            <Text color={textColorSecondary}>- Hồ sơ chờ phản hồi cơ quan thuế: {summary.waitingResultCount}</Text>
          </Flex>
        </Card>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>Đối soát dữ liệu</Text>
          <Flex direction='column' gap='8px'>
            <Text color={textColorSecondary}>- Chứng từ pháp lý (Legal documents): {legalRows.length}</Text>
            <Text color={textColorSecondary}>- Tệp đính kèm (Attachments): {attachmentRows.length}</Text>
            <Text color={textColorSecondary}>- Hóa đơn có phát sinh thuế: {summary.invoiceTaxInvoiceCount ?? "--"}</Text>
            <Text color={textColorSecondary}>- Tổng thuế từ hóa đơn: {invoices.available ? formatCurrency(summary.invoiceTaxTotal) : "--"}</Text>
            <Text color={textColorSecondary}>
              - Hồ sơ thuế có đủ pháp lý và đính kèm: {Math.max(enrichedTaxRows.length - summary.missingDocumentsCount, 0)}
            </Text>
          </Flex>
        </Card>
      </SimpleGrid>

      <Card p='20px' mb='20px'>
        <Flex justify='space-between' align='center' mb='12px'>
          <Text color={textColor} fontWeight='700'>Thuế từ danh sách hóa đơn</Text>
          <Text color={textColorSecondary}>
            Tổng thuế: <Text as='span' color={textColor} fontWeight='700'>{invoices.available ? formatCurrency(summary.invoiceTaxTotal) : "--"}</Text>
          </Text>
        </Flex>
        {!invoices.available ? <Text color={textColorSecondary}>--</Text> : invoiceRowsWithTax.length ? (
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Hóa đơn</Th>
                <Th>Nguồn</Th>
                <Th>Ngày lập</Th>
                <Th isNumeric>Thuế</Th>
              </Tr>
            </Thead>
            <Tbody>
              {invoiceRowsWithTax.map((item) => (
                <Tr key={item.id}>
                  <Td color={textColor} fontWeight='600'>{`INV-${item.id}`}</Td>
                  <Td color={textColorSecondary}>{item.sourceLabel}</Td>
                  <Td color={textColorSecondary}>{formatDateTime(item.issuedAt)}</Td>
                  <Td color={textColor} isNumeric>{formatCurrency(item.taxAmount)}</Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        ) : (
          <Text color={textColorSecondary}>Chưa có hóa đơn phát sinh thuế.</Text>
        )}
      </Card>

      <Card p='20px' mb='20px'>
        <Flex justify='space-between' align={{ base: "stretch", lg: "center" }} direction={{ base: "column", lg: "row" }} gap='10px' mb='12px'>
          <Text color={textColor} fontWeight='700'>Danh sách hồ sơ thuế</Text>
          <Flex gap='10px' wrap='wrap'>
            <Input
              value={taxSearch}
              onChange={(e) => setTaxSearch(e.target.value)}
              placeholder='Tìm theo kỳ, trạng thái, ghi chú'
              maxW='260px'
            />
            <Select value={taxTypeFilter} onChange={(e) => setTaxTypeFilter(e.target.value)} maxW='160px'>
              <option value='ALL'>Tất cả loại thuế</option>
              <option value='VAT'>VAT</option>
              <option value='PIT'>PIT</option>
              <option value='CIT'>CIT</option>
              <option value='INSURANCE'>BHXH/BHYT/BHTN</option>
            </Select>
            <Select value={taxStatusFilter} onChange={(e) => setTaxStatusFilter(e.target.value)} maxW='170px'>
              <option value='ALL'>Tất cả trạng thái</option>
              <option value='DRAFT'>Nháp</option>
              <option value='READY'>Sẵn sàng nộp</option>
              <option value='SUBMITTED'>Đã nộp</option>
              <option value='ACCEPTED'>Đã chấp nhận</option>
              <option value='REJECTED'>Bị từ chối</option>
            </Select>
          </Flex>
        </Flex>

        {filteredTaxRows.length ? (
          <Box>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Mã hồ sơ</Th>
                  <Th>Loại thuế</Th>
                  <Th>Kỳ khai</Th>
                  <Th>Khoảng thời gian</Th>
                  <Th>Trạng thái</Th>
                  <Th>Ngày nộp</Th>
                  <Th>Hồ sơ pháp lý</Th>
                  <Th>Đính kèm</Th>
                </Tr>
              </Thead>
              <Tbody>
                {pagedTaxRows.map((item) => {
                  const statusMeta = taxStatusMeta(item.status);
                  const riskColor = item.isOverdueSubmission ? "red" : statusMeta.colorScheme;
                  return (
                    <Tr key={item.id}>
                      <Td color={textColor} fontWeight='600'>{`${item.taxType || "TAX"}-${item.periodCode}`}</Td>
                      <Td color={textColorSecondary}>{taxTypeLabel(item.taxType)}</Td>
                      <Td color={textColorSecondary}>{item.periodCode}</Td>
                      <Td color={textColorSecondary}>{formatPeriodRange(item.fromDate, item.toDate)}</Td>
                      <Td>
                        <Badge colorScheme={riskColor}>
                          {item.isOverdueSubmission ? "Quá hạn nộp" : statusMeta.label}
                        </Badge>
                      </Td>
                      <Td color={textColorSecondary}>{formatDateTime(item.submittedAt)}</Td>
                      <Td>
                        <Badge colorScheme={item.legalDocCount > 0 ? "green" : "orange"}>
                          {item.legalDocCount > 0 ? `${item.legalDocCount} chứng từ` : "Thiếu"}
                        </Badge>
                      </Td>
                      <Td>
                        <Badge colorScheme={item.attachmentCount > 0 ? "green" : "orange"}>
                          {item.attachmentCount > 0 ? `${item.attachmentCount} tệp` : "Thiếu"}
                        </Badge>
                      </Td>
                    </Tr>
                  );
                })}
              </Tbody>
            </Table>
            <TablePagination
              totalItems={filteredTaxRows.length}
              currentPage={taxPage}
              pageSize={taxPageSize}
              onPageChange={setTaxPage}
              onPageSizeChange={(size) => {
                setTaxPageSize(size);
                setTaxPage(1);
              }}
            />
          </Box>
        ) : (
          <Text color={textColorSecondary}>Không có hồ sơ thuế phù hợp với bộ lọc hiện tại.</Text>
        )}
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px'>
        <Card p='20px'>
          <Flex justify='space-between' align={{ base: "stretch", lg: "center" }} direction={{ base: "column", lg: "row" }} gap='10px' mb='12px'>
            <Text color={textColor} fontWeight='700'>Chứng từ pháp lý</Text>
            <Input
              value={docSearch}
              onChange={(e) => setDocSearch(e.target.value)}
              placeholder='Tìm theo số chứng từ / tiêu đề / liên kết'
              maxW='300px'
            />
          </Flex>
          {filteredLegalRows.length ? (
            <Box>
              <Table size='sm'>
                <Thead>
                  <Tr>
                    <Th>Tham chiếu</Th>
                    <Th>Loại</Th>
                    <Th>Tiêu đề</Th>
                    <Th>Liên quan</Th>
                    <Th>Ngày hiệu lực</Th>
                  </Tr>
                </Thead>
                <Tbody>
                  {pagedLegalRows.map((item) => (
                    <Tr key={item.id}>
                      <Td color={textColor} fontWeight='600'>{item.referenceNo || `DOC-${item.id}`}</Td>
                      <Td color={textColorSecondary}>{docTypeLabel(item.docType)}</Td>
                      <Td color={textColorSecondary}>{item.title || "--"}</Td>
                      <Td color={textColorSecondary}>{`${entityTypeLabel(item.relatedEntityType)} #${item.relatedEntityId || "--"}`}</Td>
                      <Td color={textColorSecondary}>{formatDate(item.issuedDate || item.createdAt)}</Td>
                    </Tr>
                  ))}
                </Tbody>
              </Table>
              <TablePagination
                totalItems={filteredLegalRows.length}
                currentPage={legalPage}
                pageSize={legalPageSize}
                onPageChange={setLegalPage}
                onPageSizeChange={(size) => {
                  setLegalPageSize(size);
                  setLegalPage(1);
                }}
              />
            </Box>
          ) : (
            <Text color={textColorSecondary}>Không có chứng từ pháp lý phù hợp.</Text>
          )}
        </Card>

        <Card p='20px'>
          <Flex justify='space-between' align={{ base: "stretch", lg: "center" }} direction={{ base: "column", lg: "row" }} gap='10px' mb='12px'>
            <Text color={textColor} fontWeight='700'>Tệp đính kèm</Text>
            <Select value={attachmentEntityFilter} onChange={(e) => setAttachmentEntityFilter(e.target.value)} maxW='240px'>
              <option value='ALL'>Tất cả loại liên kết</option>
              <option value='TAX_FILING'>Hồ sơ thuế</option>
              <option value='INVOICE'>Hóa đơn</option>
              <option value='PAYMENT'>Thanh toán</option>
              <option value='SUPPLIER_INVOICE'>Hóa đơn NCC</option>
              <option value='PAYROLL_RUN'>Đợt lương</option>
              <option value='PAYROLL_ITEM'>Phiếu lương</option>
              <option value='FINANCE_APPROVAL_REQUEST'>Yêu cầu phê duyệt</option>
              <option value='OTHER'>Khác</option>
            </Select>
          </Flex>
          {filteredAttachmentRows.length ? (
            <Box>
              <Table size='sm'>
                <Thead>
                  <Tr>
                    <Th>Tên tệp</Th>
                    <Th>Loại liên kết</Th>
                    <Th>Entity ID</Th>
                    <Th>Người tải</Th>
                    <Th>Ngày tải</Th>
                  </Tr>
                </Thead>
                <Tbody>
                  {pagedAttachmentRows.map((item) => (
                    <Tr key={item.id}>
                      <Td color={textColor} fontWeight='600'>{item.fileName || `FILE-${item.id}`}</Td>
                      <Td color={textColorSecondary}>{entityTypeLabel(item.entityType)}</Td>
                      <Td color={textColorSecondary}>{item.entityId || "--"}</Td>
                      <Td color={textColorSecondary}>{item.uploadedBy || "--"}</Td>
                      <Td color={textColorSecondary}>{formatDateTime(item.uploadedAt)}</Td>
                    </Tr>
                  ))}
                </Tbody>
              </Table>
              <TablePagination
                totalItems={filteredAttachmentRows.length}
                currentPage={attachmentPage}
                pageSize={attachmentPageSize}
                onPageChange={setAttachmentPage}
                onPageSizeChange={(size) => {
                  setAttachmentPageSize(size);
                  setAttachmentPage(1);
                }}
              />
            </Box>
          ) : (
            <Text color={textColorSecondary}>Không có tệp đính kèm phù hợp.</Text>
          )}
        </Card>
      </SimpleGrid>
    </Box>
  );
}
