import { useCallback, useEffect, useState } from 'react';
import { Alert, AlertIcon, Box, Button, FormControl, FormLabel, Heading, Select, SimpleGrid, Text, Textarea } from '@chakra-ui/react';
import { getTechnicianLocationWorkspace, reportVehicleLocation } from '@features/storage/api/vehicleLocationApi';
import TechnicianRepairReport from './TechnicianRepairReport';

const copy = {
  title: 'Báo vị trí xe cho khách hàng',
  help: 'Ghi nhận nơi xe đang nằm thực tế. Chỉ chọn vị trí trong chi nhánh; khi đổi vị trí, chỗ cũ được giải phóng và lịch sử di chuyển được lưu.',
  unknown: 'Chưa xác nhận vị trí', loading: 'Đang tải xe được giao…', empty: 'Chưa có xe đang xử lý được giao cho bạn.',
  loadError: 'Chưa tải được danh sách xe và vị trí.', success: 'Đã cập nhật vị trí. Khách hàng sẽ thấy trong Garage của tôi.',
  saveError: 'Chưa cập nhật được vị trí. Vui lòng tải lại và thử lại.',
};

export default function TechnicianVehicleLocation() {
  const [data, setData] = useState({ orders: [], bays: [] }), [orderId, setOrderId] = useState(''), [bayId, setBayId] = useState(''), [note, setNote] = useState('');
  const [loading, setLoading] = useState(true), [saving, setSaving] = useState(false), [error, setError] = useState(''), [notice, setNotice] = useState('');
  const load = useCallback(async () => {
    setLoading(true); setError('');
    try { const result = await getTechnicianLocationWorkspace(); setData(result); }
    catch (e) { setError(e.message || copy.loadError); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  const order = data.orders.find(item => String(item.id) === orderId);
  const submit = async event => {
    event.preventDefault(); if (!order || !bayId || !note.trim() || saving) return;
    setSaving(true); setError(''); setNotice('');
    try {
      await reportVehicleLocation(order.id, { bayId: Number(bayId), expectedBayId: order.bayId ?? null, note: note.trim() });
      setNotice(copy.success); setNote(''); await load();
    } catch (e) { setError(e.message || copy.saveError); }
    finally { setSaving(false); }
  };
  return <Box bg="white" borderRadius="xl" border="1px solid" borderColor="gray.200" p={5}>
    <Heading size="md">{copy.title}</Heading><Text fontSize="sm" color="gray.600" mt={2}>{copy.help}</Text>
    {error && <Alert status="error" mt={3}><AlertIcon/>{error}</Alert>}{notice && <Alert status="success" mt={3}><AlertIcon/>{notice}</Alert>}
    {loading ? <Text mt={3} role="status">{copy.loading}</Text> : !data.orders.length ? <Text mt={3}>{copy.empty}</Text> : <form onSubmit={submit}>
      <SimpleGrid columns={{ base: 1, md: 2 }} gap={4} mt={4}>
        <FormControl isRequired><FormLabel>Xe / lệnh được giao</FormLabel><Select disabled={saving} value={orderId} onChange={event => { setOrderId(event.target.value); setBayId(''); setNote(''); setNotice(''); }}><option value="">Chọn xe cần báo vị trí</option>{data.orders.map(item => <option key={item.id} value={item.id}>{item.licensePlate} · {item.code}</option>)}</Select></FormControl>
        <FormControl isRequired><FormLabel>Kho / bãi / khoang thực tế</FormLabel><Select disabled={!order || saving} value={bayId} onChange={event => setBayId(event.target.value)}><option value="">Chọn vị trí hiện tại</option>{data.bays.filter(item => item.branchId === order?.branchId).map(item => <option key={item.id} value={item.id} disabled={item.occupiedVehicleIds.length >= item.capacity && !item.occupiedVehicleIds.includes(order?.vehicleId)}>{item.code} · {item.name} · {item.serviceArea || item.bayType} ({item.occupiedVehicleIds.length}/{item.capacity} xe)</option>)}</Select></FormControl>
      </SimpleGrid>
      {order && <Text fontSize="sm" color="gray.600" mt={3}>Vị trí đã ghi nhận: {order.bayCode || copy.unknown}</Text>}
      <FormControl isRequired mt={3}><FormLabel>Ghi chú bàn giao vị trí</FormLabel><Textarea disabled={saving} maxLength={500} value={note} onChange={event => setNote(event.target.value)} placeholder="Ví dụ: Đã chuyển xe sang bãi chờ, khoang A03"/></FormControl>
      <Button mt={3} type="submit" colorScheme="teal" isLoading={saving} isDisabled={!order || !bayId || !note.trim()}>Xác nhận vị trí thực tế</Button>
    </form>}
    {order && <TechnicianRepairReport key={order.id} order={order} onSaved={load}/>}
    <Button mt={3} size="sm" variant="ghost" onClick={load} isDisabled={saving || loading}>Tải lại vị trí</Button>
  </Box>;
}
