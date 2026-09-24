export const PRESETS = {
  sample_full_features: {
    template: 'sample_full_features.docx',
    format: 'pdf' as const,
    data: {
      invoice_no: 'INV-SMK-2026-00892',
      date: '8 มีนาคม 2569',
      customer_name: 'คุณวิเชียร สมบูรณ์ทรัพย์',
      description: 'เงินทำสัญญาจะซื้อจะขายที่ดินพร้อมสิ่งปลูกสร้าง โครงการ พาร์ค เฮอริเทจ พัฒนาการ',
      amount: '500,000.00 บาท',
      payment_qr: 'https://www.sammakorn.co.th/pay/INV-SMK-2026-00892',
      inv_barcode: 'SMK8850123456789'
    }
  },
  invoice: {
    template: 'invoice.docx',
    format: 'pdf' as const,
    data: {
      invoice_no: 'INV-2026-001',
      date: '8 มี.ค. 2569',
      customer_name: 'บริษัท สัมมากร จำกัด (มหาชน)',
      description: 'ค่าบริการบริหารจัดการและพัฒนาโครงการ',
      amount: '120,000.00'
    }
  },
  username: {
    template: 'username.docx',
    format: 'pdf' as const,
    data: {
      CustomerName: 'สมชาย รักสัมมากร',
      Price: 3500000,
      Date: '2026-03-08'
    }
  },
  asd_complaint_report: {
    template: 'asd_complaint_report.docx',
    format: 'pdf' as const,
    data: {
      company_name: 'บริษัท สัมมากร จำกัด (มหาชน)',
      project_name: 'มิตติ ชัยพฤกษ์-วงแหวน (NB6)',
      report_period: '1 - 31 สิงหาคม 2569',
      department: 'หน่วยงานซ่อมบำรุง / บริการหลังการขาย',
      print_date: '8 กันยายน 2569',
      printed_by: 'เฉลิมชัย ตรัยถาวรวงศ์',
      no: '1',
      case_no: 'NB6-69-012',
      plot: 'B-08',
      house_no: '88/19',
      reporter: 'คุณณัฐพล เจริญผล',
      tel: '081-456-7890',
      topic: 'หมวดงานซ่อมบ้าน (งานโครงสร้างและรอยร้าว)',
      detail: 'รอยร้าวบริเวณบัวหน้าต่างห้องนอนใหญ่ชั้น 2 และสีกะเทาะ',
      appointment_date: '12/09/2569',
      status: 'นัดหมายแล้ว'
    }
  },
  asd_timeline_report: {
    template: 'asd_timeline_report.docx',
    format: 'pdf' as const,
    data: {
      project_name: 'โครงการ มีนบุรี 1 (MB1)',
      case_no: 'MB1-69-045',
      plot: 'A-15',
      house_no: '124/8',
      customer_name: 'คุณวิเชียร สมบูรณ์ทรัพย์',
      customer_tel: '089-123-4567',
      warranty_status: 'อยู่ในประกัน (Claim)',
      topic: 'หมวดงานซ่อมบ้าน',
      sub_topic: 'งานสุขาภิบาลและท่อประปา',
      complaint_detail: 'น้ำซึมจากท่อระบายน้ำใต้ซิงค์ล้างจานห้องครัวชั้น 1',
      status: 'ปิดงานเสร็จสิ้น',
      case_qr_url: 'https://www.sammakorn.co.th/asd/case/MB1-69-045',
      data_source: 'Contact Center 1427',
      request_date: '15 ส.ค. 2569',
      intake_by: 'จนท. ประสานงาน CC 1427',
      survey_date: '16 ส.ค. 2569',
      surveyor_name: 'นายสมควร ช่างทอง (วิศวกรสำรวจ)',
      repair_date: '18 ส.ค. 2569',
      contractor_name: 'ทีมช่างสุขาภิบาล บจก.เอสเอ็มเค เซอร์วิส',
      close_date: '19 ส.ค. 2569',
      inspector_name: 'นายเฉลิมชัย ตรัยถาวรวงศ์ (วิศวกรโครงการ)',
      caption_before: 'ท่อ PVC ใต้ซิงค์มีน้ำหยดซึมบริเวณข้อต่อ',
      caption_after: 'เปลี่ยนชุดท่อใหม่และยาแนวเรียบร้อย ไม่พบรอยรั่วซึม'
    }
  },
  asd_pending_repairs: {
    template: 'asd_pending_repairs.xlsx',
    format: 'xlsx' as const,
    data: {
      project_name: 'โครงการ นิมิตใหม่ (NM)',
      as_of_date: '8 กันยายน 2569',
      printed_by: 'เฉลิมชัย ตรัยถาวรวงศ์',
      total_pending: 18,
      within_sla: 14,
      over_sla: 4,
      waiting_parts: 3,
      cases: [
        {
          no: 1,
          project: 'NM',
          case_no: 'NM-69-035',
          plot: 'P04',
          house_no: '99/4',
          customer: 'คุณสมศักดิ์ วงศ์สวัสดิ์',
          request_date: '27/08/2569',
          aging_days: 12,
          topic: 'สาธารณูปโภค (ท่อระบายน้ำ)',
          delay_reason: 'รอชิ้นส่วนฝาปิดบ่อพักสั่งทำพิเศษ',
          staff: 'นายประเสริฐ ช่างซ่อม',
          status: 'รออะไหล่'
        },
        {
          no: 2,
          project: 'NM',
          case_no: 'NM-69-034',
          plot: 'P08',
          house_no: '99/8',
          customer: 'คุณวิภาวรรณ ชัยเจริญ',
          request_date: '27/08/2569',
          aging_days: 12,
          topic: 'งานสีและพื้นผิว',
          delay_reason: 'ฝนตกต่อเนื่อง ไม่สามารถทาสีภายนอกได้',
          staff: 'นายอำนาจ พลอยดี',
          status: 'รอนัดหมาย'
        },
        {
          no: 3,
          project: 'NM',
          case_no: 'NM-69-033',
          plot: 'A12',
          house_no: '105/12',
          customer: 'คุณธนากร ภักดี',
          request_date: '18/08/2569',
          aging_days: 21,
          topic: 'งานโครงสร้างหลังคา',
          delay_reason: 'รอทีมช่างเฉพาะทางเข้าตรวจสอบซ้ำ',
          staff: 'วิศวกรโครงสร้าง SMK',
          status: 'เกินกำหนด SLA'
        }
      ]
    }
  },
  asd_renovation_refund: {
    template: 'asd_renovation_refund.docx',
    format: 'pdf' as const,
    data: {
      doc_no: 'REF-ASD-2026-0045',
      issue_date: '8 กันยายน 2569',
      owner_name: 'คุณเกษม ชัยเจริญ',
      house_no: '55/12',
      plot: 'B-04',
      project_name: 'อเวนิว สุวรรณภูมิ (LB1)',
      permit_no: 'MOD-LB1-2569-018',
      permit_date: '15 พฤษภาคม 2569',
      renovation_type: 'ต่อเติมหลังคาที่จอดรถหน้าบ้านแบบโครงเหล็กน้ำหนักเบา',
      deposit_amount: '50,000.00',
      deposit_amount_text: 'ห้าหมื่นบาทถ้วน',
      inspection_date: '2 กันยายน 2569',
      deduction_amount: '0.00',
      refund_amount: '50,000.00',
      refund_amount_text: 'ห้าหมื่นบาทถ้วน',
      bank_name: 'ธนาคารไทยพาณิชย์ จำกัด (มหาชน)',
      bank_account: '045-2-98765-4',
      account_name: 'นายเกษม ชัยเจริญ',
      inspector_name: 'นายธนกฤต ช่างตรวจ (วิศวกรโครงการ)',
      authorized_manager: 'นายเกียรติศักดิ์ บริหาร (ผจก.ฝ่ายบริการหลังการขาย)'
    }
  },
  asd_house_warranty: {
    template: 'asd_house_warranty.xlsx',
    format: 'xlsx' as const,
    data: {
      project_name: 'โครงการ ชัยพฤกษ์-วงแหวน 2 (NB5)',
      print_date: '8 กันยายน 2569',
      houses: [
        {
          no: 1,
          plot: 'A01',
          house_no: '18/1',
          model: 'พาร์ค พรีเมียม 2 ชั้น',
          owner_name: 'คุณธีรพงษ์ มั่นคง',
          tel: '081-888-9999',
          transfer_date: '10/01/2568',
          handover_date: '15/01/2568',
          warranty_start: '15/01/2568',
          warranty_end: '15/01/2570',
          warranty_status: 'อยู่ในประกัน',
          remarks: 'ลูกบ้านรับมอบกุญแจครบชุด'
        },
        {
          no: 2,
          plot: 'A02',
          house_no: '18/2',
          model: 'พาร์ค วิลล่า',
          owner_name: 'คุณสุดาพร พงษ์ไพศาล',
          tel: '086-777-1234',
          transfer_date: '20/02/2568',
          handover_date: '25/02/2568',
          warranty_start: '25/02/2568',
          warranty_end: '25/02/2570',
          warranty_status: 'อยู่ในประกัน',
          remarks: 'มีการยื่นขอต่อเติมครัวหลังบ้าน'
        },
        {
          no: 3,
          plot: 'B05',
          house_no: '18/15',
          model: 'พาร์ค พรีเมียม 2 ชั้น',
          owner_name: 'คุณประดิษฐ์ รักษ์ไทย',
          tel: '089-555-4321',
          transfer_date: '05/06/2566',
          handover_date: '10/06/2566',
          warranty_start: '10/06/2566',
          warranty_end: '10/06/2568',
          warranty_status: 'หมดประกัน (Out of Warranty)',
          remarks: 'ต่อสัญญา Home Care Service รายปี'
        }
      ]
    }
  }
};
