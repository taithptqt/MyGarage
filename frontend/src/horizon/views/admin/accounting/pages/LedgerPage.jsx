import React, { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Box,
  Button,
  Flex,
  FormControl,
  FormLabel,
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
  getAccountingPeriods,
  getChartOfAccounts,
  getJournalEntries,
  getJournalEntryLines,
  postJournalEntry
} from "../api/accountingApi";
import {
  formatCurrency,
  formatDate,
  formatDateTime,
  getCurrentUserId,
  sumBy,
  toNumber
} from "../utils/accountingHelpers";

function fieldOf(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

export default function LedgerPage() {
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [periods, setPeriods] = useState({ available: false, items: [] });
  const [accounts, setAccounts] = useState({ available: false, items: [] });
  const [entries, setEntries] = useState({ available: false, items: [] });
  const [lines, setLines] = useState({ available: false, items: [] });

  const [draftAccount, setDraftAccount] = useState("");
  const [draftDebit, setDraftDebit] = useState("");
  const [draftCredit, setDraftCredit] = useState("");
  const [draftNote, setDraftNote] = useState("");
  const [draftLines, setDraftLines] = useState([]);
  const [postingId, setPostingId] = useState(null);

  useEffect(() => {
    let mounted = true;
    async function load() {
      setLoading(true);
      setError("");
      try {
        const [periodData, accountData, entryData, lineData] = await Promise.all([
          getAccountingPeriods(),
          getChartOfAccounts(),
          getJournalEntries(),
          getJournalEntryLines()
        ]);
        if (!mounted) return;
        setPeriods(periodData);
        setAccounts(accountData);
        setEntries(entryData);
        setLines(lineData);
      } catch (err) {
        if (!mounted) return;
        setError(err?.message || "Không tải được dữ liệu sổ cái.");
      } finally {
        if (mounted) setLoading(false);
      }
    }
    load();
    return () => {
      mounted = false;
    };
  }, []);

  async function refreshEntries() {
    const [entryData] = await Promise.all([getJournalEntries()]);
    setEntries(entryData);
  }

  const openPeriod = useMemo(
    () =>
      (periods.items || []).find((item) =>
        String(fieldOf(item, "status", "Status") || "").toUpperCase().includes("OPEN")
      ),
    [periods.items]
  );

  const postedEntries = useMemo(
    () =>
      (entries.items || []).filter((item) =>
        String(fieldOf(item, "status", "Status") || "").toUpperCase() === "POSTED"
      ),
    [entries.items]
  );

  const draftTotals = useMemo(() => {
    const totalDebit = sumBy(draftLines, (item) => toNumber(item.debit));
    const totalCredit = sumBy(draftLines, (item) => toNumber(item.credit));
    return {
      totalDebit,
      totalCredit,
      balanced: Math.abs(totalDebit - totalCredit) < 0.0001
    };
  }, [draftLines]);

  function addDraftLine() {
    if (!draftAccount.trim()) return;
    const debit = toNumber(draftDebit);
    const credit = toNumber(draftCredit);
    if (debit <= 0 && credit <= 0) return;
    setDraftLines((prev) => [
      ...prev,
      { account: draftAccount.trim(), debit, credit, note: draftNote.trim() }
    ]);
    setDraftAccount("");
    setDraftDebit("");
    setDraftCredit("");
    setDraftNote("");
  }

  function removeDraftLine(index) {
    setDraftLines((prev) => prev.filter((_, i) => i !== index));
  }

  async function handlePostJournal(entryId) {
    const userId = getCurrentUserId();
    if (!userId) {
      setError("Không xác định được user hiện tại để post journal.");
      return;
    }
    setPostingId(entryId);
    try {
      await postJournalEntry(entryId, userId);
      await refreshEntries();
    } catch (err) {
      setError(err?.message || "Post journal thất bại.");
    } finally {
      setPostingId(null);
    }
  }

  if (loading) {
    return (
      <Flex minH='260px' align='center' justify='center' direction='column' gap='10px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dữ liệu sổ cái...</Text>
      </Flex>
    );
  }

  return (
    <Box>
      <Box mb='20px'>
        <Text color={textColor} fontSize='2xl' fontWeight='700'>Sổ cái / Ledger</Text>
        <Text color={textColorSecondary} fontSize='sm'>
          Quản lý kỳ kế toán, bút toán và kiểm tra cân debit = credit trước khi post.
        </Text>
      </Box>

      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, lg: 4 }} gap='20px' mb='20px'>
        <MiniStatistics
          name='Kỳ kế toán mở'
          value={openPeriod ? String(fieldOf(openPeriod, "periodCode", "PeriodCode", "name", "Name") || "OPEN") : "Chưa có"}
        />
        <MiniStatistics name='Chart of accounts' value={String((accounts.items || []).length)} />
        <MiniStatistics name='Journal entries' value={String((entries.items || []).length)} />
        <MiniStatistics name='Journal POSTED' value={String(postedEntries.length)} />
      </SimpleGrid>

      <Card p='16px' mb='20px'>
        <Flex wrap='wrap' gap='10px' align='center'>
          <Badge colorScheme={periods.available ? "green" : "orange"}>
            API AccountingPeriod: {periods.available ? "OK" : "Chưa có"}
          </Badge>
          <Badge colorScheme={accounts.available ? "green" : "orange"}>
            API ChartOfAccounts: {accounts.available ? "OK" : "Chưa có"}
          </Badge>
          <Badge colorScheme={entries.available ? "green" : "orange"}>
            API JournalEntry: {entries.available ? "OK" : "Chưa có"}
          </Badge>
          <Badge colorScheme={lines.available ? "green" : "orange"}>
            API JournalEntryLine: {lines.available ? "OK" : "Chưa có"}
          </Badge>
        </Flex>
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px'>
        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>Manual journal draft (kiểm tra cân bằng)</Text>
          <SimpleGrid columns={{ base: 1, md: 2 }} gap='10px' mb='10px'>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Account code</FormLabel>
              <Input value={draftAccount} onChange={(e) => setDraftAccount(e.target.value)} placeholder='VD: 1111' />
            </FormControl>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Note</FormLabel>
              <Input value={draftNote} onChange={(e) => setDraftNote(e.target.value)} placeholder='Nội dung dòng' />
            </FormControl>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Debit</FormLabel>
              <Input type='number' value={draftDebit} onChange={(e) => setDraftDebit(e.target.value)} />
            </FormControl>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Credit</FormLabel>
              <Input type='number' value={draftCredit} onChange={(e) => setDraftCredit(e.target.value)} />
            </FormControl>
          </SimpleGrid>
          <Button size='sm' colorScheme='blue' onClick={addDraftLine}>Thêm dòng</Button>

          <Box mt='14px'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Account</Th>
                  <Th isNumeric>Debit</Th>
                  <Th isNumeric>Credit</Th>
                  <Th>Note</Th>
                  <Th></Th>
                </Tr>
              </Thead>
              <Tbody>
                {draftLines.map((line, index) => (
                  <Tr key={`${line.account}-${index}`}>
                    <Td>{line.account}</Td>
                    <Td isNumeric>{formatCurrency(line.debit)}</Td>
                    <Td isNumeric>{formatCurrency(line.credit)}</Td>
                    <Td>{line.note || "--"}</Td>
                    <Td textAlign='right'>
                      <Button size='xs' variant='ghost' colorScheme='red' onClick={() => removeDraftLine(index)}>
                        Xóa
                      </Button>
                    </Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
            <Flex mt='10px' justify='space-between' align='center'>
              <Text color={textColorSecondary}>
                Tổng Debit: <Text as='span' color={textColor} fontWeight='700'>{formatCurrency(draftTotals.totalDebit)}</Text>
                {" "}· Tổng Credit: <Text as='span' color={textColor} fontWeight='700'>{formatCurrency(draftTotals.totalCredit)}</Text>
              </Text>
              <Badge colorScheme={draftTotals.balanced ? "green" : "red"}>
                {draftTotals.balanced ? "Cân bằng" : "Lệch số"}
              </Badge>
            </Flex>
          </Box>
        </Card>

        <Card p='20px'>
          <Text color={textColor} fontWeight='700' mb='12px'>Kỳ kế toán & journal entries</Text>
          {(periods.items || []).length ? (
            <Box mb='16px'>
              <Table size='sm'>
                <Thead>
                  <Tr>
                    <Th>Period</Th>
                    <Th>Từ ngày</Th>
                    <Th>Đến ngày</Th>
                    <Th>Status</Th>
                  </Tr>
                </Thead>
                <Tbody>
                  {(periods.items || []).slice(0, 6).map((item, idx) => (
                    <Tr key={idx}>
                      <Td color={textColor} fontWeight='600'>
                        {fieldOf(item, "periodCode", "PeriodCode", "name", "Name") || `Period #${idx + 1}`}
                      </Td>
                      <Td color={textColorSecondary}>{formatDate(fieldOf(item, "startDate", "StartDate"))}</Td>
                      <Td color={textColorSecondary}>{formatDate(fieldOf(item, "endDate", "EndDate"))}</Td>
                      <Td>
                        <Badge colorScheme={String(fieldOf(item, "status", "Status") || "").toUpperCase().includes("OPEN") ? "green" : "gray"}>
                          {fieldOf(item, "status", "Status") || "UNKNOWN"}
                        </Badge>
                      </Td>
                    </Tr>
                  ))}
                </Tbody>
              </Table>
            </Box>
          ) : null}

          {(entries.items || []).length ? (
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Entry no</Th>
                  <Th>Date</Th>
                  <Th>Status</Th>
                  <Th textAlign='right'>Thao tác</Th>
                </Tr>
              </Thead>
              <Tbody>
                {(entries.items || []).slice(0, 8).map((entry, idx) => {
                  const id = fieldOf(entry, "id", "Id");
                  const status = String(fieldOf(entry, "status", "Status") || "");
                  return (
                    <Tr key={id || idx}>
                      <Td color={textColor} fontWeight='600'>
                        {fieldOf(entry, "entryNo", "EntryNo", "entry_no") || `JE-${id}`}
                      </Td>
                      <Td color={textColorSecondary}>
                        {formatDateTime(fieldOf(entry, "entryDate", "EntryDate", "createdAt", "CreatedAt"))}
                      </Td>
                      <Td>
                        <Badge colorScheme={status.toUpperCase() === "POSTED" ? "green" : "orange"}>
                          {status || "DRAFT"}
                        </Badge>
                      </Td>
                      <Td textAlign='right'>
                        <Button
                          size='xs'
                          colorScheme='blue'
                          isDisabled={status.toUpperCase() !== "DRAFT"}
                          isLoading={postingId === id}
                          onClick={() => handlePostJournal(id)}>
                          Post
                        </Button>
                      </Td>
                    </Tr>
                  );
                })}
              </Tbody>
            </Table>
          ) : (
            <Text color={textColorSecondary}>
              Chưa có dữ liệu journal để thao tác post/reverse.
            </Text>
          )}
        </Card>
      </SimpleGrid>
    </Box>
  );
}
