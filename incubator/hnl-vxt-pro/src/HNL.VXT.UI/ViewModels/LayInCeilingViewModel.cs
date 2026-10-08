using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HNL.VXT.Core.Models;
using HNL.VXT.UI.Hosting;
using HNL.VXT.UI.Infrastructure;

namespace HNL.VXT.UI.ViewModels
{
    public sealed class LayInCeilingViewModel : INotifyPropertyChanged
    {
        private readonly IVxtHostBridge _host;
        private LayInCeilingSettings _settings = new LayInCeilingSettings();
        private bool _hasBoundary;
        private string _boundaryStatus = "Chưa chọn biên trần";
        private string _previewStatus = "Chọn vùng trần để xem trước.";
        private string _summary = "T chính --  •  T phụ dài --  •  T phụ ngắn --  •  Ty --";

        public LayInCeilingViewModel(IVxtHostBridge host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            SelectBoundaryCommand = new RelayCommand(() => _host.SelectBoundary());
            PickBoundaryPointCommand = new RelayCommand(() => _host.PickBoundaryPoint());
            PickManualStartCommand = new RelayCommand(
                () => _host.PickLayInManualStart(Snapshot()),
                () => HasBoundary);
            PickDoorCommand = new RelayCommand(
                () => _host.PickLayInDoor(Snapshot()),
                () => HasBoundary);
            PickHangerBlockCommand = new RelayCommand(
                () => _host.PickLayInHangerBlock(Snapshot()));
            PickStartMarkerBlockCommand = new RelayCommand(
                () => _host.PickLayInStartMarkerBlock(Snapshot()));

            RefreshPreviewCommand = new RelayCommand(
                () => _host.RequestLayInPreview(Snapshot()),
                () => HasBoundary);
            ClearPreviewCommand = new RelayCommand(() => _host.ClearLayInPreview());
            CreateCommand = new RelayCommand(
                () => _host.RequestLayInCreate(Snapshot()),
                () => HasBoundary);
            ResetCommand = new RelayCommand(ResetDefaults);
            PickHorizontalDimensionCommand = new RelayCommand(
                () => _host.PickLayInDimensionPosition(true, Snapshot()), () => HasBoundary);
            PickVerticalDimensionCommand = new RelayCommand(
                () => _host.PickLayInDimensionPosition(false, Snapshot()), () => HasBoundary);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string[] GridOptions { get; } =
        {
            "600 × 600",
            "610 × 610",
            "600 × 1200",
            "610 × 1220"
        };

        public string[] DirectionOptions { get; } =
        {
            "Phương ngang",
            "Phương dọc",
            "Song song cạnh ngắn",
            "Song song cạnh dài",
            "Tối ưu vật tư"
        };

        public string[] StartOptions { get; } =
        {
            "Cân đều hai biên",
            "Chọn điểm bắt đầu",
            "Theo vị trí cửa"
        };

        public string[] DimensionOptions { get; } =
        {
            "Tắt",
            "Theo một ô",
            "Gộp nhiều ô",
            "Kích thước tổng",
            "Theo lưới và tấm cắt biên"
        };

        public string[] DimStyleOptions
        {
            get
            {
                var list = new List<string> { "Hiện hành" };
                var names = _host.GetDimStyleNames() ?? Array.Empty<string>();
                list.AddRange(names.Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n));
                return list.ToArray();
            }
        }

        public string SelectedDimensionStyle
        {
            get => string.IsNullOrWhiteSpace(_settings.DimensionStyle)
                ? "Hiện hành" : _settings.DimensionStyle;
            set
            {
                var chosen = string.Equals(value, "Hiện hành", StringComparison.Ordinal)
                    ? string.Empty : (value ?? string.Empty);
                if (string.Equals(_settings.DimensionStyle, chosen, StringComparison.Ordinal)) return;
                _settings.DimensionStyle = chosen;
                Changed();
            }
        }

        public string[] HorizontalPositionOptions { get; } = { "Tự động", "Phía trên", "Phía dưới" };
        public string[] VerticalPositionOptions { get; } = { "Tự động", "Bên trái", "Bên phải" };

        public ICommand SelectBoundaryCommand { get; }
        public ICommand PickBoundaryPointCommand { get; }
        public ICommand PickManualStartCommand { get; }
        public ICommand PickDoorCommand { get; }
        public ICommand PickHangerBlockCommand { get; }
        public ICommand PickStartMarkerBlockCommand { get; }
        public ICommand RefreshPreviewCommand { get; }
        public ICommand ClearPreviewCommand { get; }
        public ICommand CreateCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand PickHorizontalDimensionCommand { get; }
        public ICommand PickVerticalDimensionCommand { get; }

