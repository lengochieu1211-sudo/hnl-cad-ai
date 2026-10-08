using System;
using System.ComponentModel;
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
        private string _previewStatus = "Chọn biên trần để xem trước Lay-in Ceiling.";
        private string _summary = "Main Tee --  •  Long Cross --  •  Short Cross --  •  Ty --";

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
            "Horizontal",
            "Vertical",
            "Parallel to Short Side",
            "Parallel to Long Side",
            "Auto Optimize"
        };

        public string[] StartOptions { get; } =
        {
            "Balanced",
            "Manual Start",
            "From Door"
        };

        public string[] DimensionOptions { get; } =
        {
            "Off",
            "Module",
            "Grouped",
            "Overall"
        };

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
                RequestPreview();
            }
        }

        public bool IsGroupedDimension => _settings.DimensionMode == LayInDimensionMode.Grouped;

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
            if (!hasBoundary)
                PreviewStatus = "Chọn biên trần để xem trước Lay-in Ceiling.";
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
        }

        public void SetStartMarkerBlock(string blockName)
        {
            _settings.StartMarkerBlockName = blockName ?? string.Empty;
            OnPropertyChanged(nameof(StartMarkerBlockName));
        }

        public void SetPreviewStats(
            int mainStocks,
            int longCross,
            int shortCross,
            int hangers,
            double wasteLength)
        {
            Summary =
                "Main Tee " + mainStocks +
                "  •  Long Cross " + longCross +
                "  •  Short Cross " + shortCross +
                "  •  Ty " + hangers +
                "  •  Waste " + Math.Round(wasteLength, 0) + " mm";
            PreviewStatus = "✓ Lay-in Preview updated • one Hatch will be created per ceiling region";
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
                case LayInMainDirectionMode.Horizontal: return "Horizontal";
                case LayInMainDirectionMode.Vertical: return "Vertical";
                case LayInMainDirectionMode.ParallelShortSide: return "Parallel to Short Side";
                case LayInMainDirectionMode.ParallelLongSide: return "Parallel to Long Side";
                default: return "Auto Optimize";
            }
        }

        private static LayInMainDirectionMode TextToDirection(string value)
        {
            if (value == "Horizontal") return LayInMainDirectionMode.Horizontal;
            if (value == "Vertical") return LayInMainDirectionMode.Vertical;
            if (value == "Parallel to Short Side") return LayInMainDirectionMode.ParallelShortSide;
            if (value == "Parallel to Long Side") return LayInMainDirectionMode.ParallelLongSide;
            return LayInMainDirectionMode.AutoOptimize;
        }

        private static string StartToText(LayInStartMode value)
        {
            switch (value)
            {
                case LayInStartMode.ManualStart: return "Manual Start";
                case LayInStartMode.FromDoor: return "From Door";
                default: return "Balanced";
            }
        }

        private static LayInStartMode TextToStart(string value)
        {
            if (value == "Manual Start") return LayInStartMode.ManualStart;
            if (value == "From Door") return LayInStartMode.FromDoor;
            return LayInStartMode.Balanced;
        }

        private static string DimensionToText(LayInDimensionMode value)
        {
            switch (value)
            {
                case LayInDimensionMode.Off: return "Off";
                case LayInDimensionMode.Module: return "Module";
                case LayInDimensionMode.Overall: return "Overall";
                default: return "Grouped";
            }
        }

        private static LayInDimensionMode TextToDimension(string value)
        {
            if (value == "Off") return LayInDimensionMode.Off;
            if (value == "Module") return LayInDimensionMode.Module;
            if (value == "Overall") return LayInDimensionMode.Overall;
            return LayInDimensionMode.Grouped;
        }
    }
}
