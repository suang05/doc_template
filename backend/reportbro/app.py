"""
ReportBro HTTP server — wraps reportbro-lib for server-side PDF generation.

API:
  PUT  /api/report  { "report": <layout_json>, "data": <dict>, "outputFormat": "pdf" }
                    → 200 { "key": "<uuid>" }
  GET  /api/report/<key>
                    → 200  PDF bytes   (Content-Type: application/pdf)
                    → 202  { "status": "processing" }
                    → 200  { "status": "error", "errorMessage": "..." }  (generation failed)
  GET  /health      → 200 { "status": "ok" }
"""

import io
import threading
import uuid
from flask import Flask, request, jsonify, send_file

app = Flask(__name__)

# In-memory report store keyed by uuid
# entry: { "status": "processing" | "done" | "error", "data": bytes | None, "errorMessage": str | None }
_store: dict = {}
_lock = threading.Lock()


def _generate(key: str, report_def: dict, data: dict):
    try:
        from reportbro import Report
        report = Report(report_def, data, is_test_data=False)
        # generate_pdf() returns bytearray of PDF bytes; raises on error
        pdf_bytes: bytes = bytes(report.generate_pdf())
        with _lock:
            _store[key] = {"status": "done", "data": pdf_bytes}
    except Exception as exc:
        with _lock:
            _store[key] = {"status": "error", "errorMessage": str(exc)}


@app.put("/api/report")
def submit_report():
    body = request.get_json(force=True, silent=True) or {}
    report_def = body.get("report")
    data = body.get("data") or {}

    if not report_def:
        return jsonify({"error": "Missing 'report' field"}), 400

    key = str(uuid.uuid4())
    with _lock:
        _store[key] = {"status": "processing"}

    threading.Thread(target=_generate, args=(key, report_def, data), daemon=True).start()

    return jsonify({"key": key}), 200


@app.get("/api/report/<key>")
def get_report(key: str):
    with _lock:
        entry = dict(_store.get(key, {}))

    if not entry:
        return jsonify({"status": "not_found"}), 404

    if entry["status"] == "processing":
        return jsonify({"status": "processing"}), 202

    if entry["status"] == "error":
        return jsonify({"status": "error", "errorMessage": entry.get("errorMessage", "unknown")}), 200

    pdf_bytes: bytes = entry["data"]
    return send_file(
        io.BytesIO(pdf_bytes),
        mimetype="application/pdf",
        as_attachment=False,
        download_name="report.pdf",
    )


@app.get("/health")
def health():
    return jsonify({"status": "ok"}), 200


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000)
