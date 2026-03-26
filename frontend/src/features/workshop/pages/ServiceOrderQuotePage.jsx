import React from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Badge, Box, Button, Heading, HStack, Spinner, Text, VStack } from "@chakra-ui/react";
import { useServiceOrderDetailData } from "../hooks/useServiceOrderDetailData";
import { formatOrderDateTime } from "../utils/serviceOrderDetailUtils";

export default function ServiceOrderQuotePage() {
  const navigate = useNavigate();
  const { orderId } = useParams();
  const { order, details, diagnoses, totalAmount, loading, error } = useServiceOrderDetailData(orderId);
  const latestDiagnosis = diagnoses[0] || null;

  return (
    <div className="workshop-table-card" style={{ padding: "24px" }}>
      <HStack justify="space-between" mb={5} flexWrap="wrap" gap={3}>
        <Box>
          <Heading size="md">Xem báo giá</Heading>
          <Text color="#707eae" mt={1}>
            Lệnh: <strong>{order?.code || `#${orderId}`}</strong>
          </Text>
        </Box>
        <Button colorScheme="blue" onClick={() => navigate("/workshop/reception")}>
          Quay lại reception
        </Button>
      </HStack>

      {loading ? (
        <Box textAlign="center" py={10}>
          <Spinner size="lg" />
        </Box>
      ) : error ? (
        <Text color="red.400">{error}</Text>
      ) : (
        <VStack align="stretch" spacing={4}>
          <HStack justify="space-between" p={4} bg="gray.50" borderRadius="md" border="1px solid #e9edf7" flexWrap="wrap">
            <Box>
              <Text fontSize="sm" color="#707eae">Khách hàng</Text>
              <Text fontWeight="700">{order?.customerName || "-"}</Text>
            </Box>
            <Box>
              <Text fontSize="sm" color="#707eae">Biển số/Thông tin xe</Text>
              <Text fontWeight="700">{order?.vehicleLicensePlate || "-"}</Text>
            </Box>
            <Box>
              <Text fontSize="sm" color="#707eae">Trạng thái</Text>
              <Badge colorScheme="blue">{order?.status || "-"}</Badge>
            </Box>
            <Box>
              <Text fontSize="sm" color="#707eae">Hẹn trả</Text>
              <Text fontWeight="700">{formatOrderDateTime(order?.estimatedHandover)}</Text>
            </Box>
          </HStack>

          <Box p={4} bg="white" borderRadius="md" border="1px solid #e9edf7">
            <Text fontWeight="700" mb={2}>Tóm tắt chẩn đoán</Text>
            <Text color="#2b3674" whiteSpace="pre-wrap">
              {latestDiagnosis?.diagnosisDetails || "Chưa có thông tin chẩn đoán chi tiết."}
            </Text>
          </Box>

          <Box p={4} bg="white" borderRadius="md" border="1px solid #e9edf7">
            <Text fontWeight="700" mb={3}>Chi tiết báo giá (view-only)</Text>
            {details.length === 0 ? (
              <Text color="#707eae">Chưa có hạng mục báo giá.</Text>
            ) : (
              <Box overflowX="auto">
                <table className="admin-table">
                  <thead>
                    <tr>
                      <th>Nội dung</th>
                      <th>SL</th>
                      <th>Đơn giá</th>
                      <th>Thành tiền</th>
                    </tr>
                  </thead>
                  <tbody>
                    {details.map((item, idx) => {
                      const quantity = Number(item.quantity || 1);
                      const unitPrice = Number(item.unitPrice || 0);
                      const amount = quantity * unitPrice;
                      return (
                        <tr key={item.id || idx}>
                          <td>{item.itemName || "Dịch vụ/Phụ tùng"}</td>
                          <td>{quantity}</td>
                          <td>{unitPrice.toLocaleString("vi-VN")}</td>
                          <td style={{ fontWeight: 700 }}>{amount.toLocaleString("vi-VN")}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </Box>
            )}
          </Box>

          <Box p={4} borderRadius="md" bg="#f4f7fe" border="1px dashed #a3aed0">
            <Text fontSize="sm" color="#707eae">Tổng giá trị ước tính</Text>
            <Text fontSize="2xl" fontWeight="800" color="#4318ff">
              {totalAmount.toLocaleString("vi-VN")}
            </Text>
          </Box>
        </VStack>
      )}
    </div>
  );
}
