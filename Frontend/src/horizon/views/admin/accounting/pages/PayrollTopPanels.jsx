import React from "react";
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
} from "@chakra-ui/react";
import Card from "components/card/Card";
import MiniStatistics from "components/card/MiniStatistics";
import TablePagination from "@shared/components/TablePagination";
import { formatCurrency, formatDate, formatDateTime } from "../utils/accountingHelpers";
import { pick } from "./payrollPageHelpers";

export function PayrollSummaryAndPeriodControls({
  textColor,
  textColorSecondary,
  openPeriod,
  runRows,
  itemRows,
  officialPayrollTotal,
  missingPayrollFactsCount,
  periods,
  runs,
  items,
  payslips,
  staffs,
  periodForm,
  setPeriodForm,
  updatePeriodEndDate,
  handleCreatePeriod,
  savingPeriod,
  autoRunLabel,
  targetBranchId,
  selectedPeriodIdForBatch,
  selectedPeriodStatusForBatch,
  handleCalculateAllStaffPayroll,
  calculatingBatchPayroll,
}) {
  return (
    <>
      <SimpleGrid columns={{ base: 1, md: 2, xl: 5 }} gap='20px' mb='20px'>
        <MiniStatistics
          name='Kỳ đang mở'
          value={String(pick(openPeriod, "periodCode", "PeriodCode") || "Chua có")}
        />
        <MiniStatistics name='Kỳ tính lương' value={String(runRows.length)} />
        <MiniStatistics name='Dòng lương' value={String(itemRows.length)} />
        <MiniStatistics name='Tổng thực nhận (server)' value={formatCurrency(officialPayrollTotal)} />
        <MiniStatistics name='Dòng thiếu payroll facts' value={String(missingPayrollFactsCount)} />
      </SimpleGrid>

      <Card p='16px' mb='20px'>
        <Flex wrap='wrap' gap='10px' align='center'>
          <Badge colorScheme={periods.available ? "green" : "orange"}>API Kỳ lương: {periods.available ? "OK" : "Chưa có"}</Badge>
          <Badge colorScheme={runs.available ? "green" : "orange"}>API Đợt lương: {runs.available ? "OK" : "Chưa có"}</Badge>
          <Badge colorScheme={items.available ? "green" : "orange"}>API Dòng lương: {items.available ? "OK" : "Chưa có"}</Badge>
          <Badge colorScheme={payslips.available ? "green" : "orange"}>API Phiếu lương: {payslips.available ? "OK" : "Chưa có"}</Badge>
          <Badge colorScheme='blue'>Nhân sự: {staffs.length}</Badge>
        </Flex>
      </Card>

      <SimpleGrid columns={{ base: 1, xl: 2 }} gap='20px' mb='20px'>
        <Card p='16px'>
          <Text color={textColor} fontWeight='700' mb='10px'>Mở kỳ lương</Text>
          <SimpleGrid columns={1} gap='10px'>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Mã kỳ lương</FormLabel>
              <Input
                value={periodForm.periodCode}
                onChange={(e) => setPeriodForm((prev) => ({ ...prev, periodCode: e.target.value }))}
                placeholder='VD: 2026-03'
              />
            </FormControl>
            <SimpleGrid columns={2} gap='10px'>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Từ ngày</FormLabel>
                <Input
                  type='date'
                  value={periodForm.startDate}
                  onChange={(e) => setPeriodForm((prev) => ({ ...prev, startDate: e.target.value }))}
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Đến ngày</FormLabel>
                <Input
                  type='date'
                  value={periodForm.endDate}
                  onChange={(e) => updatePeriodEndDate(e.target.value)}
                />
              </FormControl>
            </SimpleGrid>
            <SimpleGrid columns={2} gap='10px'>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Ngày trả lương</FormLabel>
                <Input
                  type='date'
                  value={periodForm.payrollDate}
                  onChange={(e) => setPeriodForm((prev) => ({ ...prev, payrollDate: e.target.value }))}
                />
              </FormControl>
              <FormControl>
                <FormLabel fontSize='xs' mb='4px'>Trạng thái</FormLabel>
                <Select
                  value={periodForm.status}
                  onChange={(e) => setPeriodForm((prev) => ({ ...prev, status: e.target.value }))}>
                  <option value='OPEN'>Đang mở</option>
                </Select>
              </FormControl>
            </SimpleGrid>
            <Text color={textColorSecondary} fontSize='xs'>
              Luu ý: Ngày trả lương phải bằng hoặc sau ngày kết thúc kỳ.
            </Text>
            <Button size='sm' colorScheme='blue' onClick={handleCreatePeriod} isLoading={savingPeriod}>
              Tạo kỳ lương
            </Button>
          </SimpleGrid>
        </Card>

        <Card p='16px'>
          <Text color={textColor} fontWeight='700' mb='10px'>Tính lương toàn bộ nhân sự</Text>
          <SimpleGrid columns={1} gap='10px'>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>Kỳ lương (tự động)</FormLabel>
              <Input value={autoRunLabel} isReadOnly />
            </FormControl>
            <Text color={textColorSecondary} fontSize='sm'>
              Hệ thống sẽ tự lấy dữ liệu lương và chỉ tính cho nhân sự thuộc chi nhánh đang chọn.
            </Text>
            {!targetBranchId ? (
              <Text color='orange.400' fontSize='xs'>
                Cần chọn chi nhánh trước khi tính lương.
              </Text>
            ) : null}
            {!selectedPeriodIdForBatch ? (
              <Text color='orange.400' fontSize='xs'>
                Cần có kỳ lương đang mở trước khi tính lương hàng loạt.
              </Text>
            ) : null}
            {selectedPeriodIdForBatch && selectedPeriodStatusForBatch !== "OPEN" ? (
              <Text color='orange.400' fontSize='xs'>
                Kỳ đã chọn chưa ở trạng thái ĐANG MỞ.
              </Text>
            ) : null}
            <Button
              size='sm'
              colorScheme='teal'
              variant='outline'
              onClick={handleCalculateAllStaffPayroll}
              isLoading={calculatingBatchPayroll}
              isDisabled={!targetBranchId || !selectedPeriodIdForBatch || selectedPeriodStatusForBatch !== "OPEN"}>
              Tính lương toàn bộ nhân sự
            </Button>
          </SimpleGrid>
        </Card>
      </SimpleGrid>
    </>
  );
}

