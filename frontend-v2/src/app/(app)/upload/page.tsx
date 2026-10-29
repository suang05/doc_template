import type { Metadata } from 'next';
import { UploadPageClient } from './_components/UploadPageClient';

export const metadata: Metadata = {
  title: 'Upload Template — SMK Document Platform',
  description: 'Upload ZIP, HTML, or Word template packages to the gateway',
};

export default function UploadPage() {
  return <UploadPageClient />;
}
