# GitHub Actions Build Fix v2.0.5

Sửa đúng các lỗi `npm run lint` từ workflow GitHub:

1. `SketchUp2DBridgeModal.tsx` TS2339 (`layer/color/lineweight` on `unknown`)
   - khai báo `Map<string, any>` rõ kiểu;
   - tuple map được annotate `[string, any]`;
   - lookup `sourceTag` được ép sang `String`.

2. `plotPublishEngine.ts` TS2749 (`s refers to a value, but is being used as a type`)
   - đổi `let status:s["status"]` thành `let status:PlotSheet["status"]`.

Workflow vẫn chạy theo thứ tự:
`npm install` → `npm run lint` → `npm run dist:win` → verify EXE → upload artifact.

Artifact mới: `HNL-CAD-AI-v2.0.5-Windows-Installer`.
