# Sprint 10 — Test Results

**Run date:** 2026-09-13  
**Framework:** xUnit.net v3.1.4 · .NET 10.0  
**Project:** `SmkDocServer.Tests` → references `SmkDocServer`  
**Result:** ✅ **16 / 16 Passed — 0 Failed — 0 Skipped**  
**Total time:** 2.55 seconds

---

## MathExpressionResolverTests

| # | Test Name | Category | Expression | Input | Expected | Result | Time |
|---|-----------|----------|-----------|-------|----------|--------|------|
| 1 | `Addition_ReturnsCorrectResult` | Arithmetic | `a + b` | a=10, b=5 | `"15"` | ✅ Pass | 2 ms |
| 2 | `Subtraction_ReturnsCorrectResult` | Arithmetic | `total - discount` | total=100, discount=15 | `"85"` | ✅ Pass | <1 ms |
| 3 | `Multiplication_ReturnsCorrectResult` | Arithmetic | `qty * unitPrice` | qty=3, unitPrice=250.50 | `"751.5"` | ✅ Pass | <1 ms |
| 4 | `Division_ReturnsCorrectResult` | Arithmetic | `total / n` | total=100, n=4 | `"25"` | ✅ Pass | 1 ms |
| 5 | `InvoiceSubtotal_MultipleItems` | Invoice | `qty1 * price1 + qty2 * price2` | qty1=2, price1=500, qty2=5, price2=120 | `"1600"` | ✅ Pass | <1 ms |
| 6 | `Vat7Percent_CalculatedCorrectly` | Invoice | `subtotal * 0.07` | subtotal=1000 | `"70"` | ✅ Pass | <1 ms |
| 7 | `GrandTotal_SubtotalPlusVat` | Invoice | `subtotal + vat` | subtotal=1000, vat=70 | `"1070"` | ✅ Pass | 49 ms |
| 8 | `Round_NotSupported_GracefulFallback` | Limitation | `ROUND(val / 7, 2)` | val=3 | not null, not empty | ✅ Pass | 3 ms |
| 9 | `IIF_ConditionalExpression` | Conditional | `IIF(qty > 5, 100, 0)` | qty=10 | `"100"` | ✅ Pass | 1 ms |
| 10 | `LiteralExpression_NoVars` | Arithmetic | `10 + 20 * 3` | (no vars) | `"70"` | ✅ Pass | <1 ms |
| 11 | `NonNumericVariable_SkippedAndExpressionReturnedAsIs` | Edge Case | `qty * 100` | qty=5, name="สมชาย" | `"500"` | ✅ Pass | 4 ms |
| 12 | `NullVariable_Skipped` | Edge Case | `qty * price` | qty=null, price=100 | not null | ✅ Pass | 6 ms |
| 13 | `EmptyVariables_LiteralOnly` | Edge Case | `100 + 50` | (empty dict) | `"150"` | ✅ Pass | <1 ms |
| 14 | `ZeroDivision_GracefulFallback` | Edge Case | `x / y` | x=10, y=0 | not null | ✅ Pass | <1 ms |
| 15 | `DecimalInput_InvariantCulture` | Edge Case | `price * qty` | price=1234.56, qty=2 | `"2469.12"` | ✅ Pass | <1 ms |
| 16 | `Precedence_MultiplyBeforeAdd` | Arithmetic | `a + b * c` | a=2, b=3, c=4 | `"14"` | ✅ Pass | 1 ms |

---

## ข้อค้นพบระหว่างทำ Test

### DataTable.Compute — ขอบเขตที่รองรับจริง

| สิ่งที่ทดสอบ | รองรับ | หมายเหตุ |
|-------------|--------|---------|
| `+`, `-`, `*`, `/` | ✅ | รองรับเต็ม |
| Parentheses `(...)` | ✅ | ใช้บังคับ precedence ได้ |
| Operator precedence | ✅ | `*` ก่อน `+` ถูกต้อง |
| Decimal literals (`0.07`) | ✅ | InvariantCulture |
| `IIF(cond, true, false)` | ✅ | Conditional expression |
| `ROUND(x, n)` 2-arg | ❌ | ไม่รองรับ → graceful fallback |
| Math functions (`ABS`, `SQRT`) | ❌ | ไม่รองรับ → graceful fallback |
| Zero division | ❌ → ✅ | Compute throws → resolver คืน expression text |
| Null variable | skip → ✅ | ตัวแปร null ถูก skip การ substitute |
| Non-numeric variable | skip → ✅ | ตัวแปรที่ parse decimal ไม่ได้ถูก skip |

### Bug ที่ค้นพบและแก้แล้ว

| ไฟล์ | บรรทัด | ปัญหา | การแก้ |
|------|--------|-------|--------|
| `MathExpressionResolver.cs` | comment line 9 | ระบุว่า `ROUND`, `ABS`, `SUM` รองรับ — ผิด | แก้ comment ให้ถูกต้อง |
| `FieldMappingService.cs` | 137 | `resolvedVars!` unnecessary null-forgiving | ลบ `!` |
| `FieldMappingService.cs` | 178 | `docData.Replace!` unnecessary null-forgiving | ลบ `!` |

---

## สรุป Sprint 10 Coverage

```
Class: MathExpressionResolver
├── Arithmetic operators (+, -, *, /)     ████████ 100%  (tests 1-4, 10, 16)
├── Invoice calculation scenario          ████████ 100%  (tests 5, 6, 7)
├── Conditional (IIF)                     ████████ 100%  (test 9)
├── Graceful fallback paths               ████████ 100%  (tests 8, 14)
└── Edge cases (null, non-numeric, locale)████████ 100%  (tests 11, 12, 13, 15)

Classes NOT unit-tested (require DB/integration):
├── DataConnectionService    → tested via API (.http scenarios)
├── FieldMappingService      → tested via API (.http scenarios)
└── DataConnectionController → tested via API (.http scenarios)
```

---

## Files

| ไฟล์ | คำอธิบาย |
|------|---------|
| [`backend.Tests/MathExpressionResolverTests.cs`](../../backend.Tests/MathExpressionResolverTests.cs) | Unit tests (xUnit) |
| [`docs/tests/sprint10-api-tests.http`](sprint10-api-tests.http) | API scenarios — VS Code REST Client |
| [`docs/tests/sprint10-e2e-scenario.md`](sprint10-e2e-scenario.md) | E2E scenario ใบแจ้งหนี้ครบวงจร |
