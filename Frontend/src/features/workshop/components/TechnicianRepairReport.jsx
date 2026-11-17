import { useState } from 'react';
import { Alert, AlertIcon, Box, Button, FormControl, FormLabel, Heading, Select, Text, Textarea } from '@chakra-ui/react';
import { reportVehicleRepair } from '@features/storage/api/vehicleLocationApi';
import { REPAIR_COPY as copy, REPAIR_PARTS } from '@features/storage/garage3d/repairConfig';

export default function TechnicianRepairReport({ order, onSaved }) {
  const [partId, setPartId] = useState(''), [note, setNote] = useState('');
  const [saving, setSaving] = useState(false), [error, setError] = useState(''), [notice, setNotice] = useState('');
  const save = async (part, content, completed = false, expectedReportId = null) => {
    if (!order || saving || !part || !content.trim()) return;
    setSaving(true); setError(''); setNotice('');
    try {
      await reportVehicleRepair(order.id, { partId: part, note: content.trim(), completed, expectedReportId });
      setNotice(copy.success); if (!completed) { setPartId(''); setNote(''); }
      await onSaved();
    } catch (e) { setError(e.message || copy.error); }
    finally { setSaving(false); }
  };
  return <Box mt={6} pt={5} borderTop="1px solid" borderColor="gray.200">
    <Heading size="sm">{copy.title}</Heading><Text fontSize="sm" color="gray.600" mt={2}>{copy.help}</Text>
    {error && <Alert status="error" mt={3}><AlertIcon/>{error}</Alert>}
    {notice && <Alert status="success" mt={3}><AlertIcon/>{notice}</Alert>}
    <FormControl mt={3}><FormLabel>{copy.select}</FormLabel><Select value={partId} disabled={saving} onChange={event => setPartId(event.target.value)}><option value="">{copy.select}</option>{REPAIR_PARTS.map(part => <option key={part.id} value={part.id}>{part.label}</option>)}</Select></FormControl>
    <FormControl mt={3}><FormLabel>{copy.note}</FormLabel><Textarea value={note} maxLength={500} disabled={saving} onChange={event => setNote(event.target.value)} placeholder={copy.placeholder}/></FormControl>
    <Button mt={3} colorScheme="teal" isLoading={saving} isDisabled={!partId || !note.trim()} onClick={() => save(partId, note)}>{copy.save}</Button>
    {!!order?.repairs?.length && <Box mt={4}><Text fontWeight="bold">{copy.active}</Text>{order.repairs.map(repair => <Box key={repair.partId} mt={3} p={3} bg="gray.50" borderRadius="md"><Text fontWeight="semibold">{REPAIR_PARTS.find(part => part.id === repair.partId)?.label || repair.partId}</Text><Text fontSize="sm" whiteSpace="pre-wrap">{repair.note}</Text><Button mt={2} size="sm" isDisabled={saving} onClick={() => save(repair.partId, repair.note, true, repair.reportId)}>{copy.complete}</Button></Box>)}</Box>}
  </Box>;
}
