import ReferencePage from '@shared/brand/ReferencePage';
import originalHome from './original-home.json';
import './OriginalHome.css';
import FounderSection from './FounderSection';

export default function HomePage() {
  return <ReferencePage sourcePath="/en" pageData={originalHome} className="original-home" afterFirstBlock={<FounderSection />} />;
}
