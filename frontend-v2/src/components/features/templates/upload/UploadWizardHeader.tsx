import React from 'react';
import { ChevronLeft, UploadCloud, Settings2, CheckCircle2 } from 'lucide-react';

export const STEPS = [
  { label: 'อัปโหลด', icon: UploadCloud },
  { label: 'ตั้งค่า Mapping', icon: Settings2 },
  { label: 'ยืนยัน', icon: CheckCircle2 },
] as const;

export interface UploadWizardHeaderProps {
  step: 1 | 2 | 3;
  mappingCount: number;
  onStepBack?: () => void;
}

export const UploadWizardHeader: React.FC<UploadWizardHeaderProps> = ({
  step,
  mappingCount,
  onStepBack,
}) => {
  const StepIcon = STEPS[step - 1].icon;

  return (
    <div className="flex items-center justify-between pb-3 border-b border-border shrink-0">
      <div className="flex items-center gap-3">
        {step > 1 && onStepBack && (
          <button
            onClick={onStepBack}
            className="w-8 h-8 rounded-sm border border-border flex items-center justify-center hover:bg-surfaceSubtle transition-colors cursor-pointer text-textMuted shrink-0"
            title="ย้อนกลับขั้นก่อนหน้า"
          >
            <ChevronLeft className="w-4 h-4" />
          </button>
        )}
        <div
          className={`w-8 h-8 rounded-sm flex items-center justify-center border shrink-0 ${
            step === 1
              ? 'bg-indigo-50 text-indigo-600 border-indigo-200'
              : step === 2
              ? 'bg-amber-50 text-amber-600 border-amber-200'
              : 'bg-emerald-50 text-emerald-600 border-emerald-200'
          }`}
        >
          <StepIcon className="w-4 h-4" />
        </div>
        <div className="flex items-center flex-wrap gap-2">
          <span
            className={`text-xs font-medium px-2 py-0.5 border rounded-sm ${
              step === 1
                ? 'bg-indigo-50 text-indigo-700 border-indigo-200'
                : step === 2
                ? 'bg-amber-50 text-amber-700 border-amber-200'
                : 'bg-emerald-50 text-emerald-700 border-emerald-200'
            }`}
          >
            ขั้น {step} {STEPS[step - 1].label}
          </span>
          <span className="text-xs text-textMuted">
            {step === 1
              ? '.docx · .xlsx (Office)'
              : step === 2
              ? `พบ ${mappingCount} placeholder`
              : 'ชื่อ · Slug · หมวดหมู่'}
          </span>
        </div>
      </div>

      {/* Step dots */}
      <div className="flex items-center gap-1.5">
        {[1, 2, 3].map((s) => (
          <div
            key={s}
            className={`w-1.5 h-1.5 rounded-full transition-colors ${
              s === step ? 'bg-primary' : 'bg-border'
            }`}
          />
        ))}
      </div>
    </div>
  );
};

