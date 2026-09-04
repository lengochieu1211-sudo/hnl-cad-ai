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
        private string _previewStatus = "Chọn Polyline kín để bắt đầu Live Preview.";
        private string _summary = "XC --  •  XP --  •  TY --  •  DIM --";
        private string _selectedPreset = "Trần chìm tiêu chuẩn";
        private bool _hasBoundary;
        private bool _applyingPreset;

        public VxtPaletteViewModel(IVxtHostBridge host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            SelectBoundaryCommand = new RelayCommand(() => _host.SelectBoundary());
            PickDirectionCommand = new RelayCommand(() => _host.PickDirection());
            PickMainDimCommand = new RelayCommand(() => _host.PickDimensionPosition(DimensionTarget.Main));
            PickFurringDimCommand = new RelayCommand(() => _host.PickDimensionPosition(DimensionTarget.Furring));
            PickHangerDimCommand = new RelayCommand(() => _host.PickDimensionPosition(DimensionTarget.Hanger));
            RefreshPreviewCommand = new RelayCommand(RequestPreview);
            ResetCommand = new RelayCommand(ResetDefaults);
            ClearPreviewCommand = new RelayCommand(() => _host.ClearPreview());
            CreateCommand = new RelayCommand(() => _host.RequestCreate(), () => CanCreate);
        }

        public string VersionLabel => "HNL Tool - VXT Pro v7.0.0-alpha.2";
        public string Subtitle => "Professional UI • Universal AutoCAD 2023–2027";
        public bool IsDarkTheme => _host.IsDarkTheme;
        public string[] Presets { get; } = { "Trần chìm tiêu chuẩn", "Tùy chỉnh" };

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
        public ICommand PickMainDimCommand { get; }
        public ICommand PickFurringDimCommand { get; }
        public ICommand PickHangerDimCommand { get; }
        public ICommand RefreshPreviewCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand ClearPreviewCommand { get; }
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

        public bool DrawMain { get => _settings.DrawMain; set { if (_settings.DrawMain != value) { _settings.DrawMain = value; Changed(); } } }
        public double MainMinSpacing { get => _settings.MainMinSpacing; set { if (!Near(_settings.MainMinSpacing, value)) { _settings.MainMinSpacing = value; Changed(); } } }
        public double MainMaxSpacing { get => _settings.MainMaxSpacing; set { if (!Near(_settings.MainMaxSpacing, value)) { _settings.MainMaxSpacing = value; Changed(); } } }

        public bool DrawFurring { get => _settings.DrawFurring; set { if (_settings.DrawFurring != value) { _settings.DrawFurring = value; Changed(); } } }
        public double FurringSpacing { get => _settings.FurringSpacing; set { if (!Near(_settings.FurringSpacing, value)) { _settings.FurringSpacing = value; Changed(); } } }

        public bool DrawHangers { get => _settings.DrawHangers; set { if (_settings.DrawHangers != value) { _settings.DrawHangers = value; Changed(); } } }
        public double HangerMinSpacing { get => _settings.HangerMinSpacing; set { if (!Near(_settings.HangerMinSpacing, value)) { _settings.HangerMinSpacing = value; Changed(); } } }
        public double HangerMaxSpacing { get => _settings.HangerMaxSpacing; set { if (!Near(_settings.HangerMaxSpacing, value)) { _settings.HangerMaxSpacing = value; Changed(); } } }

        public bool DimMain { get => _settings.DimMain; set { if (_settings.DimMain != value) { _settings.DimMain = value; Changed(); } } }
        public bool DimFurring { get => _settings.DimFurring; set { if (_settings.DimFurring != value) { _settings.DimFurring = value; Changed(); } } }
        public bool DimHanger { get => _settings.DimHanger; set { if (_settings.DimHanger != value) { _settings.DimHanger = value; Changed(); } } }

        public Array DimensionPositions => Enum.GetValues(typeof(DimensionPosition));
        public DimensionPosition MainDimPosition { get => _settings.MainDimPosition; set { if (_settings.MainDimPosition != value) { _settings.MainDimPosition = value; Changed(); } } }
        public DimensionPosition FurringDimPosition { get => _settings.FurringDimPosition; set { if (_settings.FurringDimPosition != value) { _settings.FurringDimPosition = value; Changed(); } } }
        public DimensionPosition HangerDimPosition { get => _settings.HangerDimPosition; set { if (_settings.HangerDimPosition != value) { _settings.HangerDimPosition = value; Changed(); } } }

        public double DimensionDistance { get => _settings.DimensionDistance; set { if (!Near(_settings.DimensionDistance, value)) { _settings.DimensionDistance = value; Changed(); } } }
        public double DimensionSpacing { get => _settings.DimensionSpacing; set { if (!Near(_settings.DimensionSpacing, value)) { _settings.DimensionSpacing = value; Changed(); } } }
        public double DirectionDegrees { get => _settings.DirectionDegrees; set { if (!Near(_settings.DirectionDegrees, value)) { _settings.DirectionDegrees = Normalize(value); Changed(); } } }

        public VxtSettings Snapshot() => _settings.Clone();

        public void SetBoundaryStatus(string display, bool hasBoundary)
        {
            BoundaryStatus = display;
            HasBoundary = hasBoundary;
            PreviewStatus = hasBoundary ? "Live Preview đang bật • thay đổi thông số để cập nhật." : "Chọn Polyline kín để bắt đầu Live Preview.";
        }

        public void SetDirection(double degrees)
        {
            _settings.DirectionDegrees = Normalize(degrees);
            MarkCustom();
            OnPropertyChanged(nameof(DirectionDegrees));
            RequestPreview();
        }

        public void SetDimensionPick(DimensionTarget target, DimensionPosition position, double distance)
        {
            switch (target)
            {
                case DimensionTarget.Main: _settings.MainDimPosition = position; OnPropertyChanged(nameof(MainDimPosition)); break;
                case DimensionTarget.Furring: _settings.FurringDimPosition = position; OnPropertyChanged(nameof(FurringDimPosition)); break;
                case DimensionTarget.Hanger: _settings.HangerDimPosition = position; OnPropertyChanged(nameof(HangerDimPosition)); break;
            }
            _settings.DimensionDistance = Math.Max(0, distance);
            MarkCustom();
            OnPropertyChanged(nameof(DimensionDistance));
            RequestPreview();
        }

        public void SetPreviewStats(int main, int furring, int hangers, int dims)
        {
            Summary = $"XC {main}  •  XP {furring}  •  TY {hangers}  •  DIM {dims}";
            PreviewStatus = "✓ Preview cập nhật • chưa ghi đối tượng vào DWG";
        }

        public void SetPreviewError(string message) => PreviewStatus = "⚠ " + message;

        private void Changed([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            MarkCustom();
            RequestPreview();
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
            RequestPreview();
        }

        private void ResetDefaults()
        {
            _applyingPreset = true;
            _settings = new VxtSettings();
            _selectedPreset = "Trần chìm tiêu chuẩn";
            OnPropertyChanged(string.Empty);
            _applyingPreset = false;
            RequestPreview();
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