        public bool HasBoundary
        {
            get => _hasBoundary;
            private set
            {
                if (!Set(ref _hasBoundary, value)) return;
                RaiseCommandStates();
            }
        }

        public string BoundaryStatus
        {
            get => _boundaryStatus;
            private set => Set(ref _boundaryStatus, value);
        }

        public string PreviewStatus
        {
            get => _previewStatus;
            private set => Set(ref _previewStatus, value);
        }

        public string Summary
        {
            get => _summary;
            private set => Set(ref _summary, value);
        }

        public string SelectedGrid
        {
            get => GridToText(_settings.GridSystem);
            set
            {
                var next = TextToGrid(value);
                if (_settings.GridSystem == next) return;
                _settings.GridSystem = next;
                Changed();
            }
        }

        public string SelectedDirection
        {
            get => DirectionToText(_settings.MainDirection);
            set
            {
                var next = TextToDirection(value);
                if (_settings.MainDirection == next) return;
                _settings.MainDirection = next;
                Changed();
            }
        }

        public string SelectedStart
        {
            get => StartToText(_settings.StartMode);
            set
            {
                var next = TextToStart(value);
                if (_settings.StartMode == next) return;
                _settings.StartMode = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsManualStart));
                OnPropertyChanged(nameof(IsDoorStart));
                RequestPreview();
            }
        }

        public bool IsManualStart => _settings.StartMode == LayInStartMode.ManualStart;
        public bool IsDoorStart => _settings.StartMode == LayInStartMode.FromDoor;

        public bool DrawHangers
        {
            get => _settings.DrawHangers;
            set
            {
                if (_settings.DrawHangers == value) return;
                _settings.DrawHangers = value;
                OnPropertyChanged();
                RequestPreview();
            }
        }

        public double HangerMaxSpacing
        {
            get => _settings.HangerMaxSpacing;
            set
            {
                if (Near(_settings.HangerMaxSpacing, value)) return;
                _settings.HangerMaxSpacing = Math.Max(0.0, value);
                Changed();
            }
        }

        public double HangerEdgeTarget
        {
            get => _settings.HangerEdgeTarget;
            set
            {
                if (Near(_settings.HangerEdgeTarget, value)) return;
                _settings.HangerEdgeTarget = Math.Max(0.0, value);
                Changed();
            }
        }

        public string HangerBlockName
        {
            get => _settings.HangerBlockName;
            set
            {
                value = value ?? string.Empty;
                if (string.Equals(_settings.HangerBlockName, value, StringComparison.Ordinal)) return;
                _settings.HangerBlockName = value;
                OnPropertyChanged();
                RequestPreview();
            }
        }

        public bool DrawStartTileBlock
        {
            get => _settings.DrawStartTileBlock;
            set
            {
                if (_settings.DrawStartTileBlock == value) return;
                _settings.DrawStartTileBlock = value;
                Changed();
            }
        }

        public string StartMarkerBlockName
        {
            get => _settings.StartMarkerBlockName;
            set
            {
                value = value ?? string.Empty;
                if (string.Equals(_settings.StartMarkerBlockName, value, StringComparison.Ordinal)) return;
                _settings.StartMarkerBlockName = value;
                OnPropertyChanged();
                RequestPreview();
            }
        }

        public string SelectedDimensionMode
        {
            get => DimensionToText(_settings.DimensionMode);
            set
            {
                var next = TextToDimension(value);
                if (_settings.DimensionMode == next) return;
                _settings.DimensionMode = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGroupedDimension));
                OnPropertyChanged(nameof(IsDimensionEnabled));
                RequestPreview();
            }
        }

        public bool IsGroupedDimension => _settings.DimensionMode == LayInDimensionMode.Grouped;
        public bool IsDimensionEnabled => _settings.DimensionMode != LayInDimensionMode.Off;

        public string SelectedHorizontalDimPosition
        {
            get => _settings.HorizontalDimSide == LayInHorizontalDimSide.Top ? "Phía trên" :
                _settings.HorizontalDimSide == LayInHorizontalDimSide.Bottom ? "Phía dưới" : "Tự động";
            set
            {
                var side = value == "Phía trên" ? LayInHorizontalDimSide.Top :
                    value == "Phía dưới" ? LayInHorizontalDimSide.Bottom : LayInHorizontalDimSide.Auto;
                if (_settings.HorizontalDimSide == side) return;
                _settings.HorizontalDimSide = side;
                Changed();
            }
        }

        public string SelectedVerticalDimPosition
        {
            get => _settings.VerticalDimSide == LayInVerticalDimSide.Right ? "Bên phải" :
                _settings.VerticalDimSide == LayInVerticalDimSide.Left ? "Bên trái" : "Tự động";
            set
            {
                var side = value == "Bên phải" ? LayInVerticalDimSide.Right :
                    value == "Bên trái" ? LayInVerticalDimSide.Left : LayInVerticalDimSide.Auto;
                if (_settings.VerticalDimSide == side) return;
                _settings.VerticalDimSide = side;
                Changed();
            }
        }

        public double HorizontalDimDistance
        {
            get => _settings.HorizontalDimDistance;
            set
            {
                var distance = Math.Max(1.0, value);
                if (Near(_settings.HorizontalDimDistance, distance)) return;
                _settings.HorizontalDimDistance = distance;
                Changed();
            }
        }

        public double VerticalDimDistance
        {
            get => _settings.VerticalDimDistance;
            set
            {
                var distance = Math.Max(1.0, value);
                if (Near(_settings.VerticalDimDistance, distance)) return;
                _settings.VerticalDimDistance = distance;
                Changed();
            }
        }

        public void SetPickedDimensionPosition(bool horizontal, bool farSide, double distance)
        {
            if (horizontal)
            {
                _settings.HorizontalDimSide = farSide ? LayInHorizontalDimSide.Top : LayInHorizontalDimSide.Bottom;
                _settings.HorizontalDimDistance = Math.Max(1.0, distance);
                OnPropertyChanged(nameof(SelectedHorizontalDimPosition));
                OnPropertyChanged(nameof(HorizontalDimDistance));
            }
            else
            {
                _settings.VerticalDimSide = farSide ? LayInVerticalDimSide.Right : LayInVerticalDimSide.Left;
                _settings.VerticalDimDistance = Math.Max(1.0, distance);
                OnPropertyChanged(nameof(SelectedVerticalDimPosition));
                OnPropertyChanged(nameof(VerticalDimDistance));
            }
            RequestPreview();
        }

        public int GroupedDimensionCount
        {
            get => _settings.GroupedDimensionCount;
            set
            {
                var next = Math.Max(1, Math.Min(100, value));
                if (_settings.GroupedDimensionCount == next) return;
                _settings.GroupedDimensionCount = next;
                Changed();
            }
        }

        public string HatchLayer
        {
            get => _settings.HatchLayer;
            set => SetText(v => _settings.HatchLayer = v, _settings.HatchLayer, value, nameof(HatchLayer));
        }

        public string HangerLayer
        {
            get => _settings.HangerLayer;
            set => SetText(v => _settings.HangerLayer = v, _settings.HangerLayer, value, nameof(HangerLayer));
        }

        public string DimensionLayer
        {
            get => _settings.DimensionLayer;
            set => SetText(v => _settings.DimensionLayer = v, _settings.DimensionLayer, value, nameof(DimensionLayer));
        }

        public LayInCeilingSettings Snapshot() => _settings.Clone();

        public void SetBoundaryStatus(string display, bool hasBoundary)
        {
            BoundaryStatus = display ?? string.Empty;
            HasBoundary = hasBoundary;
            // DimStyle table belongs to the active DWG, not to the palette.
            OnPropertyChanged(nameof(DimStyleOptions));
            if (!hasBoundary)
                PreviewStatus = "Chọn vùng trần để xem trước.";
        }

        // Manual and door anchors are DWG coordinates, not application-wide presets.
        // Clear them on document transition without injecting a Preview command.
        public void ClearDrawingPoints()
        {
            _settings.ManualStartPoint = null;
            _settings.DoorPoint = null;
            if (_settings.StartMode != LayInStartMode.Balanced)
            {
                _settings.StartMode = LayInStartMode.Balanced;
                OnPropertyChanged(nameof(SelectedStart));
                OnPropertyChanged(nameof(IsManualStart));
                OnPropertyChanged(nameof(IsDoorStart));
            }
        }

        public void SetManualStart(HNL.VXT.Core.Geometry.Point2 point)
        {
            _settings.ManualStartPoint = point;
            _settings.StartMode = LayInStartMode.ManualStart;
            OnPropertyChanged(nameof(SelectedStart));
            OnPropertyChanged(nameof(IsManualStart));
            OnPropertyChanged(nameof(IsDoorStart));
            RequestPreview();
        }

        public void SetDoorPoint(HNL.VXT.Core.Geometry.Point2 point)
        {
            _settings.DoorPoint = point;
            _settings.StartMode = LayInStartMode.FromDoor;
            OnPropertyChanged(nameof(SelectedStart));
            OnPropertyChanged(nameof(IsManualStart));
            OnPropertyChanged(nameof(IsDoorStart));
            RequestPreview();
        }

        public void SetHangerBlock(string blockName)
        {
            _settings.HangerBlockName = blockName ?? string.Empty;
            OnPropertyChanged(nameof(HangerBlockName));
            RequestPreview();
        }

        public void SetStartMarkerBlock(string blockName)
        {
            _settings.StartMarkerBlockName = blockName ?? string.Empty;
            OnPropertyChanged(nameof(StartMarkerBlockName));
            RequestPreview();
        }

        public void SetPreviewStats(
            int mainStocks,
            int longCross,
            int shortCross,
            int hangers,
            double wasteLength)
        {
            Summary =
                "T chính " + mainStocks +
                "  •  T phụ dài " + longCross +
                "  •  T phụ ngắn " + shortCross +
                "  •  Ty " + hangers +
                "  •  Hao hụt " + Math.Round(wasteLength, 0) + " mm";
            PreviewStatus = "✓ Đã cập nhật xem trước • mỗi mảng trần tạo một Hatch.";
        }

        public void SetPreviewError(string message)
        {
            PreviewStatus = "⚠ " + (message ?? string.Empty);
        }

        private void ResetDefaults()
        {
            _settings = new LayInCeilingSettings();
            OnPropertyChanged(string.Empty);
            RequestPreview();
        }

        private void Changed([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            RequestPreview();
        }

        private void RequestPreview()
        {
            if (HasBoundary)
                _host.RequestLayInPreview(Snapshot());
        }

        private void RaiseCommandStates()
        {
            (PickManualStartCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PickDoorCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RefreshPreviewCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CreateCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PickHorizontalDimensionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PickVerticalDimensionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void SetText(Action<string> setter, string current, string value, string propertyName)
        {
            value = value ?? string.Empty;
            if (string.Equals(current, value, StringComparison.Ordinal)) return;
            setter(value);
            OnPropertyChanged(propertyName);
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-9;

        private static string GridToText(LayInGridSystem value)
        {
            switch (value)
            {
                case LayInGridSystem.Module610x610: return "610 × 610";
                case LayInGridSystem.Module600x1200: return "600 × 1200";
                case LayInGridSystem.Module610x1220: return "610 × 1220";
                default: return "600 × 600";
            }
        }

        private static LayInGridSystem TextToGrid(string value)
        {
            if (value == "610 × 610") return LayInGridSystem.Module610x610;
            if (value == "600 × 1200") return LayInGridSystem.Module600x1200;
            if (value == "610 × 1220") return LayInGridSystem.Module610x1220;
            return LayInGridSystem.Module600x600;
        }

        private static string DirectionToText(LayInMainDirectionMode value)
        {
            switch (value)
            {
                case LayInMainDirectionMode.Horizontal: return "Phương ngang";
                case LayInMainDirectionMode.Vertical: return "Phương dọc";
                case LayInMainDirectionMode.ParallelShortSide: return "Song song cạnh ngắn";
                case LayInMainDirectionMode.ParallelLongSide: return "Song song cạnh dài";
                default: return "Tối ưu vật tư";
            }
        }

        private static LayInMainDirectionMode TextToDirection(string value)
        {
            if (value == "Phương ngang") return LayInMainDirectionMode.Horizontal;
            if (value == "Phương dọc") return LayInMainDirectionMode.Vertical;
            if (value == "Song song cạnh ngắn") return LayInMainDirectionMode.ParallelShortSide;
            if (value == "Song song cạnh dài") return LayInMainDirectionMode.ParallelLongSide;
            return LayInMainDirectionMode.AutoOptimize;
        }

        private static string StartToText(LayInStartMode value)
        {
            switch (value)
            {
                case LayInStartMode.ManualStart: return "Chọn điểm bắt đầu";
                case LayInStartMode.FromDoor: return "Theo vị trí cửa";
                default: return "Cân đều hai biên";
            }
        }

        private static LayInStartMode TextToStart(string value)
        {
            if (value == "Chọn điểm bắt đầu") return LayInStartMode.ManualStart;
            if (value == "Theo vị trí cửa") return LayInStartMode.FromDoor;
            return LayInStartMode.Balanced;
        }

        private static string DimensionToText(LayInDimensionMode value)
        {
            switch (value)
            {
                case LayInDimensionMode.Off: return "Tắt";
                case LayInDimensionMode.Module: return "Theo một ô";
                case LayInDimensionMode.Overall: return "Kích thước tổng";
                case LayInDimensionMode.GridAndEdges: return "Theo lưới và tấm cắt biên";
                default: return "Gộp nhiều ô";
            }
        }

        private static LayInDimensionMode TextToDimension(string value)
        {
            if (value == "Tắt") return LayInDimensionMode.Off;
            if (value == "Theo một ô") return LayInDimensionMode.Module;
            if (value == "Kích thước tổng") return LayInDimensionMode.Overall;
            if (value == "Theo lưới và tấm cắt biên") return LayInDimensionMode.GridAndEdges;
            return LayInDimensionMode.Grouped;
        }
    }
}
