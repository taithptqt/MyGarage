import React from "react";
import {
  Box,
  Button,
  Divider,
  Flex,
  FormControl,
  FormLabel,
  Input,
  Modal,
  ModalBody,
  ModalCloseButton,
  ModalContent,
  ModalFooter,
  ModalHeader,
  ModalOverlay,
  SimpleGrid,
  Spinner,
  Text,
  Textarea,
} from "@chakra-ui/react";
import { formatCurrency, formatDate } from "../utils/accountingHelpers";

export function PayrollDetailModal({
  textColor,
  textColorSecondary,
  selectedPayrollItem,
  handleClosePayrollDetail,
  selectedPayrollBreakdown,
  loadingPayrollDetail,
  payrollDetailError,
}) {
  return (
    <Modal
      isOpen={Boolean(selectedPayrollItem)}
      onClose={handleClosePayrollDetail}
      size='3xl'
      isCentered
      scrollBehavior='inside'>
      <ModalOverlay />
      <ModalContent>
        <ModalHeader>
          Chi tiết lương: {selectedPayrollItem?.staffName || "--"}
        </ModalHeader>
        <ModalCloseButton />
        <ModalBody>
          <SimpleGrid columns={{ base: 1, md: 3 }} gap='10px' mb='12px'>
            <Box borderWidth='1px' borderRadius='10px' p='10px'>
              <Text color={textColorSecondary} fontSize='xs'>Kỳ lương</Text>
              <Text color={textColor} fontWeight='700'>
                {selectedPayrollItem?.periodCode || "--"}
              </Text>
            </Box>
            <Box borderWidth='1px' borderRadius='10px' p='10px'>
              <Text color={textColorSecondary} fontSize='xs'>Đợt lương</Text>
              <Text color={textColor} fontWeight='700'>
                {selectedPayrollItem?.runNo || "--"}
              </Text>
            </Box>
            <Box borderWidth='1px' borderRadius='10px' p='10px'>
              <Text color={textColorSecondary} fontSize='xs'>Mốc tính</Text>
              <Text color={textColor} fontWeight='700'>
                {formatDate(selectedPayrollBreakdown?.policyAsOfDate)}
              </Text>
            </Box>
          </SimpleGrid>

          {loadingPayrollDetail ? (
            <Flex align='center' gap='8px' mb='10px'>
              <Spinner size='sm' />
              <Text color={textColorSecondary} fontSize='sm'>Đang tải hệ số lương...</Text>
            </Flex>
          ) : null}

          {payrollDetailError ? (
            <Text color='orange.400' fontSize='sm' mb='10px'>
              {payrollDetailError}
            </Text>
          ) : null}

          {selectedPayrollBreakdown?.amountsSource === "INCOMPLETE" ? (
            <Text color='orange.400' fontSize='sm' mb='10px'>
              Backend chưa trả đủ breakdown canonical cho dòng lương này. Popup chỉ hiển thị các số server đã cung cấp.
            </Text>
          ) : null}

          {selectedPayrollBreakdown ? (
            <>
              <SimpleGrid columns={{ base: 1, md: 2 }} gap='10px' mb='12px'>
                <Box borderWidth='1px' borderRadius='10px' p='12px'>
                  <Text color={textColorSecondary} fontSize='xs'>Lương cơ bản (gốc)</Text>
                  <Text color={textColor} fontWeight='700'>
                    {selectedPayrollBreakdown.hasOriginalBaseSalary
                      ? formatCurrency(selectedPayrollBreakdown.originalBaseSalary)
                      : "Chưa thiết lập"}
                  </Text>
                </Box>
                <Box borderWidth='1px' borderRadius='10px' p='12px'>
                  <Text color={textColorSecondary} fontSize='xs'>Hệ số lương</Text>
                  <Text color={textColor} fontWeight='700'>
                    {Number(selectedPayrollBreakdown.coefficient || 1).toFixed(4)}
                  </Text>
                  <Text color={textColorSecondary} fontSize='xs'>
                    Thâm niên: {selectedPayrollBreakdown.yearsOfService} năm ({selectedPayrollBreakdown.tenureRange})
                  </Text>
                </Box>
                <Box borderWidth='1px' borderRadius='10px' p='12px'>
                  <Text color={textColorSecondary} fontSize='xs'>Lương áp dụng sau hệ số</Text>
                  <Text color={textColor} fontWeight='700'>
                    {formatCurrency(selectedPayrollItem.baseSalary)}
                  </Text>
                </Box>
                <Box borderWidth='1px' borderRadius='10px' p='12px'>
                  <Text color={textColorSecondary} fontSize='xs'>Phụ cấp</Text>
                  <Text color={textColor} fontWeight='700'>
                    {formatCurrency(selectedPayrollItem.allowanceAmount)}
                  </Text>
                </Box>
              </SimpleGrid>

              <SimpleGrid columns={{ base: 1, md: 2 }} gap='10px' mb='12px'>
                <Box borderWidth='1px' borderRadius='10px' p='12px'>
                  <Text color={textColorSecondary} fontSize='xs'>Tổng BH NLĐ</Text>
                  <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollBreakdown.employeeInsuranceTotal)}</Text>
                  <Text color={textColorSecondary} fontSize='xs' mt='6px'>
                    Trang này không còn tách BHXH/BHYT/BHTN ở frontend nếu backend chưa trả breakdown chi tiết.
                  </Text>
                </Box>

                <Box borderWidth='1px' borderRadius='10px' p='12px'>
                  <Text color={textColorSecondary} fontSize='xs'>Thuế TNCN (PIT)</Text>
                  <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollBreakdown.pitAmount)}</Text>
                  <Divider my='8px' />
                  <Text color={textColorSecondary} fontSize='xs'>Khấu trừ công thiếu</Text>
                  <Text color={textColor} fontWeight='700'>{formatCurrency(selectedPayrollItem.attendanceDeduction)}</Text>
                  <Text color={textColorSecondary} fontSize='xs' mt='6px'>Tạm ứng + Khoản vay + Khác</Text>
                  <Text color={textColor} fontWeight='700'>
                    {formatCurrency(
                      selectedPayrollItem.advanceDeduction +
                      selectedPayrollItem.loanDeduction +
                      selectedPayrollItem.otherDeduction +
                      selectedPayrollItem.penaltyAmount,
                    )}
                  </Text>
                </Box>
              </SimpleGrid>

              <Box borderWidth='1px' borderColor='green.300' bg='green.50' borderRadius='12px' p='12px'>
                <SimpleGrid columns={{ base: 1, md: 3 }} gap='10px'>
                  <Box>
                    <Text color='green.700' fontSize='xs'>Tổng thu nhập</Text>
                    <Text color='green.800' fontWeight='800' fontSize='lg'>
                      {selectedPayrollBreakdown.totalIncome == null ? "--" : formatCurrency(selectedPayrollBreakdown.totalIncome)}
                    </Text>
                  </Box>
                  <Box>
                    <Text color='green.700' fontSize='xs'>Tổng khấu trừ</Text>
                    <Text color='green.800' fontWeight='800' fontSize='lg'>
                      {selectedPayrollBreakdown.totalDeduction == null ? "--" : formatCurrency(selectedPayrollBreakdown.totalDeduction)}
                    </Text>
                  </Box>
                  <Box>
                    <Text color='green.700' fontSize='xs'>Tổng tiền nhận được</Text>
                    <Text color='green.800' fontWeight='800' fontSize='lg'>
                      {selectedPayrollBreakdown.netPay == null ? "--" : formatCurrency(selectedPayrollBreakdown.netPay)}
                    </Text>
                  </Box>
                </SimpleGrid>
              </Box>
            </>
          ) : (
            <Text color={textColorSecondary}>Chưa có dữ liệu chi tiết để hiển thị.</Text>
          )}
        </ModalBody>
        <ModalFooter>
          <Button onClick={handleClosePayrollDetail}>Đóng</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export function InitialSalaryRequestModal({
  initialSalaryTarget,
  handleCloseInitialSalaryRequest,
  salaryCheckAsOfDate,
  initialSalaryForm,
  setInitialSalaryForm,
  handleSubmitInitialSalaryRequest,
  sendingInitialSalaryRequest,
}) {
  return (
    <Modal isOpen={Boolean(initialSalaryTarget)} onClose={handleCloseInitialSalaryRequest} isCentered size='lg'>
      <ModalOverlay />
      <ModalContent>
        <ModalHeader>Gửi duyệt lương khởi tạo</ModalHeader>
        <ModalCloseButton />
        <ModalBody>
          <SimpleGrid columns={1} gap='10px'>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>
                Nhân sự
              </FormLabel>
              <Input value={initialSalaryTarget?.staffName || "--"} isReadOnly />
            </FormControl>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>
                Hiệu lực từ ngày
              </FormLabel>
              <Input type='date' value={salaryCheckAsOfDate} isReadOnly />
            </FormControl>
            <FormControl isRequired>
              <FormLabel fontSize='xs' mb='4px'>
                Lương cơ bản đề xuất
              </FormLabel>
              <Input
                type='number'
                min='0'
                step='1000'
                value={initialSalaryForm.baseSalary}
                onChange={(e) => setInitialSalaryForm((prev) => ({ ...prev, baseSalary: e.target.value }))}
                placeholder='VD: 12.000.000'
              />
            </FormControl>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>
                Lý do
              </FormLabel>
              <Input
                value={initialSalaryForm.reason}
                onChange={(e) => setInitialSalaryForm((prev) => ({ ...prev, reason: e.target.value }))}
                placeholder='Lý do đề xuất'
              />
            </FormControl>
            <FormControl>
              <FormLabel fontSize='xs' mb='4px'>
                Ghi chú
              </FormLabel>
              <Textarea
                value={initialSalaryForm.note}
                onChange={(e) => setInitialSalaryForm((prev) => ({ ...prev, note: e.target.value }))}
                placeholder='Thông tin bổ sung'
                rows={3}
              />
            </FormControl>
          </SimpleGrid>
        </ModalBody>
        <ModalFooter gap='8px'>
          <Button variant='ghost' onClick={handleCloseInitialSalaryRequest}>
            Hủy
          </Button>
          <Button
            colorScheme='orange'
            onClick={handleSubmitInitialSalaryRequest}
            isLoading={sendingInitialSalaryRequest}>
            Gửi duyệt
          </Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}
