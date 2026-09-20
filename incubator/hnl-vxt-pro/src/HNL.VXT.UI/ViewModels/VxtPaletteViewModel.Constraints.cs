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

        public ObservableCollection<VxtConstraintDiagnostic> ConstraintDiagnostics { get; }
            = new ObservableCollection<VxtConstraintDiagnostic>();

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

            if (ConstraintDiagnostics.Count == 0) return;

            var hard = ConstraintDiagnostics.Count(x => x.IsHard);
            PreviewStatus = hard > 0
                ? "⛔ Có vi phạm HARD. Create sẽ bị chặn; bấm Mxx để xác định Polyline."
                : "⚠ Có Min mềm đã dùng. Bấm Mxx để xác định Polyline và xem giá trị giảm.";
        }
    }
}
