'use client';

import React, { useState, useEffect } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/Button';

interface SessionWarningModalProps {
  onExtend: () => void;
  onLogout: () => void;
}

export function SessionWarningModal({ onExtend, onLogout }: SessionWarningModalProps) {
  const [seconds, setSeconds] = useState(60);

  useEffect(() => {
    if (seconds <= 0) { onLogout(); return; }
    const t = setTimeout(() => setSeconds((s) => s - 1), 1000);
    return () => clearTimeout(t);
  }, [seconds, onLogout]);

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-[100]">
      <div className="bg-surface border border-border rounded-md p-6 w-80 space-y-4 shadow-lg">
        <div className="flex items-start gap-3">
          <AlertTriangle className="w-5 h-5 text-amber-500 shrink-0 mt-0.5" />
          <div>
            <p className="text-sm font-semibold text-textPrimary">Session กำลังจะหมดอายุ</p>
            <p className="text-xs text-textMuted mt-1">
              ระบบจะออกจากระบบโดยอัตโนมัติใน{' '}
              <span className="font-semibold text-amber-600">{seconds}</span> วินาที
            </p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button onClick={onExtend} className="flex-1" size="sm">
            ต่ออายุ Session
          </Button>
          <Button variant="ghost" onClick={onLogout} className="flex-1" size="sm">
            ออกจากระบบ
          </Button>
        </div>
      </div>
    </div>
  );
}
