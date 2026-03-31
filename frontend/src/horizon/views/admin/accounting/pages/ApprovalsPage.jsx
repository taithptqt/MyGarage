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
import {
  createFinanceApprovalRequest,
  decideFinanceApproval,
  getFinanceApprovalRequests,
  updateFinanceApprovalRequest
} from "../api/accountingApi";
import {
  formatDateTime,
  getCurrentUserId,
  getRiskColorScheme,
  normalizeText
} from "../utils/accountingHelpers";

function fieldOf(item, ...keys) {
  for (const key of keys) {
    if (item && item[key] !== undefined && item[key] !== null) return item[key];
  }
  return null;
}

function extractMetaFromReason(reasonText) {
  const text = String(reasonText || "");
  const riskMatch = text.match(/\[Risk:([A-Z]+)\]/i);
  const amountMatch = text.match(/\[Amount:([0-9.]+)\]/i);
  const slaMatch = text.match(/\[SLA:([^\]]+)\]/i);
  return {
    risk: riskMatch ? riskMatch[1].toUpperCase() : "MEDIUM",
    amount: amountMatch ? Number(amountMatch[1]) : 0,
    sla: slaMatch ? slaMatch[1] : "--"
  };
}

export default function ApprovalsPage() {
  const toast = useToast();
  const textColor = useColorModeValue("secondaryGray.900", "white");
  const textColorSecondary = useColorModeValue("secondaryGray.600", "secondaryGray.300");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [search, setSearch] = useState("");
  const [savingId, setSavingId] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [sourceLabel, setSourceLabel] = useState("n/a");

  const [rows, setRows] = useState([]);
  const [form, setForm] = useState({
    requestType: "REFUND",
    risk: "MEDIUM",
    amount: "",
    sla: "4h",
    reason: ""
  });

  async function loadData() {
    setLoading(true);
    setError("");
    try {
      const data = await getFinanceApprovalRequests();
      const normalized = (data?.items || []).map((item) => {
        const reason = fieldOf(item, "reason", "Reason");
        const meta = extractMetaFromReason(reason);
        return {
          raw: item,
          id: fieldOf(item, "id", "Id"),
          requestType: fieldOf(item, "requestType", "RequestType") || "UNKNOWN",
          status: fieldOf(item, "status", "Status") || "PENDING",
          reason,
          rejectReason: fieldOf(item, "rejectReason", "RejectReason"),
          createdAt: fieldOf(item, "createdAt", "CreatedAt"),
          requestedBy: fieldOf(item, "requestedBy", "RequestedBy"),
          approverId: fieldOf(item, "approverId", "ApproverId"),
          risk: meta.risk,
          amount: meta.amount,
          sla: meta.sla
        };
      });
      setRows(normalized);
      setSourceLabel(data?.source || "n/a");
    } catch (err) {
      setError(err?.message || "Không tải được dữ liệu approval tài chính.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  const filteredRows = useMemo(() => {
    return rows.filter((item) => {
      const statusPass = statusFilter === "ALL" || String(item.status).toUpperCase() === statusFilter;
      const searchTarget = normalizeText(
        [item.id, item.requestType, item.reason, item.status, item.risk, item.sla].join(" ")
      );
      const searchPass = !search || searchTarget.includes(normalizeText(search));
      return statusPass && searchPass;
    });
  }, [rows, statusFilter, search]);

  const summary = useMemo(() => {
    const pending = filteredRows.filter((item) => String(item.status).toUpperCase() === "PENDING").length;
    const approved = filteredRows.filter((item) => String(item.status).toUpperCase() === "APPROVED").length;
    const rejected = filteredRows.filter((item) => String(item.status).toUpperCase() === "REJECTED").length;
    return { pending, approved, rejected };
  }, [filteredRows]);

  async function handleCreate() {
    const userId = getCurrentUserId();
    if (!userId) {
      toast({
        status: "warning",
        title: "Không xác định được user hiện tại để tạo approval"
      });
      return;
    }
    if (!form.reason.trim()) {
      toast({
        status: "warning",
        title: "Vui lòng nhập lý do yêu cầu duyệt"
      });
      return;
    }

    setSubmitting(true);
    try {
      const reason = `[Risk:${form.risk}][Amount:${Number(form.amount || 0)}][SLA:${form.sla}] ${form.reason}`.trim();
      const result = await createFinanceApprovalRequest({
        requestType: form.requestType,
        requestedBy: userId,
        approverId: null,
        status: "PENDING",
        reason,
        rejectReason: null
      });
      if (!result.ok) {
        throw new Error(result?.data?.message || "Tạo approval thất bại.");
      }
      toast({
        status: "success",
        title: "Đã tạo yêu cầu duyệt tài chính"
      });
      setForm({
        requestType: "REFUND",
        risk: "MEDIUM",
        amount: "",
        sla: "4h",
        reason: ""
      });
      await loadData();
    } catch (err) {
      toast({
        status: "error",
        title: "Không tạo được yêu cầu",
        description: err?.message
      });
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDecision(row, decision) {
    setSavingId(row.id);
    try {
      const approverId = getCurrentUserId() || 0;
      if (approverId <= 0) {
        throw new Error("Không xác định được approverId từ phiên đăng nhập.");
      }
      try {
        await decideFinanceApproval(row.id, {
          decision,
          approverId,
          rejectReason: decision === "REJECTED" ? "Rejected by accountant approval flow." : null
        });
      } catch {
        const payload = {
          requestType: row.requestType,
          requestedBy: row.requestedBy || approverId,
          approverId,
          status: decision,
          reason: row.reason,
          rejectReason: decision === "REJECTED" ? "Rejected by accountant approval flow." : null,
          serviceOrderDiscountId: null,
          gatePassId: null,
          materialReturnId: null
        };
        const fallbackResult = await updateFinanceApprovalRequest(row.id, payload);
        if (!fallbackResult.ok) {
          throw new Error(fallbackResult?.data?.message || "Cập nhật approval thất bại.");
        }
      }

      toast({
        status: "success",
        title: `Đã ${decision === "APPROVED" ? "duyệt" : "từ chối"} yêu cầu #${row.id}`
      });
      await loadData();
    } catch (err) {
      toast({
        status: "error",
        title: "Không cập nhật được approval",
        description: err?.message
      });
    } finally {
      setSavingId(null);
    }
  }

  if (loading) {
    return (
      <Flex minH='260px' align='center' justify='center' direction='column' gap='10px'>
        <Spinner size='lg' color='brand.500' />
        <Text color={textColorSecondary}>Đang tải dữ liệu approval tài chính...</Text>
      </Flex>
    );
  }

  return (
    <Box>
      <Box mb='20px'>
        <Text color={textColor} fontSize='2xl' fontWeight='700'>Approval tài chính</Text>
        <Text color={textColorSecondary} fontSize='sm'>
          Quản lý yêu cầu duyệt refund/reversal/adjustment và xử lý SLA theo mức rủi ro.
        </Text>
      </Box>

      {error ? (
        <Card p='16px' mb='20px'>
          <Text color='red.400' fontWeight='600'>{error}</Text>
        </Card>
      ) : null}

      <SimpleGrid columns={{ base: 1, md: 2, lg: 4 }} gap='20px' mb='20px'>
        <MiniStatistics name='Pending' value={String(summary.pending)} />
        <MiniStatistics name='Approved' value={String(summary.approved)} />
        <MiniStatistics name='Rejected' value={String(summary.rejected)} />
        <MiniStatistics name='Nguồn dữ liệu' value={sourceLabel} />
      </SimpleGrid>

      <SimpleGrid columns={{ base: 1, xl: 3 }} gap='20px' mb='20px'>
        <Card p='20px' gridColumn={{ base: "span 1", xl: "span 1" }}>
          <Text color={textColor} fontWeight='700' mb='10px'>Tạo yêu cầu duyệt</Text>
          <SimpleGrid columns={1} gap='10px'>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Loại yêu cầu</FormLabel>
              <Select
                value={form.requestType}
                onChange={(e) => setForm((prev) => ({ ...prev, requestType: e.target.value }))}>
                <option value='REFUND'>REFUND</option>
                <option value='REVERSAL'>REVERSAL</option>
                <option value='INVOICE_ADJUSTMENT'>INVOICE_ADJUSTMENT</option>
                <option value='SUPPLIER_PAYMENT_VOID'>SUPPLIER_PAYMENT_VOID</option>
                <option value='MANUAL_JOURNAL'>MANUAL_JOURNAL</option>
                <option value='PAYROLL_ADJUSTMENT'>PAYROLL_ADJUSTMENT</option>
              </Select>
            </FormControl>
            <SimpleGrid columns={2} gap='10px'>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Mức rủi ro</FormLabel>
                <Select
                  value={form.risk}
                  onChange={(e) => setForm((prev) => ({ ...prev, risk: e.target.value }))}>
                  <option value='LOW'>LOW</option>
                  <option value='MEDIUM'>MEDIUM</option>
                  <option value='HIGH'>HIGH</option>
                </Select>
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>SLA</FormLabel>
                <Input
                  value={form.sla}
                  onChange={(e) => setForm((prev) => ({ ...prev, sla: e.target.value }))}
                  placeholder='VD: 4h'
                />
              </FormControl>
            </SimpleGrid>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Số tiền ảnh hưởng</FormLabel>
              <Input
                type='number'
                value={form.amount}
                onChange={(e) => setForm((prev) => ({ ...prev, amount: e.target.value }))}
              />
            </FormControl>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Lý do</FormLabel>
              <Input
                value={form.reason}
                onChange={(e) => setForm((prev) => ({ ...prev, reason: e.target.value }))}
                placeholder='Mô tả chi tiết lý do phê duyệt'
              />
            </FormControl>
            <Button colorScheme='blue' onClick={handleCreate} isLoading={submitting}>
              Tạo yêu cầu duyệt
            </Button>
          </SimpleGrid>
        </Card>

        <Card p='20px' gridColumn={{ base: "span 1", xl: "span 2" }}>
          <Flex wrap='wrap' gap='10px' mb='10px'>
            <Select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} maxW='180px'>
              <option value='ALL'>Tất cả status</option>
              <option value='PENDING'>PENDING</option>
              <option value='APPROVED'>APPROVED</option>
              <option value='REJECTED'>REJECTED</option>
            </Select>
            <Input
              placeholder='Tìm theo type/lý do/risk/SLA'
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              maxW='320px'
            />
          </Flex>
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>ID</Th>
                <Th>Type</Th>
                <Th>Risk/SLA</Th>
                <Th>Status</Th>
                <Th>Lý do</Th>
                <Th>Thời gian</Th>
                <Th textAlign='right'>Thao tác</Th>
              </Tr>
            </Thead>
            <Tbody>
              {filteredRows.map((row) => (
                <Tr key={row.id}>
                  <Td color={textColor} fontWeight='600'>#{row.id}</Td>
                  <Td color={textColorSecondary}>{row.requestType}</Td>
                  <Td>
                    <Flex direction='column' gap='4px'>
                      <Badge colorScheme={getRiskColorScheme(row.risk)}>{row.risk}</Badge>
                      <Text color={textColorSecondary} fontSize='xs'>{row.sla}</Text>
                    </Flex>
                  </Td>
                  <Td>
                    <Badge colorScheme={String(row.status).toUpperCase() === "APPROVED" ? "green" : String(row.status).toUpperCase() === "REJECTED" ? "red" : "orange"}>
                      {row.status}
                    </Badge>
                  </Td>
                  <Td color={textColorSecondary}>{row.reason || "--"}</Td>
                  <Td color={textColorSecondary}>{formatDateTime(row.createdAt)}</Td>
                  <Td textAlign='right'>
                    <Flex justify='flex-end' gap='8px'>
                      <Button
                        size='xs'
                        colorScheme='green'
                        variant='outline'
                        isDisabled={String(row.status).toUpperCase() !== "PENDING"}
                        isLoading={savingId === row.id}
                        onClick={() => handleDecision(row, "APPROVED")}>
                        Duyệt
                      </Button>
                      <Button
                        size='xs'
                        colorScheme='red'
                        variant='outline'
                        isDisabled={String(row.status).toUpperCase() !== "PENDING"}
                        isLoading={savingId === row.id}
                        onClick={() => handleDecision(row, "REJECTED")}>
                        Từ chối
                      </Button>
                    </Flex>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Card>
      </SimpleGrid>
    </Box>
  );
}
