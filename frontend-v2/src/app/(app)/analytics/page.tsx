import React from 'react';
import { navLabels } from '@/components/layout/Sidebar';

export default function AnalyticsPage() {
  return (
    <div className="flex h-full items-center justify-center pt-20">
      <div className="text-center space-y-2">
        <h3 className="text-sm font-bold text-textPrimary">
          {navLabels['analytics']} (Coming Soon)
        </h3>
        <p className="text-xs text-textMuted">ฟีเจอร์นี้อยู่ในระหว่างการพัฒนา</p>
      </div>
    </div>
  );
}
