'use client';

import Link from 'next/link';

export default function NotFound() {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center bg-smk-canvas p-4 text-center">
      <h1 className="text-4xl font-bold text-smk-navy mb-2">404</h1>
      <p className="text-sm text-smk-muted mb-6">ไม่พบหน้าที่ต้องการ</p>
      <Link 
        href="/"
        className="bg-smk-navy hover:bg-smk-navy-dark text-white px-4 py-2 rounded-xl text-xs font-semibold transition shadow-smk"
      >
        กลับหน้าหลัก
      </Link>
    </div>
  );
}
