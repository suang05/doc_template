import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'SMK Document Platform v2 — SAMMAKORN',
  description: 'Enterprise Document Generation Microservice & Management Console',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="th">
      <body>{children}</body>
    </html>
  );
}
