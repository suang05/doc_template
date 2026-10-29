/**
 * Monaco Editor Completion Provider for SMK Document Server Template Studio
 * Provides IntelliSense, snippets, transforms, and field suggestions for HTML templates.
 */

export interface MonacoCompletionOptions {
  getAvailableFields?: () => string[];
}

export function registerTemplateCompletion(
  monaco: any,
  options?: MonacoCompletionOptions
) {
  if (!monaco?.languages) return { dispose: () => {} };

  const disposable = monaco.languages.registerCompletionItemProvider('html', {
    triggerCharacters: ['{', ':', '#', '@', '<', ' '],
    provideCompletionItems: (model: any, position: any) => {
      const textUntilPosition = model.getValueInRange({
        startLineNumber: position.lineNumber,
        startColumn: 1,
        endLineNumber: position.lineNumber,
        endColumn: position.column,
      });

      const word = model.getWordUntilPosition(position);
      const range = {
        startLineNumber: position.lineNumber,
        endLineNumber: position.lineNumber,
        startColumn: word.startColumn,
        endColumn: word.endColumn,
      };

      const suggestions: any[] = [];
      const itemKind = monaco.languages.CompletionItemKind;
      const insertRule = monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet;

      // ── 1. Handlebars Blocks & Logic Helpers ──────────────────────────────
      suggestions.push(
        {
          label: '{{#each items}}',
          kind: itemKind.Snippet,
          insertText: '{{#each ${1:items}}}\n<tr>\n  <td>{{addOne @index}}</td>\n  <td>{{${2:name}}}</td>\n  <td style="text-align: right;">{{${3:amount}:number}}</td>\n</tr>\n{{/each}}',
          insertTextRules: insertRule,
          detail: 'วนลูปตารางรายการ (Handlebars #each Loop)',
          documentation: 'วนลูปสมาชิกใน Array สำหรับทำแถวตาราง พร้อม {{addOne @index}} และฟิลด์ย่อย',
          range,
          sortText: '01_each',
        },
        {
          label: '{{#if condition}}',
          kind: itemKind.Snippet,
          insertText: '{{#if ${1:condition}}}\n  ${2:<!-- แสดงเมื่อเงื่อนไขเป็นจริง -->}\n{{else}}\n  ${3:<!-- แสดงเมื่อเงื่อนไขเป็นเท็จ -->}\n{{/if}}',
          insertTextRules: insertRule,
          detail: 'ตรวจสอบเงื่อนไข (Handlebars #if Condition)',
          documentation: 'แสดงผลบล็อกเนื้อหาตามเงื่อนไข True/False หรือความมีอยู่ของข้อมูล',
          range,
          sortText: '02_if',
        },
        {
          label: '{{#ifEquals val1 val2}}',
          kind: itemKind.Snippet,
          insertText: '{{#ifEquals ${1:status} "${2:APPROVED}"}}\n  <span class="badge">${3:อนุมัติแล้ว}</span>\n{{else}}\n  <span>${4:รออนุมัติ}</span>\n{{/ifEquals}}',
          insertTextRules: insertRule,
          detail: 'เปรียบเทียบค่าเท่ากัน (SMK Helper #ifEquals)',
          documentation: 'เปรียบเทียบค่า String หรือ Number สองค่า เช่น status == "APPROVED"',
          range,
          sortText: '03_ifEquals',
        },
        {
          label: '{{addOne @index}}',
          kind: itemKind.Function,
          insertText: '{{addOne @index}}',
          detail: 'ลำดับที่ 1-based (SMK Helper addOne)',
          documentation: 'แปลง index ของลูปจาก 0 เริ่มต้นเป็น 1, 2, 3... สำหรับแสดงในตาราง',
          range,
          sortText: '04_addOne',
        },
        {
          label: '{{inc @index}}',
          kind: itemKind.Function,
          insertText: '{{inc @index}}',
          detail: 'ลำดับที่ 1-based (Alias ของ addOne)',
          documentation: 'แปลง index ของลูปจาก 0 เป็น 1, 2, 3...',
          range,
          sortText: '05_inc',
        }
      );

      // ── 2. Document & Gotenberg Structure Snippets ───────────────────────
      suggestions.push(
        {
          label: 'template-header',
          kind: itemKind.Snippet,
          insertText: '<template id="header">\n  <div style="font-family: \'Sarabun\', sans-serif; font-size: 8pt; color: #64748b; width: 100%; border-bottom: 1px solid #cbd5e1; padding-bottom: 4px; display: flex; justify-content: space-between;">\n    <span>${1:บริษัท สัมมากร จำกัด (มหาชน)}</span>\n    <span>${2:เอกสารสำคัญ}</span>\n  </div>\n</template>',
          insertTextRules: insertRule,
          detail: 'ส่วนหัวกระดาษ PDF (<template id="header">)',
          documentation: 'แท็กหัวกระดาษสำหรับ Gotenberg Chromium จะแสดงซ้ำทุกหน้า',
          range,
          sortText: '10_header',
        },
        {
          label: 'template-footer',
          kind: itemKind.Snippet,
          insertText: '<template id="footer">\n  <div style="font-family: \'Sarabun\', sans-serif; font-size: 8pt; color: #94a3b8; width: 100%; border-top: 1px solid #cbd5e1; padding-top: 4px; display: flex; justify-content: space-between;">\n    <span>${1:พิมพ์จากระบบ SMK Document Server}</span>\n    <span>หน้าที่ <span class="pageNumber"></span> / <span class="totalPages"></span></span>\n  </div>\n</template>',
          insertTextRules: insertRule,
          detail: 'ส่วนท้ายกระดาษพร้อมเลขหน้า (<template id="footer">)',
          documentation: 'แท็กท้ายกระดาษ Gotenberg รองรับ class pageNumber และ totalPages อัตโนมัติ',
          range,
          sortText: '11_footer',
        },
        {
          label: 'page-break',
          kind: itemKind.Snippet,
          insertText: '<div style="page-break-after: always;"></div>',
          detail: 'ตัวแบ่งหน้ากระดาษ A4 (Page Break)',
          documentation: 'บังคับให้เนื้อหาถัดไปขึ้นหน้ากระดาษใหม่ใน PDF',
          range,
          sortText: '12_pagebreak',
        },
        {
          label: 'table-a4',
          kind: itemKind.Snippet,
          insertText: '<table style="width: 100%; border-collapse: collapse; margin-top: 10px; font-size: 10pt;">\n  <thead>\n    <tr style="background: #f8fafc; border-bottom: 1.5px solid #cbd5e1;">\n      <th style="padding: 6px 8px; text-align: center; width: 45px;">#</th>\n      <th style="padding: 6px 8px; text-align: left;">${1:รายละเอียด}</th>\n      <th style="padding: 6px 8px; text-align: right; width: 120px;">${2:จำนวนเงิน}</th>\n    </tr>\n  </thead>\n  <tbody>\n    {{#each ${3:items}}}\n    <tr style="border-bottom: 1px solid #e2e8f0;">\n      <td style="padding: 6px 8px; text-align: center;">{{addOne @index}}</td>\n      <td style="padding: 6px 8px;">{{${4:description}}}</td>\n      <td style="padding: 6px 8px; text-align: right;">{{${5:amount}:number}}</td>\n    </tr>\n    {{/each}}\n  </tbody>\n</table>',
          insertTextRules: insertRule,
          detail: 'ตารางข้อมูล A4 มาตรฐาน (Table + Loop)',
          documentation: 'ตารางสไตล์ทางการพร้อมหัวตารางและลูปแถวรายการ',
          range,
          sortText: '13_table',
        }
      );

      // ── 3. Media Placeholders (QR, Barcode, Image) ───────────────────────
      suggestions.push(
        {
          label: '{{qr:key}}',
          kind: itemKind.Snippet,
          insertText: '{{qr:${1:trackingUrl}}}',
          insertTextRules: insertRule,
          detail: 'สร้างภาพ QR Code จากข้อความ/URL',
          documentation: 'สร้าง QR Code ขนาด 150x150 อัตโนมัติจากค่าในฟิลด์',
          range,
          sortText: '20_qr',
        },
        {
          label: '{{barcode:key}}',
          kind: itemKind.Snippet,
          insertText: '{{barcode:${1:referenceNo}}}',
          insertTextRules: insertRule,
          detail: 'สร้างบาร์โค้ด Code128',
          documentation: 'สร้างภาพ Barcode ขนาด 300x100 อัตโนมัติจากค่าในฟิลด์',
          range,
          sortText: '21_barcode',
        },
        {
          label: '{{image:key}}',
          kind: itemKind.Snippet,
          insertText: '{{image:${1:signatureUrl}}}',
          insertTextRules: insertRule,
          detail: 'แทรกรูปภาพจาก URL หรือ Base64',
          documentation: 'แทรกภาพ <img> จาก URL หรือ Data URI',
          range,
          sortText: '22_image',
        }
      );

      // ── 4. Transform Filters (:thai_baht_text, :number, etc.) ────────────
      const isInsidePlaceholder = textUntilPosition.includes('{{') && !textUntilPosition.endsWith('}}');
      const hasColon = textUntilPosition.endsWith(':');

      if (isInsidePlaceholder || hasColon) {
        suggestions.push(
          {
            label: ':thai_baht_text',
            kind: itemKind.Property,
            insertText: hasColon ? 'thai_baht_text}}' : ':thai_baht_text}}',
            detail: 'แปลงตัวเลขเป็นตัวหนังสือบาทไทย',
            documentation: 'ตัวอย่าง: 1250.50 → (หนึ่งพันสองร้อยห้าสิบบาทห้าสิบสตางค์)',
            range,
            sortText: '30_baht',
          },
          {
            label: ':number',
            kind: itemKind.Property,
            insertText: hasColon ? 'number}}' : ':number}}',
            detail: 'จัดรูปแบบตัวเลขทศนิยมมีคอมม่า',
            documentation: 'ตัวอย่าง: 1250000.5 → 1,250,000.50',
            range,
            sortText: '31_number',
          },
          {
            label: ':date',
            kind: itemKind.Property,
            insertText: hasColon ? 'date}}' : ':date}}',
            detail: 'จัดรูปแบบวันที่สากล ค.ศ. (DD/MM/YYYY)',
            documentation: 'ตัวอย่าง: 2026-09-19 → 19/09/2026',
            range,
            sortText: '32_date',
          },
          {
            label: ':thai_date',
            kind: itemKind.Property,
            insertText: hasColon ? 'thai_date}}' : ':thai_date}}',
            detail: 'จัดรูปแบบวันที่ไทย พ.ศ. แบบเต็ม',
            documentation: 'ตัวอย่าง: 2026-09-19 → 19 กันยายน 2569',
            range,
            sortText: '33_thai_date',
          },
          {
            label: ':uppercase',
            kind: itemKind.Property,
            insertText: hasColon ? 'uppercase}}' : ':uppercase}}',
            detail: 'แปลงข้อความเป็นตัวพิมพ์ใหญ่',
            range,
            sortText: '34_upper',
          },
          {
            label: ':lowercase',
            kind: itemKind.Property,
            insertText: hasColon ? 'lowercase}}' : ':lowercase}}',
            detail: 'แปลงข้อความเป็นตัวพิมพ์เล็ก',
            range,
            sortText: '35_lower',
          }
        );
      }

      // ── 5. Available Field Suggestions (From Template / Mappings) ────────
      const availableFields = options?.getAvailableFields?.() || [];
      if (availableFields.length > 0) {
        availableFields.forEach((field) => {
          suggestions.push({
            label: `{{${field}}}`,
            kind: itemKind.Field,
            insertText: `{{${field}}}`,
            detail: `ฟิลด์ข้อมูลจากเทมเพลต (${field})`,
            documentation: `ดึงค่าของ ${field} จากข้อมูลนำเข้า JSON`,
            range,
            sortText: `40_${field}`,
          });
          suggestions.push({
            label: `{{${field}:number}}`,
            kind: itemKind.Field,
            insertText: `{{${field}:number}}`,
            detail: `ฟิลด์ตัวเลข (${field})`,
            range,
            sortText: `41_${field}_num`,
          });
          suggestions.push({
            label: `{{${field}:thai_baht_text}}`,
            kind: itemKind.Field,
            insertText: `{{${field}:thai_baht_text}}`,
            detail: `ฟิลด์ตัวหนังสือบาท (${field})`,
            range,
            sortText: `42_${field}_baht`,
          });
        });
      }

      return { suggestions };
    },
  });

  return disposable;
}
