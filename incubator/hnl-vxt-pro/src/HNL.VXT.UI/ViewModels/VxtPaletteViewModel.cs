using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HNL.VXT.Core.Models;
using HNL.VXT.UI.Hosting;
using HNL.VXT.UI.Infrastructure;

namespace HNL.VXT.UI.ViewModels
{
    public sealed class VxtPaletteViewModel : INotifyPropertyChanged
    {
        private readonly IVxtHostBridge _host;
        private VxtSettings _settings = new VxtSettings();
        private string _boundaryStatus = "Chưa chọn biên trần";
        private string _previewStatus = "Chọn Polyline kín để bắt đầu xem trước.";
        private string _summary = "XC --  •  XP --  •  TY --  •  DIM --";
        private string _selectedPreset = "Trần chìm tiêu chuẩn";
        private string _generalEquipmentStatus = "Chưa chọn";
        private string _mainEquipmentStatus = "Chưa chọn";
        private string _furringEquipmentStatus = "Chưa chọn";
        private bool _hasBoundary;
        private bool _applyingPreset;

        public VxtPaletteViewModel(IVxtHostBridge host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            SelectBoundaryCommand = new RelayCommand(() => _host.SelectBoundary());
            PickDirectionCommand = new RelayCommand(
                () => _host.PickDirection(_settings.MainDirection),
                () => _settings.MainDirection == MainDirectionMode.TwoPoints ||
                      _settings.MainDirection == MainDirectionMode.RectangleRegions);

            PickMainBlockCommand = new RelayCommand(() => _host.PickBlock(BlockTarget.Main));
            PickFurringBlockCommand = new RelayCommand(() => _host.PickBlock(BlockTarget.Furring));
            PickHangerBlockCommand = new RelayCommand(() => _host.PickBlock(BlockTarget.Hanger));

            PickGeneralEquipmentCommand = new RelayCommand(() => _host.PickEquipment(EquipmentTarget.General));
            PickMainEquipmentCommand = new RelayCommand(() => _host.PickEquipment(EquipmentTarget.Main));
            PickFurringEquipmentCommand = new RelayCommand(() => _host.PickEquipment(EquipmentTarget.Furring));

            PickMainDimCommand = new RelayCommand(() => _host.PickDimensionPosition(DimensionTarget.Main));
            PickFurringDimCommand = new RelayCommand(() => _host.PickDimensionPosition(DimensionTarget.Furring));
            PickHangerDimCommand = new RelayCommand(() => _host.PickDimensionPosition(DimensionTarget.Hanger));

            RefreshPreviewCommand = new RelayCommand(RequestPreview);
            ResetCommand = new RelayCommand(ResetDefaults);
            ClearPreviewCommand = new RelayCommand(() => _host.ClearPreview());
            ExportDiagnosticsCommand = new RelayCommand(() => _host.ExportDiagnostics(_settings.Clone()));
            CreateCommand = new RelayCommand(() => _host.RequestCreate(), () => CanCreate);
        }

        public string VersionLabel => "VXT Pro v7.0.0-alpha.3";
        public string Subtitle => "WYSIWYG Preview • tương thích V6.7.4 • AutoCAD 2023–2027";
        public bool IsDarkTheme => _host.IsDarkTheme;

        public string[] Presets { get; } = { "Trần chìm tiêu chuẩn", "Tùy chỉnh" };
        public string[] MainDirectionOptions { get; } =
        {
            "Theo phương ngang",
            "Theo phương dọc",
            "Chọn hướng bằng 2 điểm",
            "Chia vùng bằng hình chữ nhật",
            "Tự động chọn hướng"
        };
        public string[] MainLayoutOptions { get; } =
        {
            "Tự động",
            "Cân đều hai đầu",
            "Dồn về một phía"
        };
        public string[] HangerLayoutOptions { get; } =
        {
            "Cân đều hai đầu",
            "Dồn theo Xương phụ"
        };
        public string[] DimensionPositionOptions { get; } =
        {
            "Tự động",
            "Phía trên",
            "Phía dưới",
            "Bên trái",
            "Bên phải"
        };

