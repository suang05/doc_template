'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { BookOpen, Lock, Sparkles, Play } from 'lucide-react';
import { CardBlock, Tabs, CodeBlock, Badge, PageHeader } from '@/components/ui';

const API_BASE = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

export const ApiDocsView: React.FC = () => {
  const [activeTab, setActiveTab] = useState('generate');
  const [codeLang, setCodeLang] = useState<'curl' | 'typescript' | 'csharp'>('curl');

  const tabs = [
    { id: 'generate', label: '1. สร้างเอกสาร (Sync & Pipeline C)' },
    { id: 'preview', label: '2. พรีวิวสด (Preview)' },
    { id: 'audit', label: '3. ตรวจสอบเวอร์ชัน (Audit)' },
    { id: 'transforms', label: '4. ฟังก์ชันไทย (Thai Transforms)' },
  ];

  return (
    <div className="space-y-4 max-w-5xl mx-auto">
      <PageHeader
        title="คู่มือนักพัฒนา (Developer API Reference & Integration Guide)"
        description="สเปกการเชื่อมต่อ REST API ตาม Canonical Contracts พร้อม Idempotency-Key และ Optimistic Concurrency (ETag / If-Match)"
        icon={BookOpen}
        iconColor="sky"
        rightSlot={
          <div className="flex items-center gap-2">
            <Link
              href="/generator"
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-sm bg-primary hover:bg-primary/90 text-white text-xs font-semibold shadow-xs transition-colors"
            >
              <Play className="w-3.5 h-3.5" />
              <span>เปิด Generator Console</span>
            </Link>
            <Badge status="success" dot size="sm">
              OpenAPI SSoT
            </Badge>
          </div>
        }
      />

      <CardBlock className="p-3.5 bg-slate-50 border border-border space-y-2">
        <div className="flex items-center gap-2 text-xs font-bold text-textPrimary">
          <Lock className="w-4 h-4 text-amber-600" />
          <span>การยืนยันตัวตนและมาตรฐานความปลอดภัย (Auth, Idempotency & ETag)</span>
        </div>
        <p className="text-xs text-textSecondary leading-relaxed">
          สำหรับการเชื่อมต่อแบบ Machine-to-Machine ให้ส่ง Header{' '}
          <code className="px-1.5 py-0.5 bg-surface rounded-xs border border-border font-mono text-[11px] text-primary">
            X-API-Key: &lt;YOUR_API_KEY&gt;
          </code>{' '}
          พร้อมระบุ{' '}
          <code className="px-1.5 py-0.5 bg-surface rounded-xs border border-border font-mono text-[11px] text-primary">
            Idempotency-Key: &lt;UUID&gt;
          </code>{' '}
          ทุกครั้งที่ส่งคำสั่งสร้างเอกสารหรือคิวงาน และระบุ{' '}
          <code className="px-1.5 py-0.5 bg-surface rounded-xs border border-border font-mono text-[11px] text-primary">
            If-Match: &quot;...&quot;
          </code>{' '}
          สำหรับการแก้ไขหรือเปลี่ยนสถานะที่ใช้ Optimistic Concurrency
        </p>
      </CardBlock>

      <div className="bg-surface border border-border rounded-sm overflow-hidden">
        <Tabs tabs={tabs} activeTab={activeTab} onChange={setActiveTab} />

        <div className="p-4">
          {activeTab === 'generate' && (
            <div className="space-y-4">
              <div className="space-y-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="px-2 py-0.5 bg-emerald-100 text-emerald-800 rounded-xs font-mono font-bold text-xs">
                    POST
                  </span>
                  <span className="font-mono text-xs font-semibold text-textPrimary">
                    /api/v1/documents
                  </span>
                  <span className="text-[11px] text-textMuted">(Sync • HTTP 201 Created)</span>
                </div>
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="px-2 py-0.5 bg-sky-100 text-sky-800 rounded-xs font-mono font-bold text-xs">
                    POST
                  </span>
                  <span className="font-mono text-xs font-semibold text-textPrimary">
                    /api/v1/document-jobs
                  </span>
                  <span className="text-[11px] text-textMuted">
                    (Pipeline C Async / Batch • HTTP 202 Accepted + Retry-After)
                  </span>
                </div>
                <p className="text-xs text-textSecondary">
                  ผสานข้อมูล JSON เข้ากับแม่แบบ (HTML/Word/Excel) และบันทึกลง MinIO พร้อมออก Pre-signed URL หรือรับประกันการทำงานแบบ Durable Async Job สำหรับงานชุดใหญ่
                </p>
              </div>

              <div className="space-y-2">
                <span className="text-xs font-semibold text-textPrimary">
                  Request Payload (Canonical POST /api/v1/documents):
                </span>
                <CodeBlock
                  code={`{
  "slug": "condo-sales-agreement",
  "payload": {
    "contractNo": "SMK-CONDO-2026-001",
    "customerName": "นายสมชาย ใจดี",
    "unitNo": "A-1204",
    "price": 3500000,
    "effectiveDate": "2026-09-15"
  },
  "outputFormat": "pdf",
  "documentRef": "CONDO-2026-001",
  "changeNote": "ฉบับลงนามจริง"
}`}
                  language="json"
                />
              </div>

              <div className="space-y-2">
                <span className="text-xs font-semibold text-textPrimary">
                  Response 201 Created (Envelope):
                </span>
                <CodeBlock
                  code={`{
  "data": {
    "generationId": "c4d92a18-912f-4a0b-8f3e-0294b3917462",
    "documentRef": "CONDO-2026-001",
    "outputFormat": "pdf",
    "fileSizeBytes": 245100,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "url": "https://storage.example.local/outputs/.../condo-sales-agreement.pdf?token=...",
    "expiresAt": "2026-09-16T19:30:00Z"
  }
}`}
                  language="json"
                />
              </div>

              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-textPrimary">
                    ตัวอย่างโค้ดเรียกใช้งาน (Client Code Example):
                  </span>
                  <div className="flex items-center border border-border rounded-sm overflow-hidden bg-surface text-[11px] font-mono">
                    <button
                      type="button"
                      onClick={() => setCodeLang('curl')}
                      className={`px-2.5 py-1 transition-colors cursor-pointer ${
                        codeLang === 'curl'
                          ? 'bg-primary text-white font-semibold'
                          : 'text-textMuted hover:text-textPrimary'
                      }`}
                    >
                      cURL
                    </button>
                    <button
                      type="button"
                      onClick={() => setCodeLang('typescript')}
                      className={`px-2.5 py-1 transition-colors cursor-pointer ${
                        codeLang === 'typescript'
                          ? 'bg-primary text-white font-semibold'
                          : 'text-textMuted hover:text-textPrimary'
                      }`}
                    >
                      TypeScript
                    </button>
                    <button
                      type="button"
                      onClick={() => setCodeLang('csharp')}
                      className={`px-2.5 py-1 transition-colors cursor-pointer ${
                        codeLang === 'csharp'
                          ? 'bg-primary text-white font-semibold'
                          : 'text-textMuted hover:text-textPrimary'
                      }`}
                    >
                      C# (.NET)
                    </button>
                  </div>
                </div>

                {codeLang === 'curl' && (
                  <CodeBlock
                    code={`curl -X POST "${API_BASE}/api/v1/documents" \\
  -H "X-API-Key: YOUR_API_KEY" \\
  -H "Idempotency-Key: $(uuidgen)" \\
  -H "Content-Type: application/json" \\
  -d '{"slug":"condo-sales-agreement","payload":{"contractNo":"SMK-CONDO-2026-001","customerName":"นายสมชาย ใจดี"},"outputFormat":"pdf"}'`}
                    language="bash"
                  />
                )}

                {codeLang === 'typescript' && (
                  <CodeBlock
                    code={`const response = await fetch('/api/proxy/v1/documents', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Idempotency-Key': crypto.randomUUID(),
  },
  body: JSON.stringify({
    slug: 'condo-sales-agreement',
    payload: {
      contractNo: 'SMK-CONDO-2026-001',
      customerName: 'นายสมชาย ใจดี',
      unitNo: 'A-1204',
      price: 3500000,
    },
    outputFormat: 'pdf',
    documentRef: 'CONDO-2026-001',
  }),
});

const { data } = await response.json();
console.log('Presigned Download URL:', data.url);`}
                    language="typescript"
                  />
                )}

                {codeLang === 'csharp' && (
                  <CodeBlock
                    code={`using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

using var request = new HttpRequestMessage(HttpMethod.Post, "http://document-server:8080/api/v1/documents");
request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
request.Content = JsonContent.Create(new
{
    slug = "condo-sales-agreement",
    payload = new Dictionary<string, object>
    {
        ["contractNo"] = "SMK-CONDO-2026-001",
        ["customerName"] = "นายสมชาย ใจดี",
        ["unitNo"] = "A-1204",
        ["price"] = 3500000
    },
    outputFormat = "pdf",
    documentRef = "CONDO-2026-001"
});

var response = await httpClient.SendAsync(request);`}
                    language="csharp"
                  />
                )}
              </div>
            </div>
          )}

          {activeTab === 'preview' && (
            <div className="space-y-4">
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  <span className="px-2 py-0.5 bg-emerald-100 text-emerald-800 rounded-xs font-mono font-bold text-xs">
                    POST
                  </span>
                  <span className="font-mono text-xs font-semibold text-textPrimary">
                    /api/v1/documents/preview/:slug
                  </span>
                </div>
                <p className="text-xs text-textSecondary">
                  สตรีม PDF ไบนารีกลับมาทันทีสำหรับ Monaco Editor หรือ Form Preview โดยไม่มี Side-effects (ไม่อัปโหลด MinIO, ไม่บันทึก Log)
                </p>
              </div>

              <div className="space-y-2">
                <span className="text-xs font-semibold text-textPrimary">Request Payload (JSON):</span>
                <CodeBlock
                  code={`{
  "payload": {
    "contractNo": "SMK-PREVIEW-01",
    "customerName": "นายทดสอบ จำลอง"
  },
  "html": "<html><body><h1>สัญญา {{contractNo}}</h1><p>ชื่อ: {{customerName}}</p></body></html>"
}`}
                  language="json"
                />
              </div>

              <div className="space-y-2">
                <span className="text-xs font-semibold text-textPrimary">Response 200 OK:</span>
                <p className="text-xs text-textSecondary font-mono bg-surfaceSubtle p-2 rounded-sm border border-border">
                  Content-Type: application/pdf
                  <br />
                  Content-Disposition: inline
                  <br />
                  &lt;PDF Binary Stream&gt;
                </p>
              </div>
            </div>
          )}

          {activeTab === 'audit' && (
            <div className="space-y-4">
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  <span className="px-2 py-0.5 bg-sky-100 text-sky-800 rounded-xs font-mono font-bold text-xs">
                    GET
                  </span>
                  <span className="font-mono text-xs font-semibold text-textPrimary">
                    /api/v1/document-versions/:documentRef/versions
                  </span>
                </div>
                <p className="text-xs text-textSecondary">
                  ดึงประวัติเวอร์ชันเอกสารทางกฎหมายย้อนหลังทั้งหมดตาม Document Ref
                </p>
              </div>

              <div className="space-y-2">
                <span className="text-xs font-semibold text-textPrimary">
                  Response 200 OK (Envelope):
                </span>
                <CodeBlock
                  code={`{
  "data": [
    {
      "id": "f8a9e012-...",
      "documentRef": "CONDO-2026-001",
      "version": 2,
      "templateVersionId": "...",
      "outputKey": "outputs/CONDO-2026-001-v2.pdf",
      "outputFormat": "pdf",
      "changeNote": "ฉบับแก้ไขลายเซ็น",
      "createdAt": "2026-09-15T10:30:00Z"
    }
  ]
}`}
                  language="json"
                />
              </div>
            </div>
          )}

          {activeTab === 'transforms' && (
            <div className="space-y-4">
              <div className="space-y-1">
                <h3 className="text-xs font-bold text-textPrimary flex items-center gap-1.5">
                  <Sparkles className="w-4 h-4 text-amber-500" />
                  <span>รายชื่อฟังก์ชันแปลงข้อมูลภาษาไทย (Built-in Thai Data Transformers)</span>
                </h3>
                <p className="text-xs text-textSecondary">
                  สามารถใช้งานผ่าน Field Mapping ในคอนโซล หรือเขียนแท็ก Inline ในเอกสาร Word/HTML ได้ทันที
                </p>
              </div>

              <div className="border border-border rounded-sm overflow-hidden">
                <table className="w-full text-xs text-left border-collapse">
                  <thead className="bg-surfaceSubtle border-b border-border text-textMuted font-semibold">
                    <tr>
                      <th className="p-2.5">รหัสฟังก์ชัน</th>
                      <th className="p-2.5">รูปแบบ Inline Syntax</th>
                      <th className="p-2.5">ตัวอย่าง Input</th>
                      <th className="p-2.5">ผลลัพธ์ที่แปลงได้ (Output)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border font-mono text-[11px]">
                    <tr>
                      <td className="p-2.5 font-bold text-primary">thai_baht_text</td>
                      <td className="p-2.5 text-slate-700">{'{{amount:thai_baht_text}}'}</td>
                      <td className="p-2.5 text-slate-500">1250000.50</td>
                      <td className="p-2.5 text-emerald-700 font-sans">
                        หนึ่งล้านสองแสนห้าหมื่นบาทห้าสิบสตางค์
                      </td>
                    </tr>
                    <tr>
                      <td className="p-2.5 font-bold text-primary">thai_date</td>
                      <td className="p-2.5 text-slate-700">{'{{date:thai_date}}'}</td>
                      <td className="p-2.5 text-slate-500">&quot;2026-09-15&quot;</td>
                      <td className="p-2.5 text-emerald-700 font-sans">15 กันยายน 2569</td>
                    </tr>
                    <tr>
                      <td className="p-2.5 font-bold text-primary">thai_currency</td>
                      <td className="p-2.5 text-slate-700">{'{{total:thai_currency}}'}</td>
                      <td className="p-2.5 text-slate-500">1250000</td>
                      <td className="p-2.5 text-emerald-700">1,250,000.00</td>
                    </tr>
                    <tr>
                      <td className="p-2.5 font-bold text-primary">thai_id_card</td>
                      <td className="p-2.5 text-slate-700">{'{{id:thai_id_card}}'}</td>
                      <td className="p-2.5 text-slate-500">&quot;1100501234567&quot;</td>
                      <td className="p-2.5 text-emerald-700">1-1005-01234-56-7</td>
                    </tr>
                    <tr>
                      <td className="p-2.5 font-bold text-primary">thai_phone</td>
                      <td className="p-2.5 text-slate-700">{'{{tel:thai_phone}}'}</td>
                      <td className="p-2.5 text-slate-500">&quot;0812345678&quot;</td>
                      <td className="p-2.5 text-emerald-700">081-234-5678</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
