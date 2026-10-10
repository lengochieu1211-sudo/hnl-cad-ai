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
        // Refresh the Palette GUID when the product caption changes so AutoCAD cannot restore the old VXT title/layout.
        private static readonly Guid PaletteGuid = new Guid("B7E5D9C2-3A1F-4D08-9AC8-1DAB6B3F7821");
        private static PaletteSet _palette;
        private static VxtPaletteView _view;
        private static VxtHostBridge _bridge;
        private static bool _active;
        private static int _uiPolishGeneration;
        private static bool _uiPolishScheduled;
        private static bool _uiPolishCompleted;

        public static void Show()
        {
            // Explicit user invocation is the first point where runtime document hooks are enabled.
            // AutoCAD startup remains completely free of VXT Session/Transient/WPF work.
            PluginEntry.EnableRuntimeHooks();
            _active = true;
            _bridge?.ResumePreviews();
            TraceUiStartup("HCF Show begin");

            if (_palette == null)
            {
                _bridge = new VxtHostBridge();
                _view = new VxtPaletteView(_bridge);
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
                _palette.Name = VxtBuildInfo.PaletteTitle;
                TraceUiStartup("Palette caption: " + _palette.Name);

                TraceUiStartup("Palette visible; scheduling UI polish");
                ScheduleUiPolish(_bridge);
                return;
            }

            _palette.Visible = true;
            _palette.Name = VxtBuildInfo.PaletteTitle;
            TraceUiStartup("Palette caption: " + _palette.Name);

            // A previous stage exception is fail-open; reopening the palette must never enqueue
            // another copy of the same startup pipeline.
            if (!_uiPolishScheduled && !_uiPolishCompleted && _view != null)
                ScheduleUiPolish(_bridge);

            TraceUiStartup("HCF Show end");
        }

        internal static bool IsActive => _active;

        internal static void StopInCommandContext()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            _bridge?.PausePreviews();

            // Native erase is safe only inside AutoCAD's modal command context.
            // If any graphic remains attached, leave HCF running and never force-dispose.
            VxtTransientPreview.Instance.Clear();
            LayInCadRuntime.ClearPreview();
            if (VxtTransientPreview.Instance.TrackedDrawableCount > 0 ||
                LayInCadRuntime.ActiveDrawableCount > 0)
            {
                _bridge?.ResumePreviews();
                doc.Editor.WriteMessage("\nHNL Tool - Không thể tắt HCF: Preview cũ chưa xóa an toàn.");
                return;
            }

            _active = false;
            ++_uiPolishGeneration;
            _uiPolishScheduled = false;
            if (_palette != null)
                _palette.Visible = false;
            PluginEntry.DisableRuntimeHooks();
            VxtSession.ReleaseDocument(null);
            doc.Editor.WriteMessage("\nHNL Tool - Đã tắt HCF; AutoCAD vẫn chạy. Gõ HCF để mở lại.");
        }

        private static void ScheduleUiPolish(VxtHostBridge bridge)
        {
            if (!_active || _view == null || _uiPolishScheduled || _uiPolishCompleted) return;
            _uiPolishScheduled = true;
            var generation = ++_uiPolishGeneration;

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

            RunNextUiStage(stages, generation);
        }

        private static void RunNextUiStage(Queue<KeyValuePair<string, Action>> stages, int generation)
        {
            if (!_active || generation != _uiPolishGeneration) return;
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
                    if (!_active || generation != _uiPolishGeneration) return;
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
                        RunNextUiStage(stages, generation);
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
                    "Ceiling Framing Pro");
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