        public string SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (!Set(ref _selectedPreset, value)) return;
                if (!_applyingPreset && value == "Trần chìm tiêu chuẩn")
                    ApplyStandardPreset();
            }
        }

        public ICommand SelectBoundaryCommand { get; }
        public ICommand PickDirectionCommand { get; }
        public ICommand PickMainBlockCommand { get; }
        public ICommand PickFurringBlockCommand { get; }
        public ICommand PickHangerBlockCommand { get; }
        public ICommand PickGeneralEquipmentCommand { get; }
        public ICommand PickMainEquipmentCommand { get; }
        public ICommand PickFurringEquipmentCommand { get; }
        public ICommand PickMainDimCommand { get; }
        public ICommand PickFurringDimCommand { get; }
        public ICommand PickHangerDimCommand { get; }
        public ICommand RefreshPreviewCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand ClearPreviewCommand { get; }
        public ICommand ExportDiagnosticsCommand { get; }
        public ICommand CreateCommand { get; }

        public bool HasBoundary
        {
            get => _hasBoundary;
            private set
            {
                if (Set(ref _hasBoundary, value))
                {
                    OnPropertyChanged(nameof(CanCreate));
                    (CreateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // Locked until Golden parity is verified.
        public bool CanCreate => false;

        public string BoundaryStatus { get => _boundaryStatus; private set => Set(ref _boundaryStatus, value); }
        public string PreviewStatus { get => _previewStatus; private set => Set(ref _previewStatus, value); }
        public string Summary { get => _summary; private set => Set(ref _summary, value); }
        public string GeneralEquipmentStatus { get => _generalEquipmentStatus; private set => Set(ref _generalEquipmentStatus, value); }
        public string MainEquipmentStatus { get => _mainEquipmentStatus; private set => Set(ref _mainEquipmentStatus, value); }
        public string FurringEquipmentStatus { get => _furringEquipmentStatus; private set => Set(ref _furringEquipmentStatus, value); }

        // XƯƠNG CHÍNH
        public bool DrawMain { get => _settings.DrawMain; set => SetSetting(ref _settings.DrawMain, value); }
        public bool UseDynamicMainBlock { get => _settings.UseDynamicMainBlock; set => SetSetting(ref _settings.UseDynamicMainBlock, value); }
        public string MainBlockName { get => _settings.MainBlockName; set => SetStringSetting(() => _settings.MainBlockName, v => _settings.MainBlockName = v, value); }
        public double MainMinSpacing { get => _settings.MainMinSpacing; set => SetNumberSetting(() => _settings.MainMinSpacing, v => _settings.MainMinSpacing = v, value); }
        public double MainMaxSpacing { get => _settings.MainMaxSpacing; set => SetNumberSetting(() => _settings.MainMaxSpacing, v => _settings.MainMaxSpacing = v, value); }
        public double MainMinEdgeOffset { get => _settings.MainMinEdgeOffset; set => SetNumberSetting(() => _settings.MainMinEdgeOffset, v => _settings.MainMinEdgeOffset = v, value); }
        public double MainMaxEdgeOffset { get => _settings.MainMaxEdgeOffset; set => SetNumberSetting(() => _settings.MainMaxEdgeOffset, v => _settings.MainMaxEdgeOffset = v, value); }
        public double MainBalanceStep { get => _settings.MainBalanceStep; set => SetNumberSetting(() => _settings.MainBalanceStep, v => _settings.MainBalanceStep = v, value); }
        public double MainSkipLimit { get => _settings.MainSkipLimit; set => SetNumberSetting(() => _settings.MainSkipLimit, v => _settings.MainSkipLimit = v, value); }

        public string SelectedMainDirection
        {
            get => MainDirectionToText(_settings.MainDirection);
            set
            {
                var mode = TextToMainDirection(value);
                if (_settings.MainDirection == mode) return;
                _settings.MainDirection = mode;
                if (mode == MainDirectionMode.Horizontal) _settings.DirectionDegrees = 0.0;
                if (mode == MainDirectionMode.Vertical) _settings.DirectionDegrees = 90.0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DirectionDegrees));
                MarkCustom();
                (PickDirectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                RequestPreview();
            }
        }

        public string SelectedMainLayout
        {
            get => MainLayoutToText(_settings.MainLayout);
            set
            {
                var mode = TextToMainLayout(value);
                if (_settings.MainLayout == mode) return;
                _settings.MainLayout = mode;
                Changed();
            }
        }

        // XƯƠNG PHỤ
        public bool DrawFurring { get => _settings.DrawFurring; set => SetSetting(ref _settings.DrawFurring, value); }
        public bool UseDynamicFurringBlock { get => _settings.UseDynamicFurringBlock; set => SetSetting(ref _settings.UseDynamicFurringBlock, value); }
        public string FurringBlockName { get => _settings.FurringBlockName; set => SetStringSetting(() => _settings.FurringBlockName, v => _settings.FurringBlockName = v, value); }
        public double FurringSpacing { get => _settings.FurringSpacing; set => SetNumberSetting(() => _settings.FurringSpacing, v => _settings.FurringSpacing = v, value); }
        public bool AskDirectionEachRegion { get => _settings.AskDirectionEachRegion; set => SetSetting(ref _settings.AskDirectionEachRegion, value); }

        // TY TREO
        public bool DrawHangers { get => _settings.DrawHangers; set => SetSetting(ref _settings.DrawHangers, value); }
        public string HangerBlockName { get => _settings.HangerBlockName; set => SetStringSetting(() => _settings.HangerBlockName, v => _settings.HangerBlockName = v, value); }
        public double HangerMinSpacing { get => _settings.HangerMinSpacing; set => SetNumberSetting(() => _settings.HangerMinSpacing, v => _settings.HangerMinSpacing = v, value); }
        public double HangerMaxSpacing { get => _settings.HangerMaxSpacing; set => SetNumberSetting(() => _settings.HangerMaxSpacing, v => _settings.HangerMaxSpacing = v, value); }
        public double HangerMinEdgeOffset { get => _settings.HangerMinEdgeOffset; set => SetNumberSetting(() => _settings.HangerMinEdgeOffset, v => _settings.HangerMinEdgeOffset = v, value); }
        public double HangerMaxEdgeOffset { get => _settings.HangerMaxEdgeOffset; set => SetNumberSetting(() => _settings.HangerMaxEdgeOffset, v => _settings.HangerMaxEdgeOffset = v, value); }
        public double HangerBalanceStep { get => _settings.HangerBalanceStep; set => SetNumberSetting(() => _settings.HangerBalanceStep, v => _settings.HangerBalanceStep = v, value); }

        public string SelectedHangerLayout
        {
            get => HangerLayoutToText(_settings.HangerLayout);
            set
            {
                var mode = TextToHangerLayout(value);
                if (_settings.HangerLayout == mode) return;
                _settings.HangerLayout = mode;
                Changed();
            }
        }

        // NÉ THIẾT BỊ
        public bool UseAvoidance { get => _settings.UseAvoidance; set => SetSetting(ref _settings.UseAvoidance, value); }
        public bool ShiftAllForAvoidance { get => _settings.ShiftAllForAvoidance; set => SetSetting(ref _settings.ShiftAllForAvoidance, value); }
        public double ClearanceDistance { get => _settings.ClearanceDistance; set => SetNumberSetting(() => _settings.ClearanceDistance, v => _settings.ClearanceDistance = v, value); }

        // DIM
        public bool AutoDimension { get => _settings.AutoDimension; set => SetSetting(ref _settings.AutoDimension, value); }
        public bool DimMain { get => _settings.DimMain; set => SetSetting(ref _settings.DimMain, value); }
        public bool DimFurring { get => _settings.DimFurring; set => SetSetting(ref _settings.DimFurring, value); }
        public bool DimHanger { get => _settings.DimHanger; set => SetSetting(ref _settings.DimHanger, value); }

        public string SelectedMainDimPosition
        {
            get => DimensionPositionToText(_settings.MainDimPosition);
            set => SetDimensionPosition(DimensionTarget.Main, TextToDimensionPosition(value));
        }
        public string SelectedFurringDimPosition
        {
            get => DimensionPositionToText(_settings.FurringDimPosition);
            set => SetDimensionPosition(DimensionTarget.Furring, TextToDimensionPosition(value));
        }
        public string SelectedHangerDimPosition
        {
            get => DimensionPositionToText(_settings.HangerDimPosition);
            set => SetDimensionPosition(DimensionTarget.Hanger, TextToDimensionPosition(value));
        }

        public double DimensionDistance { get => _settings.DimensionDistance; set => SetNumberSetting(() => _settings.DimensionDistance, v => _settings.DimensionDistance = v, value); }
        public double DimensionSpacing { get => _settings.DimensionSpacing; set => SetNumberSetting(() => _settings.DimensionSpacing, v => _settings.DimensionSpacing = v, value); }
        public double DirectionDegrees { get => _settings.DirectionDegrees; set => SetDirectionFromInput(value); }

        public VxtSettings Snapshot() => _settings.Clone();

        public void SetBoundaryStatus(string display, bool hasBoundary)
        {
            BoundaryStatus = display;
            HasBoundary = hasBoundary;
            PreviewStatus = hasBoundary
                ? "Xem trước đang bật • thay đổi thông số để cập nhật."
                : "Chọn Polyline kín để bắt đầu xem trước.";
        }

        public void SetDirection(double degrees)
        {
            _settings.MainDirection = MainDirectionMode.TwoPoints;
            _settings.DirectionDegrees = Normalize(degrees);
            MarkCustom();
            OnPropertyChanged(nameof(SelectedMainDirection));
            OnPropertyChanged(nameof(DirectionDegrees));
            (PickDirectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            RequestPreview();
        }

        public void SetBlock(BlockTarget target, string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName)) return;
            switch (target)
            {
                case BlockTarget.Main:
                    _settings.MainBlockName = blockName;
                    OnPropertyChanged(nameof(MainBlockName));
                    break;
                case BlockTarget.Furring:
                    _settings.FurringBlockName = blockName;
                    OnPropertyChanged(nameof(FurringBlockName));
                    break;
                case BlockTarget.Hanger:
                    _settings.HangerBlockName = blockName;
                    OnPropertyChanged(nameof(HangerBlockName));
                    break;
            }
            MarkCustom();
        }

        public void SetEquipmentStatus(EquipmentTarget target, int count)
        {
            var text = count > 0 ? $"Đã chọn: {count}" : "Chưa chọn";
            switch (target)
            {
                case EquipmentTarget.General: GeneralEquipmentStatus = text; break;
                case EquipmentTarget.Main: MainEquipmentStatus = text; break;
                case EquipmentTarget.Furring: FurringEquipmentStatus = text; break;
            }
        }

        public void SetDimensionPick(DimensionTarget target, DimensionPosition position, double distance)
        {
            switch (target)
            {
                case DimensionTarget.Main:
                    _settings.MainDimPosition = position;
                    OnPropertyChanged(nameof(SelectedMainDimPosition));
                    break;
                case DimensionTarget.Furring:
                    _settings.FurringDimPosition = position;
                    OnPropertyChanged(nameof(SelectedFurringDimPosition));
                    break;
                case DimensionTarget.Hanger:
                    _settings.HangerDimPosition = position;
                    OnPropertyChanged(nameof(SelectedHangerDimPosition));
                    break;
            }
            _settings.DimensionDistance = Math.Max(0, distance);
            MarkCustom();
            OnPropertyChanged(nameof(DimensionDistance));
            RequestPreview();
        }

        public void SetPreviewStats(int main, int furring, int hangers, int dims)
        {
            Summary = $"Xương chính {main}  •  Xương phụ {furring}  •  Ty {hangers}  •  DIM {dims}";
            PreviewStatus = "✓ Đã cập nhật xem trước • chưa ghi đối tượng vào bản vẽ";
        }

        public void SetPreviewError(string message) => PreviewStatus = "⚠ " + message;

        private void SetDirectionFromInput(double value)
        {
            var normalized = Normalize(value);
            if (Near(_settings.DirectionDegrees, normalized)) return;
            _settings.DirectionDegrees = normalized;
            if (!Near(normalized, 0.0) && !Near(normalized, 90.0))
                _settings.MainDirection = MainDirectionMode.TwoPoints;
            OnPropertyChanged(nameof(DirectionDegrees));
            OnPropertyChanged(nameof(SelectedMainDirection));
            MarkCustom();
            RequestPreview();
        }

        private void SetDimensionPosition(DimensionTarget target, DimensionPosition value)
        {
            var changed = false;
            switch (target)
            {
                case DimensionTarget.Main:
                    changed = _settings.MainDimPosition != value;
                    _settings.MainDimPosition = value;
                    break;
                case DimensionTarget.Furring:
                    changed = _settings.FurringDimPosition != value;
                    _settings.FurringDimPosition = value;
                    break;
                case DimensionTarget.Hanger:
                    changed = _settings.HangerDimPosition != value;
                    _settings.HangerDimPosition = value;
                    break;
            }
            if (changed) Changed();
        }

        private void Changed([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            MarkCustom();
            RequestPreview();
        }

        private void SetSetting(ref bool field, bool value, [CallerMemberName] string propertyName = null)
        {
            if (field == value) return;
            field = value;
            Changed(propertyName);
        }

        private void SetNumberSetting(Func<double> getter, Action<double> setter, double value, [CallerMemberName] string propertyName = null)
        {
            if (Near(getter(), value)) return;
            setter(value);
            Changed(propertyName);
        }

        private void SetStringSetting(Func<string> getter, Action<string> setter, string value, [CallerMemberName] string propertyName = null)
        {
            value = value ?? string.Empty;
            if (string.Equals(getter(), value, StringComparison.Ordinal)) return;
            setter(value);
            Changed(propertyName);
        }

        private void MarkCustom()
        {
            if (_applyingPreset || _selectedPreset == "Tùy chỉnh") return;
            _selectedPreset = "Tùy chỉnh";
            OnPropertyChanged(nameof(SelectedPreset));
        }

        private void RequestPreview()
        {
            if (!HasBoundary) return;
            if (!_settings.IsValid(out var error))
            {
                SetPreviewError(error);
                return;
            }
            _host.RequestPreview(_settings.Clone());
        }

        private void ApplyStandardPreset()
        {
            _applyingPreset = true;
            _settings = new VxtSettings();
            OnPropertyChanged(string.Empty);
            _applyingPreset = false;
            (PickDirectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            RequestPreview();
        }

        private void ResetDefaults()
        {
            _applyingPreset = true;
            _settings = new VxtSettings();
            _selectedPreset = "Trần chìm tiêu chuẩn";
            _generalEquipmentStatus = "Chưa chọn";
            _mainEquipmentStatus = "Chưa chọn";
            _furringEquipmentStatus = "Chưa chọn";
            OnPropertyChanged(string.Empty);
            _applyingPreset = false;
            (PickDirectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            RequestPreview();
        }

        private static string MainDirectionToText(MainDirectionMode mode)
        {
            switch (mode)
            {
                case MainDirectionMode.Vertical: return "Theo phương dọc";
                case MainDirectionMode.TwoPoints: return "Chọn hướng bằng 2 điểm";
                case MainDirectionMode.RectangleRegions: return "Chia vùng bằng hình chữ nhật";
                case MainDirectionMode.Auto: return "Tự động chọn hướng";
                default: return "Theo phương ngang";
            }
        }

        private static MainDirectionMode TextToMainDirection(string value)
        {
            switch (value)
            {
                case "Theo phương dọc": return MainDirectionMode.Vertical;
                case "Chọn hướng bằng 2 điểm": return MainDirectionMode.TwoPoints;
                case "Chia vùng bằng hình chữ nhật": return MainDirectionMode.RectangleRegions;
                case "Tự động chọn hướng": return MainDirectionMode.Auto;
                default: return MainDirectionMode.Horizontal;
            }
        }

        private static string MainLayoutToText(MainLayoutMode mode)
        {
            switch (mode)
            {
                case MainLayoutMode.Auto: return "Tự động";
                case MainLayoutMode.OneSide: return "Dồn về một phía";
                default: return "Cân đều hai đầu";
            }
        }

        private static MainLayoutMode TextToMainLayout(string value)
        {
            switch (value)
            {
                case "Tự động": return MainLayoutMode.Auto;
                case "Dồn về một phía": return MainLayoutMode.OneSide;
                default: return MainLayoutMode.BalancedTwoEnds;
            }
        }

        private static string HangerLayoutToText(HangerLayoutMode mode)
            => mode == HangerLayoutMode.OneSideFollowFurring ? "Dồn theo Xương phụ" : "Cân đều hai đầu";

        private static HangerLayoutMode TextToHangerLayout(string value)
            => value == "Dồn theo Xương phụ" ? HangerLayoutMode.OneSideFollowFurring : HangerLayoutMode.BalancedTwoEnds;

        private static string DimensionPositionToText(DimensionPosition position)
        {
            switch (position)
            {
                case DimensionPosition.Top: return "Phía trên";
                case DimensionPosition.Bottom: return "Phía dưới";
                case DimensionPosition.Left: return "Bên trái";
                case DimensionPosition.Right: return "Bên phải";
                default: return "Tự động";
            }
        }

        private static DimensionPosition TextToDimensionPosition(string value)
        {
            switch (value)
            {
                case "Phía trên": return DimensionPosition.Top;
                case "Phía dưới": return DimensionPosition.Bottom;
                case "Bên trái": return DimensionPosition.Left;
                case "Bên phải": return DimensionPosition.Right;
                default: return DimensionPosition.Auto;
            }
        }

        private static double Normalize(double value)
        {
            value %= 360.0;
            return value < 0 ? value + 360.0 : value;
        }

        private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-8;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
