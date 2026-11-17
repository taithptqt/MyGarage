import { useCallback, useEffect, useState } from "react";
import { Alert, AlertIcon, Box, Button, FormControl, FormLabel, Heading, Select, Text } from "@chakra-ui/react";
import { getTechnicianLocationWorkspace } from "@features/storage/api/vehicleLocationApi";
import TechnicianRepairReport from "./TechnicianRepairReport";

const LABELS = {
  title: "Cập nhật bộ phận sửa chữa",
  select: "Chọn xe và lệnh được giao",
  loading: "Đang tải xe được giao…",
  empty: "Chưa có xe đang xử lý được giao cho bạn.",
  error: "Chưa tải được danh sách xe được giao.",
  refresh: "Tải lại",
};

export default function TechnicianRepairWorkspace() {
  const [orders, setOrders] = useState([]);
  const [orderId, setOrderId] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const result = await getTechnicianLocationWorkspace();
      setOrders(Array.isArray(result?.orders) ? result.orders : []);
    } catch (err) {
      setError(err.message || LABELS.error);
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => { void load(); }, [load]);
  const order = orders.find(item => String(item.id) === orderId);

  return <Box bg="white" borderRadius="xl" border="1px solid" borderColor="gray.200" p={5}>
    <Heading size="md">{LABELS.title}</Heading>
    {error && <Alert status="error" mt={3}><AlertIcon />{error}</Alert>}
    {loading ? <Text mt={3} role="status">{LABELS.loading}</Text> : !orders.length ? <Text mt={3}>{LABELS.empty}</Text> : <FormControl mt={3}>
      <FormLabel>{LABELS.select}</FormLabel>
      <Select value={orderId} onChange={event => setOrderId(event.target.value)}>
        <option value="">{LABELS.select}</option>
        {orders.map(item => <option key={item.id} value={item.id}>{item.licensePlate} · {item.code}</option>)}
      </Select>
    </FormControl>}
    {order && <TechnicianRepairReport key={order.id} order={order} onSaved={load} />}
    <Button mt={3} size="sm" variant="ghost" onClick={load} isDisabled={loading}>{LABELS.refresh}</Button>
  </Box>;
}
