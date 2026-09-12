using System;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        /// <summary>
        /// Shows quantities measured from the exact final plan shared by Preview and Create.
        /// "đoạn" means actual XC/XP drawing entities, not procurement stock bars.
        /// </summary>
        public void SetPreviewActualStats(VxtFinalPlanMetrics metrics)
        {
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));

            Summary =
                $"XC {metrics.MainCount} đoạn • {metrics.MainLengthM:0.00} m   |   " +
                $"XP {metrics.FurringCount} đoạn • {metrics.FurringLengthM:0.00} m\n" +
                $"Ty {metrics.HangerCount}  •  DIM {metrics.DimensionCount}";
            PreviewStatus = "✓ Đã cập nhật xem trước • số lượng/chiều dài lấy từ hình học cuối • chưa ghi đối tượng vào bản vẽ";
        }
    }
}
