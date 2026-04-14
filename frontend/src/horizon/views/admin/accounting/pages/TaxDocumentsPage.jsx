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
import { getDocumentAttachments, getInvoices, getLegalDocuments, getTaxFilings } from "../api/accountingApi";
import { formatCurrency, formatDate, formatDateTime, normalizeDate, toNumber } from "../utils/accountingHelpers";

const TAX_TYPE_LABELS = {
  VAT: "VAT",
  PIT: "PIT",
  CIT: "CIT",
  INSURANCE: "BHXH/BHYT/BHTN"
};

const TAX_STATUS_META = {
  DRAFT: { label: "Nhap", colorScheme: "gray" },
  READY: { label: "San sang nop", colorScheme: "orange" },
  SUBMITTED: { label: "Da nop cho phan hoi", colorScheme: "blue" },
  ACCEPTED: { label: "Da chap nhan", colorScheme: "green" },
  REJECTED: { label: "Bi tu choi", colorScheme: "red" }
};

const DOC_TYPE_LABELS = {
  PAYMENT_PROOF: "Chung tu thanh toan",
  INVOICE_FILE: "File hoa don",
  REFUND_PROOF: "Chung tu hoan tien",
  PAYROLL_APPROVAL: "Phe duyet luong",
  TAX_RECEIPT: "Bien lai thue",
  SUPPLIER_INVOICE: "Hoa don NCC",
  LABOR_CONTRACT: "Hop dong lao dong",
  OTHER: "Khac"
};

