# HNL CAD AI v2.8.1 — Bridge P0 Golden Verification

## Scope
Chỉ xử lý P0 số 1: Bridge. Không thêm workflow CAD mới.

## P0 defects/risk fixed
1. Timer poll có thể chồng lần gọi khi HTTP chậm → thêm Interlocked single-flight guard.
2. Browser timeout nhưng action cũ có thể còn chạy muộn → action mang timeout/deadline; plugin chặn action hết hạn trước queue và trước execution.
3. Client timeout không dọn queue → thêm cancel endpoint và client best-effort cancel.
4. Queue TTL cũ 120 giây ngắn hơn Publish budget 180 giây → bỏ TTL cứng; prune theo deadline từng action.
5. Action không thuộc capability vẫn vào queue → reject 422 ngay tại server.
6. Bridge khó chứng minh round-trip thật → thêm read-only Golden Smoke GET_STATUS → GET_DRAFTING_STATUS → GET_LAYOUTS → GET_LAYERS.
7. Chẩn đoán thiếu bằng chứng instance/heartbeat/poll → thêm bridgeInstanceId, PID, last heartbeat, last poll, last error.

## Runtime Golden gate
Trong HNL: Diagnostics → **Golden Test Bridge**.
PASS phải trả `BRIDGE_GOLDEN_READ_ONLY` và 4 action đều ok, có latency, pluginVersion, AutoCAD version, drawingName.

## Promotion rule
- BRG-01 chỉ đổi REVIEW → VERIFIED sau khi Golden Test Bridge PASS trên AutoCAD thật.
- BRG-02 chỉ đổi REVIEW → VERIFIED sau Golden PASS + stress: 20 lần test liên tiếp, thử AutoCAD busy/ESC/restart HNL, không có action chạy muộn.

Static PASS không được dùng thay cho runtime evidence.
