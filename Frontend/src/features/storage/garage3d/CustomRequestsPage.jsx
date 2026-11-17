import { useEffect, useState } from 'react';
import { getCustomRequests } from './customRequestsApi';
import './CustomRequestsPage.css';

const copy = {
  title: 'Yêu cầu custom xe',
  intro: 'Danh sách lựa chọn khách hàng gửi từ không gian xe 3D.',
  loading: 'Đang tải yêu cầu…', empty: 'Chưa có yêu cầu custom xe.',
  error: 'Chưa tải được yêu cầu. Vui lòng thử lại.',
};

export default function CustomRequestsPage() {
  const [page, setPage] = useState(1), [version, setVersion] = useState(0);
  const [data, setData] = useState({ items: [], total: 0 }), [loading, setLoading] = useState(true), [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    setLoading(true); setError('');
    getCustomRequests(page).then(result => { if (active) setData(result); })
      .catch(() => { if (active) setError(copy.error); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, version]);
  return <section className="custom-requests-page">
    <header><div><h1>{copy.title}</h1><p>{copy.intro}</p></div><button disabled={loading} onClick={() => setVersion(value => value + 1)}>Tải lại</button></header>
    {loading ? <p role="status">{copy.loading}</p> : error ? <p role="alert">{error}</p> : <>
      {!data.items.length && <p>{copy.empty}</p>}
      <div className="custom-requests-list">{data.items.map(item => <article key={item.id}>
        <header><h2>#{item.id} · {item.customerName}</h2><time>{item.createdAt ? new Date(item.createdAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : '—'}</time></header>
        <p>{item.branchName}{item.customerPhone && <> · <a href={`tel:${item.customerPhone.replace(/[^+\d]/g, '')}`}>{item.customerPhone}</a></>}</p>
        <pre>{item.content.replace(/^\[CUSTOM XE 3D\]\r?\n/, '')}</pre>
      </article>)}</div>
      <nav aria-label="Phân trang yêu cầu custom"><button disabled={page === 1} onClick={() => setPage(value => value - 1)}>Trang trước</button><span>Trang {page} / {Math.max(1, Math.ceil(data.total / 20))}</span><button disabled={page * 20 >= data.total} onClick={() => setPage(value => value + 1)}>Trang sau</button></nav>
    </>}
  </section>;
}
