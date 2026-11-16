import Link from 'next/link';
import { FileQuestion, Home } from 'lucide-react';
import { buttonVariants } from '@/components/ui/Button';
import { CardBlock } from '@/components/ui/CardBlock';

export default function NotFound() {
  return (
    <div className="min-h-screen bg-canvas flex items-center justify-center p-6">
      <CardBlock className="w-full max-w-md p-6 shadow-sm text-center">
        <div className="mx-auto w-11 h-11 rounded-sm border border-border bg-surfaceSubtle flex items-center justify-center text-primary">
          <FileQuestion className="w-5 h-5" aria-hidden="true" />
        </div>
        <p className="mt-3 text-[11px] font-mono font-semibold tracking-wider text-textMuted uppercase">
          404 • Page Not Found
        </p>
        <h1 className="mt-1 text-lg font-semibold text-textPrimary">
          ไม่พบหน้าที่คุณต้องการ
        </h1>
        <p className="mt-1.5 text-xs text-textSecondary leading-relaxed">
          ลิงก์นี้อาจถูกย้าย ลบออก หรือไม่ถูกต้องในระบบ SMK Doc Server
        </p>
        <div className="mt-5 flex justify-center">
          <Link href="/templates" className={buttonVariants({ variant: 'primary', size: 'md' })}>
            <Home className="w-3.5 h-3.5" aria-hidden="true" />
            <span>กลับไปหน้าแม่แบบเอกสาร</span>
          </Link>
        </div>
      </CardBlock>
    </div>
  );
}
