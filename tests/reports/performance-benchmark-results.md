# SMK Document Server — Performance & Concurrency Benchmark Report

**วันที่ทดสอบ:** 2026-09-18  
**สภาพแวดล้อม:** Docker V2 Stack (smk-v2) · Gotenberg 8 · PostgreSQL 16 · MinIO S3 · .NET 10 (Release)

---

## 📊 1. การทดสอบปริมาณข้อมูล 1,000 แถว (1,000 Rows Data Density Benchmark)

ทดสอบการประมวลผลข้อมูลธุรกรรมจริง (Financial & Statement Transactions) จำนวน 1,000 แถว ในแต่ละเอนจิน:

| Template Engine | ประเภทการประมวลผล | เวลาที่ใช้ (Latency) | Throughput (แถว/วินาที) | ขนาดไฟล์ Output | จำนวนหน้า |
|---|---|:---:|:---:|:---:|:---:|
| **HTML Engine (Handlebars)** | Merge ข้อมูลลงในเทมเพลต | **55 ms** | **18,182 แถว/วินาที** | 348.7 KB (HTML) | - |
| **Word Engine (OpenXML)** | ขยายแถวตารางแบบไดนามิก | **454 ms** | **2,203 แถว/วินาที** | 3.7 KB (.docx) | ~18 หน้า |
| **Excel Engine (ClosedXML)** | แทรกแถว + Auto-shift สูตร SUM | **991 ms** | **1,009 แถว/วินาที** | 39.5 KB (.xlsx) | 1 Sheet |
| **Gotenberg Chromium** | เรนเดอร์ HTML 1,000 แถว เป็น PDF | **5,609 ms (5.6s)** | **178 แถว/วินาที** | 315 KB (PDF) | **46 หน้า A4** |

> **จุดเด่น:** เอกสาร PDF 1,000 แถว ความยาว **46 หน้า** ใช้เวลาเพียง **5.6 วินาที** โดยมี Running Header / Footer พร้อมตัวเลขนับหน้า หน้าที่ 1 / 46 จนถึง 46 / 46 แสดงผลครบทุกหน้า

---

## 🚀 2. การทดสอบ Throughput และ Concurrency ของระบบ (API Load Benchmark)

ทดสอบการยิงคำขอสร้างเอกสารจริงผ่าน Endpoint POST /api/documents/generate/test-receipt-v2 แบบ Concurrent:

* **จำนวนคำขอ (Total Requests):** 20 คำขอ
* **ระดับ Concurrency:** 4 Workers พร้อมกัน
* **อัตราความสำเร็จ (Success Rate):** **100% (20/20)**
* **ระยะเวลารวม (Total Elapsed):** 5.79 วินาที
* **Throughput ของระบบ (Documents TPS):** **3.45 เอกสาร/วินาที**
* **Average Latency:** 1,152 ms
* **P95 Latency:** 1,400 ms
