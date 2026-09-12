# HNL VXT Pro UI Specification

## Branding
- Logo HNL ở header Palette.
- Dòng chính: `HNL Tool - Vẽ Xương Trần`.
- Ghi chú phiên bản ngay dưới: `HNL Tool - VXT Pro v7.0.0-alpha.1`.
- Logo gọn, không chiếm diện tích thao tác.

## Palette
1. Phạm vi & hướng bố trí.
2. Xương chính XC.
3. Xương phụ XP.
4. Ty treo.
5. Dimension.
6. Live Preview.
7. Footer actions.

## DIM UX
Mỗi nhóm XC / XP / Ty có:
- checkbox bật/tắt;
- vị trí `Auto / Top / Bottom / Left / Right`;
- nút `Chọn vị trí...` để pick trực tiếp ngoài CAD.

Khi pick, VXT xác định cạnh gần nhất + khoảng cách DIM và cập nhật transient preview ngay trên Model Space.

## Preview AutoCAD ACI
- Boundary: 8.
- XC: 4.
- XP: 2.
- Ty: 3.
- DIM: 6.
- Direction: 5.
- Avoidance/conflict: 1.

Màu preview không thay đổi layer/màu của đối tượng thật.