const ENTITY_TYPE_LABELS = {
  TAX_FILING: "Ho so thue",
  INVOICE: "Hoa don",
  PAYMENT: "Thanh toan",
  PAYROLL_RUN: "Dot luong",
  PAYROLL_ITEM: "Phieu luong",
  SUPPLIER_INVOICE: "Hoa don NCC",
  FINANCE_APPROVAL_REQUEST: "Yeu cau phe duyet tai chinh",
  OTHER: "Khac"
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
  return TAX_STATUS_META[code] || { label: code || "Chua xac dinh", colorScheme: "gray" };
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

        let invoiceItems = [];
        let invoiceAvailable = false;
        try {
          invoiceItems = await getInvoices();
          invoiceAvailable = true;
        } catch {
          invoiceItems = [];
          invoiceAvailable = false;
        }

        if (!mounted) return;
        setTaxFilings(taxData);
        setLegalDocuments(legalData);
        setAttachments(attachmentData);
        setInvoices({ available: invoiceAvailable, items: invoiceItems || [] });
      } catch (err) {
        if (!mounted) return;
        setError(err?.message || "Khong tai duoc du lieu thue/chung tu.");
      } finally {
        if (mounted) setLoading(false);
      }
    }
    load();
    return () => {
      mounted = false;
    };
  }, []);

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

  const summary = useMemo(() => {
    const needActionCount = enrichedTaxRows.filter((item) => item.status === "DRAFT" || item.status === "READY" || item.status === "REJECTED").length;
    const waitingResultCount = enrichedTaxRows.filter((item) => item.status === "SUBMITTED").length;
    const acceptedCount = enrichedTaxRows.filter((item) => item.status === "ACCEPTED").length;
    const overdueSubmissionCount = enrichedTaxRows.filter((item) => item.isOverdueSubmission).length;
    const missingDocumentsCount = enrichedTaxRows.filter((item) => item.legalDocCount <= 0 || item.attachmentCount <= 0).length;
    const invoiceTaxTotal = invoiceRows.reduce((total, item) => total + toNumber(item.taxAmount), 0);
    return {
      totalTaxFilings: enrichedTaxRows.length,
      needActionCount,
      waitingResultCount,
      acceptedCount,
      overdueSubmissionCount,
      missingDocumentsCount,
      invoiceTaxTotal,
      invoiceTaxInvoiceCount: invoiceRowsWithTax.length
    };
  }, [enrichedTaxRows, invoiceRows, invoiceRowsWithTax.length]);

  if (loading) {
    return (
      <Flex minH='260px' align='center' justify='center' direction='column' gap='10px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Dang tai du lieu thue/chung tu...</Text>
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
        <MiniStatistics name='Ho so ke khai' value={String(summary.totalTaxFilings)} />
        <MiniStatistics name='Can xu ly' value={String(summary.needActionCount)} />
        <MiniStatistics name='Cho phan hoi' value={String(summary.waitingResultCount)} />
        <MiniStatistics name='Du dieu kien quyet toan' value={String(summary.acceptedCount)} />
        <MiniStatistics name='Tong thue hoa don' value={formatCurrency(summary.invoiceTaxTotal)} />
      </SimpleGrid>

      <Card p='16px' mb='20px'>
        <Flex wrap='wrap' gap='10px' align='center'>
          <Badge colorScheme={taxFilings.available ? "green" : "orange"}>
            API TaxFiling: {taxFilings.available ? "OK" : "Chua co"}
          </Badge>
          <Badge colorScheme={legalDocuments.available ? "green" : "orange"}>
            API LegalDocument: {legalDocuments.available ? "OK" : "Chua co"}
          </Badge>
          <Badge colorScheme={attachments.available ? "green" : "orange"}>
            API DocumentAttachment: {attachments.available ? "OK" : "Chua co"}
          </Badge>
          <Badge colorScheme={invoices.available ? "green" : "orange"}>
            API Invoices: {invoices.available ? "OK" : "Chua co"}
          </Badge>
        </Flex>
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>Canh bao nghiep vu</Text>
          <Flex direction='column' gap='8px'>
            <Text color={textColorSecondary}>- Ho so qua han nop: {summary.overdueSubmissionCount}</Text>
            <Text color={textColorSecondary}>- Ho so thieu chung tu phap ly hoac tep dinh kem: {summary.missingDocumentsCount}</Text>
            <Text color={textColorSecondary}>- Ho so cho phan hoi co quan thue: {summary.waitingResultCount}</Text>
          </Flex>
        </Card>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>Doi soat du lieu</Text>
          <Flex direction='column' gap='8px'>
            <Text color={textColorSecondary}>- Legal documents: {legalRows.length}</Text>
            <Text color={textColorSecondary}>- Attachments: {attachmentRows.length}</Text>
            <Text color={textColorSecondary}>- Hoa don co phat sinh thue: {summary.invoiceTaxInvoiceCount}</Text>
            <Text color={textColorSecondary}>- Tong thue tu hoa don: {formatCurrency(summary.invoiceTaxTotal)}</Text>
            <Text color={textColorSecondary}>
              - Ho so thue co du phap ly va dinh kem: {Math.max(enrichedTaxRows.length - summary.missingDocumentsCount, 0)}
            </Text>
          </Flex>
        </Card>
      </SimpleGrid>

      <Card p='20px' mb='20px'>
        <Flex justify='space-between' align='center' mb='12px'>
          <Text color={textColor} fontWeight='700'>Thuế từ danh sách hóa đơn</Text>
          <Text color={textColorSecondary}>
            Tổng thuế: <Text as='span' color={textColor} fontWeight='700'>{formatCurrency(summary.invoiceTaxTotal)}</Text>
          </Text>
        </Flex>
        {invoiceRowsWithTax.length ? (
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Hoa don</Th>
                <Th>Nguon</Th>
                <Th>Ngay lap</Th>
                <Th isNumeric>Thue</Th>
              </Tr>
            </Thead>
            <Tbody>
              {invoiceRowsWithTax.slice(0, 30).map((item) => (
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
          <Text color={textColorSecondary}>Chua co hoa don phat sinh thue.</Text>
        )}
      </Card>

      <Card p='20px' mb='20px'>
        <Flex justify='space-between' align={{ base: "stretch", lg: "center" }} direction={{ base: "column", lg: "row" }} gap='10px' mb='12px'>
          <Text color={textColor} fontWeight='700'>Danh sach ho so thue</Text>
          <Flex gap='10px' wrap='wrap'>
            <Input
              value={taxSearch}
              onChange={(e) => setTaxSearch(e.target.value)}
              placeholder='Tim theo ky, trang thai, ghi chu'
              maxW='260px'
            />
            <Select value={taxTypeFilter} onChange={(e) => setTaxTypeFilter(e.target.value)} maxW='160px'>
              <option value='ALL'>Tat ca loai thue</option>
              <option value='VAT'>VAT</option>
              <option value='PIT'>PIT</option>
              <option value='CIT'>CIT</option>
              <option value='INSURANCE'>BHXH/BHYT/BHTN</option>
            </Select>
            <Select value={taxStatusFilter} onChange={(e) => setTaxStatusFilter(e.target.value)} maxW='170px'>
              <option value='ALL'>Tat ca trang thai</option>
              <option value='DRAFT'>Nhap</option>
              <option value='READY'>San sang nop</option>
              <option value='SUBMITTED'>Da nop</option>
              <option value='ACCEPTED'>Da chap nhan</option>
              <option value='REJECTED'>Bi tu choi</option>
            </Select>
          </Flex>
        </Flex>

        {filteredTaxRows.length ? (
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Ma ho so</Th>
                <Th>Loai thue</Th>
                <Th>Ky khai</Th>
                <Th>Khoang thoi gian</Th>
                <Th>Trang thai</Th>
                <Th>Ngay nop</Th>
                <Th>Ho so phap ly</Th>
                <Th>Dinh kem</Th>
              </Tr>
            </Thead>
            <Tbody>
              {filteredTaxRows.map((item) => {
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
                        {item.isOverdueSubmission ? "Qua han nop" : statusMeta.label}
                      </Badge>
                    </Td>
                    <Td color={textColorSecondary}>{formatDateTime(item.submittedAt)}</Td>
                    <Td>
                      <Badge colorScheme={item.legalDocCount > 0 ? "green" : "orange"}>
                        {item.legalDocCount > 0 ? `${item.legalDocCount} chung tu` : "Thieu"}
                      </Badge>
                    </Td>
                    <Td>
                      <Badge colorScheme={item.attachmentCount > 0 ? "green" : "orange"}>
                        {item.attachmentCount > 0 ? `${item.attachmentCount} tep` : "Thieu"}
                      </Badge>
                    </Td>
                  </Tr>
                );
              })}
            </Tbody>
          </Table>
        ) : (
          <Text color={textColorSecondary}>Khong co ho so thue phu hop voi bo loc hien tai.</Text>
        )}
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px'>
        <Card p='20px'>
          <Flex justify='space-between' align={{ base: "stretch", lg: "center" }} direction={{ base: "column", lg: "row" }} gap='10px' mb='12px'>
            <Text color={textColor} fontWeight='700'>Chung tu phap ly</Text>
            <Input
              value={docSearch}
              onChange={(e) => setDocSearch(e.target.value)}
              placeholder='Tim theo so chung tu / tieu de / lien ket'
              maxW='300px'
            />
          </Flex>
          {filteredLegalRows.length ? (
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Tham chieu</Th>
                  <Th>Loai</Th>
                  <Th>Tieu de</Th>
                  <Th>Lien quan</Th>
                  <Th>Ngay hieu luc</Th>
                </Tr>
              </Thead>
              <Tbody>
                {filteredLegalRows.slice(0, 20).map((item) => (
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
          ) : (
            <Text color={textColorSecondary}>Khong co chung tu phap ly phu hop.</Text>
          )}
        </Card>

        <Card p='20px'>
          <Flex justify='space-between' align={{ base: "stretch", lg: "center" }} direction={{ base: "column", lg: "row" }} gap='10px' mb='12px'>
            <Text color={textColor} fontWeight='700'>Tep dinh kem</Text>
            <Select value={attachmentEntityFilter} onChange={(e) => setAttachmentEntityFilter(e.target.value)} maxW='240px'>
              <option value='ALL'>Tat ca loai lien ket</option>
              <option value='TAX_FILING'>Ho so thue</option>
              <option value='INVOICE'>Hoa don</option>
              <option value='PAYMENT'>Thanh toan</option>
              <option value='SUPPLIER_INVOICE'>Hoa don NCC</option>
              <option value='PAYROLL_RUN'>Dot luong</option>
              <option value='PAYROLL_ITEM'>Phieu luong</option>
              <option value='FINANCE_APPROVAL_REQUEST'>Yeu cau phe duyet</option>
              <option value='OTHER'>Khac</option>
            </Select>
          </Flex>
          {filteredAttachmentRows.length ? (
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Ten tep</Th>
                  <Th>Loai lien ket</Th>
                  <Th>Entity ID</Th>
                  <Th>Nguoi tai</Th>
                  <Th>Ngay tai</Th>
                </Tr>
              </Thead>
              <Tbody>
                {filteredAttachmentRows.slice(0, 20).map((item) => (
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
          ) : (
            <Text color={textColorSecondary}>Khong co tep dinh kem phu hop.</Text>
          )}
        </Card>
      </SimpleGrid>
    </Box>
  );
}
