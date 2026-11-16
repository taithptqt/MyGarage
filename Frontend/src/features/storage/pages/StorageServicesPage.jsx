import ReferencePage from '@shared/brand/ReferencePage';
import pages from '@shared/brand/reference-pages.json';

const sourcePage = pages['/en/storage'].page;
const storagePage = {
  ...sourcePage,
  acf_page_header: {
    ...sourcePage.acf_page_header,
    groupHeader: {
      ...sourcePage.acf_page_header.groupHeader,
      buttons: [
        ...(sourcePage.acf_page_header.groupHeader.buttons || []),
        { button: { title: 'Khám phá garage 3D', url: '/storage/garage-3d' } },
      ],
    },
  },
};

export default function StorageServicesPage(){
  return <ReferencePage sourcePath="/en/storage" pageData={storagePage}/>;
}
