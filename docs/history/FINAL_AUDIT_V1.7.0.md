# Kiểm tra phát hành v2.0.0

- TypeScript/TSX transpile: PASS 64 files.
- Electron main/preload syntax: PASS.
- SketchUp Ruby syntax: PASS.
- RBZ integrity: PASS.
- Diagnostic/Error logging: giữ nguyên và nối Command Health.
- Geometry destructive fixes: yêu cầu confirm.
- Cleanup: có mức Safe/Standard/Aggressive.
- Recovery: 5 generations.
- Publish: chỉ báo READY nội bộ; không giả xác nhận CTB/Xref native khi AutoCAD Bridge offline.

## Test thực tế cần chạy trên Windows
1. Build NSIS EXE.
2. New/Open/Save/Recovery.
3. Geometry Doctor trên file có duplicate/self-crossing.
4. Drawing Compare 2 revision.
5. Command Health sau 20–30 command.
6. Core Self-Test.
7. SketchUp `.rbz` trên SketchUp thực.
8. DXF roundtrip với AutoCAD.
9. File 10k/50k/100k entities.
