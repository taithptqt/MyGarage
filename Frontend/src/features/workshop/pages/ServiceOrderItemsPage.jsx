import React from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Box, Button, Heading, HStack, Spinner, Text } from "@chakra-ui/react";
import { useServiceOrderDetailData } from "@features/workshop/hooks/useServiceOrderDetailData";

export default function ServiceOrderItemsPage() {
  const navigate = useNavigate();
  const { orderId } = useParams();
  const { order, details, totalAmount, loading, error } = useServiceOrderDetailData(orderId);

  return (
    <div className="workshop-table-card" style={{ padding: "24px" }}>
      <HStack justify="space-between" mb={5} flexWrap="wrap" gap={3}>
        <Box>
          <Heading size="md">Hạng mục công việc và phụ tùng</Heading>
          <Text color="#707eae" mt={1}>
            Lệnh: <strong>{order?.code || `#${orderId}`}</strong>
          </Text>
        </Box>
        <Button colorScheme="blue" onClick={() => navigate("/workshop/reception")}>
        Quay lại
        </Button>
      </HStack>

      {loading ? (
        <Box textAlign="center" py={10}>
          <Spinner size="lg" />
        </Box>
      ) : error ? (
        <Text color="red.400">{error}</Text>
      ) : details.length === 0 ? (
        <Text color="#707eae">Chưa có danh sách hạng mục công việc.</Text>
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
                    <td>
                      <div style={{ fontWeight: 700 }}>{item.itemName || "Dịch vụ/Phụ tùng"}</div>
                      {item.note ? <div style={{ color: "#707eae", fontSize: 13 }}>{item.note}</div> : null}
                    </td>
                    <td>{quantity}</td>
                    <td>{unitPrice.toLocaleString("vi-VN")}</td>
                    <td style={{ fontWeight: 700 }}>{amount.toLocaleString("vi-VN")}</td>
                  </tr>
                );
              })}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={3} style={{ textAlign: "right", fontWeight: 700 }}>
                  Tổng cộng:
                </td>
                <td style={{ fontWeight: 800, color: "#4318ff" }}>
                  {totalAmount.toLocaleString("vi-VN")}
                </td>
              </tr>
            </tfoot>
          </table>
        </Box>
      )}
    </div>
  );
}





