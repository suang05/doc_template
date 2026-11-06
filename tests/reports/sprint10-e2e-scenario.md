# Sprint 10 — E2E Test Scenario: ใบแจ้งหนี้อัตโนมัติ

## Scenario: "ใบแจ้งหนี้คำนวณยอดอัตโนมัติ + ดึงชื่อลูกค้าจาก DB"

### ระบบที่เกี่ยวข้อง
- **expr**: คำนวณ subtotal, VAT 7%, grandTotal จาก qty × unitPrice ที่รับมาใน JSON
- **sql**: ดึง customerName, taxId จาก PostgreSQL ด้วย customer_id ที่รับมาใน JSON
- **json**: ข้อมูลพื้นฐาน (invoiceNo, date, remark) มาจาก payload โดยตรง

---

## Step 1 — ตั้งค่า DataConnection

สร้าง DataConnection ชี้ไปที่ DB ที่มีตาราง `customers`:

```sql
-- ตาราง customers ใน PostgreSQL
CREATE TABLE customers (
  id SERIAL PRIMARY KEY,
  name VARCHAR(150) NOT NULL,
  tax_id VARCHAR(13),
  address TEXT
);
INSERT INTO customers VALUES (1, 'บริษัท สยามพาณิชย์ จำกัด', '0105555000001', '99/9 ถ.สีลม กรุงเทพ 10500');
INSERT INTO customers VALUES (2, 'ห้างหุ้นส่วน ไทยเทรด', '0993344556677', '55 ถ.เจริญกรุง กรุงเทพ 10120');
```

**API Call**:
```
POST /api/DataConnection
{ "name": "ERP DB", "databaseType": "postgresql", "connectionString": "Host=db;Database=erp;..." }
→ Response: { "id": "abc-123-...", "name": "ERP DB", "connectionString": null }
```

---

## Step 2 — ตั้งค่า FieldMapping (บน template "invoice.docx")

| # | Placeholder | Label | DataSourceType | SourcePath / Expression | SortOrder |
|---|-------------|-------|----------------|------------------------|-----------|
| 1 | `invoiceNo` | เลขที่ใบแจ้งหนี้ | json | `invoiceNo` | 0 |
| 2 | `invoiceDate` | วันที่ | json | `date` | 1 |
| 3 | `customerId` | รหัสลูกค้า | json | `customerId` | 2 |
| 4 | `customerName` | ชื่อลูกค้า | **sql** | `name` (column) | 3 |
| 5 | `customerTaxId` | เลขประจำตัวผู้เสียภาษี | **sql** | `tax_id` | 4 |
| 6 | `qty` | จำนวน | json | `items[0].quantity` | 5 |
| 7 | `unitPrice` | ราคาต่อหน่วย | json | `items[0].unitPrice` | 6 |
| 8 | `subtotal` | ยอดก่อน VAT | **expr** | `qty * unitPrice` | 7 |
| 9 | `vat` | VAT 7% | **expr** | `subtotal * 0.07` | 8 |
| 10 | `grandTotal` | ยอดรวมสุทธิ | **expr** | `subtotal + vat` | 9 |

**SQL Query สำหรับ customerName และ customerTaxId** (ใช้ query เดียวกัน — cached):
```sql
SELECT name, tax_id FROM customers WHERE id = {{customerId}}
```
หมายเหตุ: ปัจจุบัน SQL ใน FieldMapping เป็น static string — ใส่ customer id โดยตรง เช่น `WHERE id = 1`

---

## Step 3 — Generate Document

**Request Payload**:
```json
POST /api/Document/generate
{
  "templateFileName": "invoice.docx",
  "outputFormat": "docx",
  "data": {
    "invoiceNo": "INV-2026-001",
    "date": "2026-09-13",
    "customerId": "1",
    "items": [
      { "quantity": "10", "unitPrice": "850" }
    ]
  }
}
```

**การประมวลผล (TryApplyMappingAsync — ตามลำดับ SortOrder)**:

