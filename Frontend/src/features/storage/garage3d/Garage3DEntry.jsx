import { useSearchParams } from 'react-router-dom';
import { useAuth } from '@shared/core/context/AuthContext';
import Garage3DPage from './Garage3DPage';
import CustomerGarage3DPage from './CustomerGarage3DPage';

export default function Garage3DEntry() {
  const { user, loading } = useAuth(); const [params] = useSearchParams();
  if (loading) return <p role="status">Đang tải garage…</p>;
  return String(user?.roleCode || user?.RoleCode).toUpperCase() === 'CUSTOMER' && params.get('mode') !== 'custom' ? <CustomerGarage3DPage/> : <Garage3DPage/>;
}
