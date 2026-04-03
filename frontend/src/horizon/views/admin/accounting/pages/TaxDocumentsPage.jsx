import React, { useEffect, useState } from "react";
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
  getDocumentAttachments,
  getLegalDocuments,
  getTaxFilings
} from "../api/accountingApi";
import { formatDate } from "../utils/accountingHelpers";

function fieldOf(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

export default function TaxDocumentsPage() {
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [taxFilings, setTaxFilings] = useState({ available: false, items: [] });
  const [legalDocuments, setLegalDocuments] = useState({ available: false, items: [] });
  const [attachments, setAttachments] = useState({ available: false, items: [] });

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
        if (!mounted) return;
        setTaxFilings(taxData);
        setLegalDocuments(legalData);
        setAttachments(attachmentData);
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

      <SimpleGrid columns={{ base: 1, md: 2, lg: 3 }} gap='20px' mb='20px'>
        <MiniStatistics name='Tax filings' value={String((taxFilings.items || []).length)} />
        <MiniStatistics name='Legal documents' value={String((legalDocuments.items || []).length)} />
        <MiniStatistics name='Attachments' value={String((attachments.items || []).length)} />
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
        </Flex>
      </Card>

      <Card p='20px'>
        {(taxFilings.items || []).length ? (
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Filing code</Th>
                <Th>Loại</Th>
                <Th>Kỳ</Th>
                <Th>Status</Th>
                <Th>Ngày nộp</Th>
              </Tr>
            </Thead>
            <Tbody>
              {(taxFilings.items || []).slice(0, 15).map((filing, idx) => (
                <Tr key={fieldOf(filing, "id", "Id") || idx}>
                  <Td color={textColor} fontWeight='600'>
                    {fieldOf(filing, "filingCode", "FilingCode", "code", "Code") || `Filing #${idx + 1}`}
                  </Td>
                  <Td color={textColorSecondary}>
                    {fieldOf(filing, "filingType", "FilingType", "type", "Type") || "--"}
                  </Td>
                  <Td color={textColorSecondary}>
                    {fieldOf(filing, "periodCode", "PeriodCode", "period", "Period") || "--"}
                  </Td>
                  <Td>
                    <Badge colorScheme='blue'>{fieldOf(filing, "status", "Status") || "PENDING"}</Badge>
                  </Td>
                  <Td color={textColorSecondary}>
                    {formatDate(fieldOf(filing, "submittedAt", "SubmittedAt", "createdAt", "CreatedAt"))}
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        ) : (
          <Text color={textColorSecondary}>
            Chưa có endpoint dữ liệu thuế/chứng từ trên backend. Trang đã sẵn để gắn hồ sơ VAT/PIT/BHXH và đính kèm pháp lý khi API được mở.
          </Text>
        )}
      </Card>
    </Box>
  );
}
