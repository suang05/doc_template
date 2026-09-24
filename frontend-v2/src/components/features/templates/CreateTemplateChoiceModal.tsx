'use client';

import React, { useState } from 'react';
import {
  FileCode,
  FileSpreadsheet,
  ArrowRight,
  ArrowLeft,
  Check,
  Sparkles,
  Layers,
  Upload,
} from 'lucide-react';
import { Modal, Button, Input, Select, Badge } from '@/components/ui';
import { templatesApi } from '@/lib/api/templates.api';

export interface CreateTemplateChoiceModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSelectOffice: () => void;
  onOpenStudio: (templateId: string) => void;
  onCreated?: () => void;
}

export const CreateTemplateChoiceModal: React.FC<CreateTemplateChoiceModalProps> = ({
  isOpen,
  onClose,
  onSelectOffice,
  onOpenStudio,
  onCreated,
}) => {
  const [step, setStep] = useState<'choice' | 'html_form'>('choice');
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [category, setCategory] = useState('contract');
  const [starterTemplate, setStarterTemplate] = useState<'blank' | 'standard'>('standard');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const resetState = () => {
    setStep('choice');
    setName('');
    setSlug('');
    setCategory('contract');
    setStarterTemplate('standard');
    setError(null);
    setLoading(false);
  };

  const handleClose = () => {
    resetState();
    onClose();
  };

  const handleChooseOffice = () => {
    handleClose();
    onSelectOffice();
  };

  const handleCreateHtmlTemplate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !slug.trim()) {
      setError('กรุณาระบุชื่อแม่แบบและ Slug ภาษาอังกฤษ');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const defaultHtml = starterTemplate === 'standard'
        ? `<!DOCTYPE html>
<html lang="th">
<head>
  <meta charset="UTF-8">
  <title>${name.trim()}</title>
  <style>
    @page { size: A4; margin: 20mm; }
    body { font-family: 'Sarabun', sans-serif; font-size: 14pt; line-height: 1.6; color: #1e293b; }
    .header { text-align: center; border-bottom: 2px solid #0284c7; padding-bottom: 12px; margin-bottom: 24px; }
    .title { font-size: 20pt; font-weight: bold; color: #0284c7; margin: 0; }
    .meta { font-size: 10pt; color: #64748b; margin-top: 4px; }
    .content { margin-top: 16px; }
    .footer { margin-top: 40px; display: flex; justify-content: space-between; font-size: 11pt; }
  </style>
</head>
<body>
  <div class="header">
    <h1 class="title">${name.trim()}</h1>
    <div class="meta">เอกสารเลขที่: {{documentNumber}} | วันที่: {{formatThaiDate date 'long'}}</div>
  </div>
  <div class="content">
    <p>เรียน {{customer.name}},</p>
    <p>ข้อความเอกสารตัวอย่าง...</p>
  </div>
  <div class="footer">
    <div>ลงชื่อ ..........................................<br>(ผู้มีอำนาจลงนาม)</div>
    <div>ลงชื่อ ..........................................<br>(ผู้รับเอกสาร)</div>
  </div>
</body>
</html>`
        : `<!DOCTYPE html>
<html lang="th">
<head>
  <meta charset="UTF-8">
  <style>
    @page { size: A4; margin: 20mm; }
    body { font-family: 'Sarabun', sans-serif; font-size: 14pt; }
  </style>
</head>
<body>
  <h1>${name.trim()}</h1>
  <p>{{message}}</p>
</body>
</html>`;

      const formData = new FormData();
      formData.append('name', name.trim());
      formData.append('slug', slug.trim().toLowerCase());
      formData.append('category', category);

      // Create a virtual file for the starter HTML
      const blob = new Blob([defaultHtml], { type: 'text/html;charset=utf-8' });
      formData.append('file', blob, `${slug.trim().toLowerCase()}.html`);

      const res = await templatesApi.createTemplate(formData);

      handleClose();
      if (onCreated) onCreated();
      onOpenStudio(res.id);
    } catch (err: any) {
      setError(err.message || 'สร้างแม่แบบล้มเหลว');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
      title={step === 'choice' ? 'เลือกรูปแบบการสร้างแม่แบบ' : 'สร้างแม่แบบ HTML ใหม่'}
      maxWidth={step === 'choice' ? 'lg' : 'md'}
    >
      {step === 'choice' ? (
        <div className="space-y-4 py-2">
          <p className="text-xs text-textSecondary text-center">
            เลือกรูปแบบการทำงานที่ตรงกับความต้องการของคุณเพื่อประสบการณ์การออกแบบเอกสารที่เหมาะสมที่สุด
          </p>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2">
            {/* Option 1: HTML Studio */}
            <div
              onClick={() => setStep('html_form')}
              className="group relative flex flex-col justify-between p-5 rounded-[4px] border border-border bg-white hover:border-sky-500 hover:shadow-md cursor-pointer transition-all duration-150"
            >
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <div className="w-10 h-10 rounded-[4px] bg-sky-50 text-sky-600 flex items-center justify-center group-hover:bg-sky-500 group-hover:text-white transition-colors">
                    <FileCode size={22} />
                  </div>
                  <Badge category="official">
                    Code-First
                  </Badge>
                </div>

                <div>
                  <h3 className="text-sm font-bold text-textPrimary group-hover:text-sky-600 transition-colors">
                    สร้างด้วยโค้ด (HTML / Handlebars)
                  </h3>
                  <p className="text-[11px] text-textMuted mt-1 leading-relaxed">
                    ออกแบบด้วย Monaco Editor เขียน HTML/CSS พร้อม Handlebars Template และนำเข้าไฟล์ .html แก้ต่อได้ทันที
                  </p>
                </div>

                <ul className="space-y-1.5 text-[11px] text-textSecondary pt-2 border-t border-border/60">
                  <li className="flex items-center gap-1.5">
                    <Check size={13} className="text-emerald-500 shrink-0" />
                    <span>Instant Live Preview แสดงผลสดทันทีขณะพิมพ์</span>
                  </li>
                  <li className="flex items-center gap-1.5">
                    <Check size={13} className="text-emerald-500 shrink-0" />
                    <span>รองรับฟอนต์ Sarabun และ Helper ภาษาไทยเต็มรูปแบบ</span>
                  </li>
                  <li className="flex items-center gap-1.5">
                    <Check size={13} className="text-emerald-500 shrink-0" />
                    <span>เหมาะสำหรับ Developer หรือต้องการจัด Layout แบบละเอียด</span>
                  </li>
                </ul>
              </div>

              <div className="pt-4 mt-3">
                <Button
                  variant="primary"
                  className="w-full justify-center"
                  icon={ArrowRight}
                  onClick={() => setStep('html_form')}
                >
                  เปิด Monaco Studio
                </Button>
              </div>
            </div>

            {/* Option 2: Office Files */}
            <div
              onClick={handleChooseOffice}
              className="group relative flex flex-col justify-between p-5 rounded-[4px] border border-border bg-white hover:border-emerald-500 hover:shadow-md cursor-pointer transition-all duration-150"
            >
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <div className="w-10 h-10 rounded-[4px] bg-emerald-50 text-emerald-600 flex items-center justify-center group-hover:bg-emerald-500 group-hover:text-white transition-colors">
                    <FileSpreadsheet size={22} />
                  </div>
                  <Badge category="financial">
                    File-First
                  </Badge>
                </div>

                <div>
                  <h3 className="text-sm font-bold text-textPrimary group-hover:text-emerald-600 transition-colors">
                    สร้างด้วยไฟล์ Office (Word / Excel)
                  </h3>
                  <p className="text-[11px] text-textMuted mt-1 leading-relaxed">
                    นำเข้าไฟล์ .docx หรือ .xlsx ที่จัดหน้าตาไว้เรียบร้อยแล้วจาก Microsoft Office พร้อมระบบคลี่ XML ดึงแท็กอัตโนมัติ
                  </p>
                </div>

                <ul className="space-y-1.5 text-[11px] text-textSecondary pt-2 border-t border-border/60">
                  <li className="flex items-center gap-1.5">
                    <Check size={13} className="text-emerald-500 shrink-0" />
                    <span>ออกแบบเอกสารใน MS Word หรือ Excel ที่คุ้นเคย</span>
                  </li>
                  <li className="flex items-center gap-1.5">
                    <Check size={13} className="text-emerald-500 shrink-0" />
                    <span>สแกนแท็ก {'{{...}}'} อัตโนมัติ พร้อมตั้งค่าตารางฟิลด์</span>
                  </li>
                  <li className="flex items-center gap-1.5">
                    <Check size={13} className="text-emerald-500 shrink-0" />
                    <span>เหมาะสำหรับฝ่ายบัญชี นิติกรรม การตลาด หรือ Non-dev</span>
                  </li>
                </ul>
              </div>

              <div className="pt-4 mt-3">
                <Button
                  variant="outline"
                  className="w-full justify-center group-hover:border-emerald-500 group-hover:text-emerald-600"
                  icon={Upload}
                  onClick={handleChooseOffice}
                >
                  อัปโหลดไฟล์แม่แบบ
                </Button>
              </div>
            </div>
          </div>
        </div>
      ) : (
        /* Step 2: HTML Details Form */
        <form onSubmit={handleCreateHtmlTemplate} className="space-y-3 py-1">
          {error && (
            <div className="p-2.5 bg-red-50 border border-red-200 text-red-700 text-xs rounded-[2px]">
              {error}
            </div>
          )}

          <Input
            label="ชื่อแม่แบบ"
            placeholder="เช่น สัญญาจะซื้อจะขายห้องชุด"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            autoFocus
          />

          <Input
            label="Slug (ภาษาอังกฤษ / URL-safe)"
            placeholder="เช่น condo-sales-agreement"
            value={slug}
            onChange={(e) => setSlug(e.target.value.toLowerCase().replace(/[^a-z0-9-_]/g, ''))}
            helperText="ใช้ระบุตัวตนใน REST API เช่น /api/documents/generate/:slug"
            required
          />

          <Select
            label="หมวดหมู่เอกสาร"
            options={[
              { value: 'contract', label: 'สัญญา / นิติกรรม' },
              { value: 'financial', label: 'การเงิน / ใบเสร็จ' },
              { value: 'official', label: 'หนังสือสำคัญ / ราชการ' },
              { value: 'hr', label: 'บุคคล / ภายใน' },
              { value: 'operations', label: 'ปฏิบัติการทั่วไป' },
            ]}
            value={category}
            onChange={(e) => setCategory(e.target.value)}
          />

          <div className="space-y-1.5 pt-1">
            <label className="text-xs font-medium text-textSecondary">โครงสร้างแม่แบบเริ่มต้น</label>
            <div className="grid grid-cols-2 gap-2">
              <div
                onClick={() => setStarterTemplate('standard')}
                className={`p-2.5 border rounded-[2px] cursor-pointer text-left transition-colors ${
                  starterTemplate === 'standard'
                    ? 'border-sky-500 bg-sky-50/50'
                    : 'border-border bg-white hover:bg-slate-50'
                }`}
              >
                <div className="flex items-center gap-1.5 text-xs font-semibold text-textPrimary">
                  <Sparkles size={13} className="text-sky-600" />
                  <span>แบบมาตรฐาน A4</span>
                </div>
                <p className="text-[10px] text-textMuted mt-0.5">มี Header, Footer และโครงสร้างฟอนต์ Sarabun</p>
              </div>

              <div
                onClick={() => setStarterTemplate('blank')}
                className={`p-2.5 border rounded-[2px] cursor-pointer text-left transition-colors ${
                  starterTemplate === 'blank'
                    ? 'border-sky-500 bg-sky-50/50'
                    : 'border-border bg-white hover:bg-slate-50'
                }`}
              >
                <div className="flex items-center gap-1.5 text-xs font-semibold text-textPrimary">
                  <Layers size={13} className="text-slate-600" />
                  <span>หน้าว่าง (Blank)</span>
                </div>
                <p className="text-[10px] text-textMuted mt-0.5">โครงสร้าง HTML พื้นฐานเริ่มต้นจากศูนย์</p>
              </div>
            </div>
          </div>

          <div className="flex items-center justify-between pt-4 border-t border-border mt-4">
            <Button
              type="button"
              variant="ghost"
              icon={ArrowLeft}
              onClick={() => setStep('choice')}
            >
              ย้อนกลับ
            </Button>
            <Button
              type="submit"
              variant="primary"
              loading={loading}
              icon={ArrowRight}
            >
              เปิด Monaco Studio
            </Button>
          </div>
        </form>
      )}
    </Modal>
  );
};
