# AI trong SketchUp ⇄ CAD — nguyên tắc v2.0.0

AI được tích hợp, nhưng chỉ ở vùng phù hợp.

## AI được phép
- gợi ý SketchUp Tag → CAD Layer;
- đề xuất màu ByLayer / lineweight;
- nhận diện nhóm Wall / Ceiling / Door / Window / MEP / Furniture theo tên Tag;
- cảnh báo tên layer/tag bất thường;
- đề xuất preset Shopdrawing/Architecture/Ceiling;
- giải thích Diagnostic Report.

## AI không được tự quyết
- đường CUT contour;
- hidden-line do occlusion;
- xóa hình học;
- merge/join geometry;
- thay gốc tọa độ;
- overwrite file;
- Publish/SaveAs DWG mà không có xác nhận.

Các quyết định hình học phải đi qua geometry/rule engine. Nếu AI online không khả dụng, hệ thống dùng rule-based fallback.
