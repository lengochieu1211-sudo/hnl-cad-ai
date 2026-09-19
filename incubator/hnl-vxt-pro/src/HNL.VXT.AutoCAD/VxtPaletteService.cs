using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Threading;
using Autodesk.AutoCAD.Windows;
using HNL.VXT.UI.Infrastructure;
using HNL.VXT.UI.Views;

namespace HNL.VXT.AutoCAD
{
    internal static class VxtPaletteService
    {
        // Beta.1 gets a refreshed Palette GUID so AutoCAD does not restore stale alpha titles/layout.
        private static readonly Guid PaletteGuid = new Guid("8F41C84C-6E27-4E9A-9F1E-1FDC49B1A706");
        private static PaletteSet _palette;
        private static VxtPaletteView _view;
        private static bool _uiPolishScheduled;
        private static bool _uiPolishCompleted;

        public static void Show()
        {
            TraceUiStartup("HVX Show begin");

            if (_palette == null)
            {
                var bridge = new VxtHostBridge();
                _view = new VxtPaletteView(bridge);
                VxtSession.Current.ViewModel = _view.ViewModel;

                // Keep first-open work deliberately light. AutoCAD hosts PaletteSet on its main
                // UI thread; running every cosmetic tree pass here can make the command appear
                // frozen. Create + show the palette first, then apply presentation stages at
                // ApplicationIdle one stage at a time.
                _palette = new PaletteSet(VxtBuildInfo.PaletteTitle, PaletteGuid)
                {
                    Style = PaletteSetStyles.ShowAutoHideButton |
                            PaletteSetStyles.ShowCloseButton |
                            PaletteSetStyles.ShowPropertiesMenu,
                    DockEnabled = DockSides.Left | DockSides.Right,
                    MinimumSize = new Size(360, 520),
                    Size = new Size(430, 740),
                    KeepFocus = false
                };

                _palette.AddVisual("Vẽ Xương Trần", _view);
                _palette.Visible = true;

                TraceUiStartup("Palette visible; scheduling UI polish");
                ScheduleUiPolish(bridge);
                return;
            }

            _palette.Visible = true;

            // A previous stage exception is fail-open; reopening the palette must never enqueue
            // another copy of the same startup pipeline.
            if (!_uiPolishScheduled && !_uiPolishCompleted && _view != null)
                ScheduleUiPolish(new VxtHostBridge());

            TraceUiStartup("HVX Show end");
        }

        private static void ScheduleUiPolish(VxtHostBridge bridge)
        {
            if (_view == null || _uiPolishScheduled || _uiPolishCompleted) return;
            _uiPolishScheduled = true;

            var stages = new Queue<KeyValuePair<string, Action>>();
            stages.Enqueue(new KeyValuePair<string, Action>(
                "LocalMainUi",
                () => VxtPaletteLocalMainUi.Apply(_view)));
            stages.Enqueue(new KeyValuePair<string, Action>(
                "CompactTuner",
                () => VxtPaletteCompactTuner.Apply(_view, bridge, _view.ViewModel)));
            stages.Enqueue(new KeyValuePair<string, Action>(
                "RuntimePolish",
                () => VxtPaletteRuntimePolish.Apply(_view)));
            stages.Enqueue(new KeyValuePair<string, Action>(
                "OptimizerUi",
                () => VxtPaletteOptimizerUi.Apply(_view)));
            stages.Enqueue(new KeyValuePair<string, Action>(
                "LayerDimComboTheme",
                () => VxtPaletteView.ApplyLayerDimComboThemeFix(_view)));
            stages.Enqueue(new KeyValuePair<string, Action>(
                "SectionOrder",
                () => VxtPaletteSectionOrder.Apply(_view)));
            stages.Enqueue(new KeyValuePair<string, Action>(
                "ToggleCompact",
                () => VxtPaletteToggleCompact.Apply(_view)));
            stages.Enqueue(new KeyValuePair<string, Action>(
                "PreviewLegend",
                () => VxtPalettePreviewLegend.Apply(_view)));
            stages.Enqueue(new KeyValuePair<string, Action>(
                "Typography",
                () => VxtPaletteTypography.Apply(_view)));
            stages.Enqueue(new KeyValuePair<string, Action>(
                "VisualIdentity",
                () => VxtPaletteVisualIdentity.Apply(_view)));
            stages.Enqueue(new KeyValuePair<string, Action>(
                "PropertiesLayout",
                () => VxtPalettePropertiesLayout.Apply(_view)));

            RunNextUiStage(stages);
        }

        private static void RunNextUiStage(Queue<KeyValuePair<string, Action>> stages)
        {
            if (_view == null)
            {
                _uiPolishScheduled = false;
                return;
            }

            if (stages == null || stages.Count == 0)
            {
                _uiPolishScheduled = false;
                _uiPolishCompleted = true;
                TraceUiStartup("UI polish complete");
                return;
            }

            var stage = stages.Dequeue();
            _view.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    var timer = Stopwatch.StartNew();
                    TraceUiStartup("START " + stage.Key);
                    try
                    {
                        stage.Value();
                        timer.Stop();
                        TraceUiStartup("PASS " + stage.Key + " " + timer.ElapsedMilliseconds + "ms");
                    }
                    catch (Exception ex)
                    {
                        timer.Stop();
                        // Cosmetic startup is fail-open. Log the exact stage and continue so a
                        // theme/layout defect cannot make HVX unusable or block AutoCAD.
                        TraceUiStartup(
                            "FAIL " + stage.Key + " " + timer.ElapsedMilliseconds + "ms | " +
                            ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        RunNextUiStage(stages);
                    }
                }),
                DispatcherPriority.ApplicationIdle);
        }

        private static void TraceUiStartup(string message)
        {
            try
            {
                var root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HNL Tool",
                    "VXT Pro");
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, "palette-startup.log");
                File.AppendAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + message + Environment.NewLine);
            }
            catch
            {
                // Startup tracing must never affect CAD operation.
            }
        }
    }
}
