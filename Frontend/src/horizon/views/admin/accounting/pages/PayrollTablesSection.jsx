import React from "react";
import {
  Badge,
  Box,
  Button,
  Flex,
  Input,
  Select,
  SimpleGrid,
  Table,
  Tbody,
  Td,
  Text,
  Th,
  Thead,
  Tr,
} from "@chakra-ui/react";
import Card from "components/card/Card";
import TablePagination from "@shared/components/TablePagination";
import { formatCurrency, formatDate, formatDateTime } from "../utils/accountingHelpers";
import { statusColor, statusLabel } from "./payrollPageHelpers";

export function PayrollPeriodsAndRunsGrid({
  textColor,
  textColorSecondary,
  periodRows,
  runSummary,
  runRows,
  busyRunId,
  handleApprove,
  handleMarkPaid,
  handleCancelRun,
}) {
  return (
    <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
      <Card p='20px'>
        <Text color={textColor} fontWeight='700' mb='10px'>Danh sách kỳ lương</Text>
        {periodRows.length ? (
          <Box maxH='420px' overflowY='auto' overflowX='auto'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Mã kỳ</Th>
                  <Th>Từ ngày</Th>
                  <Th>Đến ngày</Th>
                  <Th>Ngày trả lương</Th>
                  <Th>Trạng thái</Th>
                </Tr>
              </Thead>
              <Tbody>
                {periodRows.map((period, idx) => {
                  const status = String(period.status || "").toUpperCase();
                  return (
                    <Tr key={period.id || idx}>
                      <Td color={textColor} fontWeight='600'>
                        {period.periodCode || `PERIOD-${idx + 1}`}
                      </Td>
                      <Td color={textColorSecondary}>{formatDate(period.startDate)}</Td>
                      <Td color={textColorSecondary}>{formatDate(period.endDate)}</Td>
                      <Td color={textColorSecondary}>{formatDate(period.payrollDate)}</Td>
                      <Td>
                        <Badge colorScheme={statusColor(status)}>{statusLabel(status)}</Badge>
                      </Td>
                    </Tr>
                  );
                })}
              </Tbody>
            </Table>
          </Box>
        ) : (
          <Text color={textColorSecondary}>Chưa có dữ liệu kỳ lương.</Text>
        )}
      </Card>

      <Card p='20px'>
        <Flex justify='space-between' align='center' mb='10px' wrap='wrap' gap='8px'>
          <Text color={textColor} fontWeight='700'>Danh sách kỳ lương</Text>
          <Flex gap='6px'>
            <Badge colorScheme='orange'>Nháp: {runSummary.draft}</Badge>
            <Badge colorScheme='blue'>Đã duyệt: {runSummary.approved}</Badge>
            <Badge colorScheme='green'>Đã chi: {runSummary.paid}</Badge>
          </Flex>
        </Flex>
        {runRows.length ? (
          <Box maxH='420px' overflowY='auto' overflowX='auto'>
            <Table size='sm'>
              <Thead>
                <Tr>
                  <Th>Mã kỳ</Th>
                  <Th>Chi nhánh</Th>
                  <Th>Ngày tạo</Th>
                  <Th>Status</Th>
                  <Th textAlign='right'>Thao tác</Th>
                </Tr>
              </Thead>
              <Tbody>
                {runRows.map((run) => (
                  <Tr key={run.id}>
                    <Td color={textColor} fontWeight='600'>{run.periodCode}</Td>
                    <Td color={textColorSecondary}>{run.branchName}</Td>
                    <Td color={textColorSecondary}>{formatDateTime(run.createdAt)}</Td>
                    <Td>
                      <Badge colorScheme={statusColor(run.status)}>
                        {statusLabel(run.status)}
                      </Badge>
                    </Td>
                    <Td textAlign='right'>
                      <Flex justify='flex-end' gap='8px'>
                        <Button
                          size='xs'
                          colorScheme='blue'
                          variant='outline'
                          isDisabled={run.status === "APPROVED" || run.status === "PAID"}
                          isLoading={busyRunId === run.id}
                          onClick={() => handleApprove(run.id)}>
                          Duyệt
                        </Button>
                        <Button
                          size='xs'
                          colorScheme='green'
                          variant='solid'
                          isDisabled={run.status !== "APPROVED"}
                          isLoading={busyRunId === run.id}
                          onClick={() => handleMarkPaid(run.id)}
                          _disabled={
                            run.status === "PAID"
                              ? {
                                opacity: 1,
                                bg: "green.400",
                                color: "white",
                                cursor: "not-allowed",
                                pointerEvents: "none",
                              }
                              : undefined
                          }>
                          {run.status === "PAID" ? "Đã chi" : "Xác nhận đã chi"}
                        </Button>
                        <Button
                          size='xs'
                          colorScheme='red'
                          variant='outline'
                          isDisabled={!(run.status === "DRAFT" || run.status === "CALCULATED")}
                          isLoading={busyRunId === run.id}
                          onClick={() => handleCancelRun(run.id)}>
                          Huỷ mã kỳ
                        </Button>
                      </Flex>
                    </Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
          </Box>
        ) : (
          <Text color={textColorSecondary}>Chưa có kỳ lương.</Text>
        )}
      </Card>
    </SimpleGrid>
  );
}

export function PayrollItemsCard({
  textColor,
  textColorSecondary,
  runFilter,
  setRunFilter,
  periodFilterOptions,
  search,
  setSearch,
  handleIssueAllPayslips,
  issuingAllPayslips,
  issuablePayslipCount,
  filteredItems,
  pagedItems,
  handleOpenPayrollDetail,
  busyItemId,
  handleIssuePayslip,
  itemPage,
  itemPageSize,
  setItemPage,
  setItemPageSize,
}) {
  return (
    <Card p='20px' mb='20px'>
      <Flex wrap='wrap' gap='10px' mb='10px'>
        <Select value={runFilter} onChange={(e) => setRunFilter(e.target.value)} maxW='220px'>
          <option value='ALL'>Tất cả kỳ lương</option>
          {periodFilterOptions.map((periodCode) => (
            <option value={periodCode} key={periodCode}>{periodCode}</option>
          ))}
        </Select>
        <Input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder='Tìm theo nhân sự / kỳ / trạng thái'
          maxW='320px'
        />
        <Button
          size='sm'
          colorScheme='purple'
          variant='outline'
          onClick={handleIssueAllPayslips}
          isLoading={issuingAllPayslips}
          isDisabled={issuingAllPayslips || issuablePayslipCount <= 0}>
          Phát hành toàn bộ phiếu lương ({issuablePayslipCount})
        </Button>
      </Flex>

      {filteredItems.length ? (
        <>
          <Table size='sm'>
          <Thead>
            <Tr>
              <Th>Nhân sự</Th>
              <Th>Mã kỳ</Th>
              <Th isNumeric>Tổng thu nhập*</Th>
              <Th isNumeric>Tổng khấu trừ*</Th>
              <Th isNumeric>Thực nhận*</Th>
              <Th>Status</Th>
              <Th>Phiếu lương</Th>
              <Th textAlign='right'>Thao tác</Th>
            </Tr>
          </Thead>
          <Tbody>
            {pagedItems.map((item) => (
              <Tr key={item.id}>
                <Td color={textColor} fontWeight='600'>
                  <Button
                    variant='link'
                    colorScheme='blue'
                    fontSize='sm'
                    fontWeight='700'
                    onClick={() => handleOpenPayrollDetail(item)}>
                    {item.staffName}
                  </Button>
                </Td>
                <Td color={textColorSecondary}>{item.periodCode || "--"}</Td>
                <Td color={textColor} isNumeric>{item.grossPay == null ? "--" : formatCurrency(item.grossPay)}</Td>
                <Td color={textColor} isNumeric>{item.totalDeduction == null ? "--" : formatCurrency(item.totalDeduction)}</Td>
                <Td color={textColor} isNumeric>{item.netPay == null ? "--" : formatCurrency(item.netPay)}</Td>
                <Td>
                  <Badge colorScheme={statusColor(item.status)}>
                    {statusLabel(item.status)}
                  </Badge>
                </Td>
                <Td>
                  <Badge colorScheme={item.hasPayslip ? "green" : "gray"}>
                    {item.hasPayslip ? "Đã phát hành" : "Chưa phát hành"}
                  </Badge>
                  <Text color={textColorSecondary} fontSize='xs'>
                    {item.amountsSource === "SERVER"
                      ? "Server"
                      : item.amountsSource === "DERIVED"
                      ? "Suy diễn"
                      : "Thiếu totals"}
                  </Text>
                  {!item.canIssuePayslip ? (
                    <Text color={textColorSecondary} fontSize='xs'>
                      Chờ duyệt đợt
                    </Text>
                  ) : null}
                </Td>
                <Td textAlign='right'>
                  <Button
                    size='xs'
                    colorScheme='purple'
                    variant='outline'
                    isDisabled={item.hasPayslip || !item.canIssuePayslip}
                    isLoading={busyItemId === item.id}
                    onClick={() => handleIssuePayslip(item.id)}>
                    Phát hành phiếu lương
                  </Button>
                </Td>
              </Tr>
            ))}
          </Tbody>
        </Table>
        <TablePagination
          totalItems={filteredItems.length}
          currentPage={itemPage}
          pageSize={itemPageSize}
          onPageChange={setItemPage}
          onPageSizeChange={(newSize) => {
            setItemPageSize(newSize);
            setItemPage(1);
          }}
          pageSizeOptions={[10, 20, 50]}
        />
      </>
    ) : (
        <Text color={textColorSecondary}>Chưa có dòng lương phù hợp bộ lọc.</Text>
      )}
      <Text color={textColorSecondary} fontSize='xs' mt='10px'>
        * Ưu tiên hiển thị totals backend. Nếu backend chưa trả explicit totals, frontend sẽ suy diễn từ các thành phần lương/khấu trừ.
      </Text>
    </Card>
  );
}
