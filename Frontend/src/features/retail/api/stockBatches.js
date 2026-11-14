const BATCH_SIZE = 50;

export async function loadStockBatches(productIds, loadBatch) {
  const ids = [...new Set((Array.isArray(productIds) ? productIds : [])
    .map(Number).filter((id) => Number.isSafeInteger(id) && id > 0))];
  const rows = [];
  for (let offset = 0; offset < ids.length; offset += BATCH_SIZE) {
    const result = await loadBatch(ids.slice(offset, offset + BATCH_SIZE));
    if (!result.ok) return result;
    if (!Array.isArray(result.data)) return { ok: false, status: result.status, data: { message: "Invalid stock response." } };
    rows.push(...result.data);
  }
  return { ok: true, status: 200, data: rows };
}