export function MissingBaseSalaryCard({
  textColor,
  textColorSecondary,
  missingBaseSalaryRows,
  loading,
  loadingMissingBaseSalary,
  handleReloadMissingBaseSalary,
  handleOpenInitialSalaryRequest,
}) {
  return (
    <Card p='20px' mb='20px'>
      <Flex justify='space-between' align='center' mb='10px' wrap='wrap' gap='8px'>
        <Text color={textColor} fontWeight='700'>
          Nhân sự chưa có lương cơ bản
        </Text>
        <Flex align='center' gap='8px'>
          <Badge colorScheme={missingBaseSalaryRows.length ? "orange" : "green"}>
            Cần duyệt: {missingBaseSalaryRows.length}
          </Badge>
          <Button
            size='xs'
            variant='outline'
            onClick={handleReloadMissingBaseSalary}
            isLoading={loading || loadingMissingBaseSalary}>
            Tải lại
          </Button>
        </Flex>
      </Flex>

      {loadingMissingBaseSalary ? (
        <Flex align='center' gap='8px'>
          <Spinner size='sm' />
          <Text color={textColorSecondary} fontSize='sm'>
            Đang quét lương cơ bản...
          </Text>
        </Flex>
      ) : missingBaseSalaryRows.length ? (
        <Box maxH='320px' overflowY='auto' overflowX='auto'>
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Nhân sự</Th>
                <Th>Trạng thái</Th>
                <Th>Ngày vào</Th>
                <Th isNumeric>Lương cơ bản</Th>
                <Th textAlign='right'>Xử lý</Th>
              </Tr>
            </Thead>
            <Tbody>
              {missingBaseSalaryRows.map((row) => (
                <Tr key={row.staffId}>
                  <Td color={textColor} fontWeight='600'>
                    {row.staffName}
                    {row.checkError ? (
                      <Text color='orange.400' fontSize='xs'>
                        {row.checkError}
                      </Text>
                    ) : null}
                  </Td>
                  <Td>
                    <Badge colorScheme='gray'>{row.status || "--"}</Badge>
                  </Td>
                  <Td color={textColorSecondary}>{formatDate(row.hireDate)}</Td>
                  <Td isNumeric color={textColor}>
                    {row.baseSalary > 0 ? formatCurrency(row.baseSalary) : "--"}
                  </Td>
                  <Td textAlign='right'>
                    <Button
                      size='xs'
                      colorScheme='orange'
                      variant='outline'
                      onClick={() => handleOpenInitialSalaryRequest(row)}>
                      Gửi duyệt lương
                    </Button>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </Box>
      ) : (
        <Text color={textColorSecondary}>Không có nhân sự nào thiếu lương cơ bản.</Text>
      )}
    </Card>
  );
}

export function InitialSalaryDecisionsCard({
  title = "Kết quả duyệt lương khởi tạo",
  textColor,
  textColorSecondary,
  initialSalaryDecisionSummary,
  pollInitialSalaryRequestUpdates,
  initialSalaryDecisionRows,
  pagedDecisionRows,
  decisionPage,
  decisionPageSize,
  setDecisionPage,
  setDecisionPageSize,
}) {
  return (
    <Card p='20px' mb='20px'>
      <Flex justify='space-between' align='center' mb='10px' wrap='wrap' gap='8px'>
        <Text color={textColor} fontWeight='700'>
          {title}
        </Text>
        <Flex align='center' gap='8px'>
          <Badge colorScheme='green'>Được duyệt: {initialSalaryDecisionSummary.approved}</Badge>
          <Badge colorScheme='red'>Bị từ chối: {initialSalaryDecisionSummary.rejected}</Badge>
          <Button size='xs' variant='outline' onClick={pollInitialSalaryRequestUpdates}>
            Tải lại
          </Button>
        </Flex>
      </Flex>

      {initialSalaryDecisionRows.length ? (
        <Box overflowX='auto'>
          <Table size='sm'>
            <Thead>
              <Tr>
                <Th>Nhân sự</Th>
                <Th>Kết quả</Th>
                <Th>Admin xử lý</Th>
                <Th>Thời gian</Th>
                <Th>Ghi chú</Th>
              </Tr>
            </Thead>
            <Tbody>
              {pagedDecisionRows.map((row) => (
                <Tr key={row.requestId}>
                  <Td color={textColor} fontWeight='600'>
                    {row.staffName}
                  </Td>
                  <Td>
                    <Badge colorScheme={row.status === "APPROVED" ? "green" : "red"}>
                      {row.status === "APPROVED" ? "Đã duyệt" : "Từ chối"}
                    </Badge>
                  </Td>
                  <Td color={textColorSecondary}>{row.approverName || "--"}</Td>
                  <Td color={textColorSecondary}>{formatDateTime(row.updatedAt)}</Td>
                  <Td color={textColorSecondary}>
                    {row.status === "REJECTED" ? row.rejectReason || "--" : "--"}
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
          <TablePagination
            totalItems={initialSalaryDecisionRows.length}
            currentPage={decisionPage}
            pageSize={decisionPageSize}
            onPageChange={setDecisionPage}
            onPageSizeChange={(newSize) => {
              setDecisionPageSize(newSize);
              setDecisionPage(1);
            }}
            pageSizeOptions={[10, 20, 50, 100]}
          />
        </Box>
      ) : (
        <Text color={textColorSecondary}>Chưa có kết quả duyệt/từ chối.</Text>
      )}
    </Card>
  );
}
