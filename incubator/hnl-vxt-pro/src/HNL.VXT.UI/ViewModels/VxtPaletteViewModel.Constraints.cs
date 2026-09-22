using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using HNL.VXT.Core.Preview;
using HNL.VXT.UI.Infrastructure;

namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        private ICommand _focusConstraintDiagnosticCommand;
        private string _constraintSummary = "✓ Không phát hiện lỗi";

        public ObservableCollection<VxtConstraintDiagnostic> ConstraintDiagnostics { get; }
            = new ObservableCollection<VxtConstraintDiagnostic>();

        public string ConstraintSummary
        {
            get => _constraintSummary;
            private set => Set(ref _constraintSummary, value);
        }

        public ICommand FocusConstraintDiagnosticCommand
        {
            get
            {
                if (_focusConstraintDiagnosticCommand == null)
                {
                    _focusConstraintDiagnosticCommand = new RelayCommand(parameter =>
                    {
                        var item = parameter as VxtConstraintDiagnostic;
                        if (item != null) _host.HighlightBoundary(item.BoundaryIndex);
                    });
                }
                return _focusConstraintDiagnosticCommand;
            }
        }

        public void SetConstraintDiagnostics(IEnumerable<VxtConstraintDiagnostic> diagnostics)
        {
            ConstraintDiagnostics.Clear();
            foreach (var item in (diagnostics ?? Enumerable.Empty<VxtConstraintDiagnostic>())
                .Where(x => x != null)
                .OrderBy(x => x.BoundaryIndex)
                .ThenByDescending(x => x.Severity)
                .ThenBy(x => x.Target)
                .ThenBy(x => x.Kind))
            {
                ConstraintDiagnostics.Add(item);
            }

            if (ConstraintDiagnostics.Count == 0)
            {
                ConstraintSummary = "✓ Không phát hiện lỗi";
                return;
            }

            var hard = ConstraintDiagnostics.Count(x => x.IsHard);
            var warnings = ConstraintDiagnostics.Count - hard;
            ConstraintSummary = (hard > 0 ? "⛔ " + hard + " lỗi" : string.Empty) +
                                (hard > 0 && warnings > 0 ? " • " : string.Empty) +
                                (warnings > 0 ? "⚠ " + warnings + " cảnh báo" : string.Empty);

            PreviewStatus = hard > 0
                ? "⛔ Có " + hard + " lỗi bố trí. Tạo sẽ bị chặn; mở Kiểm tra bố trí và bấm Mxx để xem đúng Polyline."
                : "⚠ Có " + warnings + " cảnh báo bố trí. Mở Kiểm tra bố trí và bấm Mxx để xem chi tiết.";
        }
    }
}