```
SortOrder 0:  invoiceNo     ← json "invoiceNo"           = "INV-2026-001"
SortOrder 1:  invoiceDate   ← json "date"                = "2026-09-13"
SortOrder 2:  customerId    ← json "customerId"          = "1"
SortOrder 3:  customerName  ← sql (execute once, cached) = "บริษัท สยามพาณิชย์ จำกัด"
SortOrder 4:  customerTaxId ← sql (cache hit, no query)  = "0105555000001"
SortOrder 5:  qty           ← json "items[0].quantity"   = "10"
SortOrder 6:  unitPrice     ← json "items[0].unitPrice"  = "850"
SortOrder 7:  subtotal      ← expr "qty * unitPrice"     = "8500"
SortOrder 8:  vat           ← expr "subtotal * 0.07"     = "595"
SortOrder 9:  grandTotal    ← expr "subtotal + vat"      = "9095"
```

**ผลที่ได้ในเอกสาร**:

| Placeholder | ค่าใน Document |
|-------------|---------------|
| `{{invoiceNo}}` | INV-2026-001 |
| `{{invoiceDate}}` | 2026-09-13 |
| `{{customerName}}` | บริษัท สยามพาณิชย์ จำกัด |
| `{{customerTaxId}}` | 0105555000001 |
| `{{qty}}` | 10 |
| `{{unitPrice}}` | 850 |
| `{{subtotal}}` | 8500 |
| `{{vat}}` | 595 |
| `{{grandTotal}}` | 9095 |

---

## ข้อจำกัดที่ต้องรู้ (Discovered from Unit Tests)

| ข้อ | รายละเอียด |
|-----|-----------|
| **expr SortOrder** | `expr` mapping ต้องมี SortOrder สูงกว่า JSON mapping ที่เป็น source ของมัน (user config responsibility) |
| **ROUND ไม่รองรับ** | `DataTable.Compute()` ไม่รองรับ `ROUND(x, n)` — ใช้แค่ arithmetic operators (+, -, *, /) |
| **IIF รองรับ** | `IIF(condition, trueVal, falseVal)` ใช้ได้ เช่น `IIF(qty > 100, subtotal * 0.9, subtotal)` |
| **SQL static query** | SQL ใน FieldMapping เป็น string คงที่ — ยังไม่ inject ค่าจาก JSON (Sprint 11 ถ้ามี) |
| **SQL PostgreSQL only** | รองรับแค่ Npgsql — DatabaseType อื่นยังไม่ implement |
| **Data Protection keys** | Key เข้ารหัสอยู่ใน container — recreate container = decrypt ไม่ได้ (acceptable for dev) |

---

## Unit Test Coverage (16/16 ✅)

```
MathExpressionResolverTests
├── Addition_ReturnsCorrectResult               ✅
├── Subtraction_ReturnsCorrectResult            ✅
├── Multiplication_ReturnsCorrectResult         ✅  qty × unitPrice
├── Division_ReturnsCorrectResult               ✅
├── InvoiceSubtotal_MultipleItems               ✅  qty1*p1 + qty2*p2
├── Vat7Percent_CalculatedCorrectly             ✅  subtotal * 0.07
├── GrandTotal_SubtotalPlusVat                  ✅  subtotal + vat
├── Round_NotSupported_GracefulFallback         ✅  ไม่ throw, fallback gracefully
├── IIF_ConditionalExpression                   ✅  IIF(qty > 5, 100, 0)
├── LiteralExpression_NoVars                    ✅  10 + 20 * 3 = 70
├── NonNumericVariable_SkippedAndExpr...        ✅  "สมชาย" skipped, qty ยังคำนวณได้
├── NullVariable_Skipped                        ✅  null variable → no throw
├── EmptyVariables_LiteralOnly                  ✅
├── ZeroDivision_GracefulFallback               ✅  ไม่ throw
├── DecimalInput_InvariantCulture               ✅  1234.56 (dot, not comma)
└── Precedence_MultiplyBeforeAdd                ✅  2 + 3*4 = 14
```
