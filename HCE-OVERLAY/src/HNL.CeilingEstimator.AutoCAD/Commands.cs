using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HNL.CeilingEstimator.Core;
using HNL.CeilingEstimator.Core.Models;

namespace HNL.CeilingEstimator.AutoCAD
{
    // This is the AutoCAD adapter only. Golden geometry and packing are owned by DemtcEngine.
    // The same immutable-per-command report feeds Preview and Table, without recalculation.
    public sealed class Commands
    {
        // HCE is the only public launcher for Ceiling Estimator Pro.
        // HCECALC stays an internal Palette button route so calculation runs
        // in a valid AutoCAD command context without touching DemtcEngine.
        [CommandMethod("HCE", CommandFlags.Modal)]
        public void Hce() { new HcePaletteCommands().Open(); }

        [CommandMethod("HCECALC", CommandFlags.Modal)]
        public void HceCalculate() { RunCeilingEstimator(); }

        [CommandMethod("HCEQA", CommandFlags.Modal)]
        public void HatchDiagnostic()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            var editor = document.Editor;
            // HCEQA must validate the user-selected 600/610 family, just like HCECALC.
            var diagnosticPitch = HceLegacyProfiles.For(document.Database).Family;
            var selection = editor.GetSelection(
                new PromptSelectionOptions
                {
                    MessageForAdding = "\nHNL Tool - HCEQA: Chọn các Hatch cần kiểm tra: "
                },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "HATCH") }));
            if (selection.Status != PromptStatus.OK || selection.Value == null)
            {
                editor.WriteMessage("\nHNL Tool - HCEQA đã hủy; không thay đổi bản vẽ.");
                return;
            }
            using (var tr = document.Database.TransactionManager.StartTransaction())
            {
                editor.WriteMessage("\nHNL Tool - HCEQA đơn vị bản vẽ=" +
                    document.Database.Insunits.ToString() + "; hệ đang chọn=" +
                    diagnosticPitch + " mm.");
                foreach (SelectedObject selected in selection.Value)
                {
                    if (selected == null || selected.ObjectId.IsNull) continue;
                    try
                    {
                        var hatch = tr.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Hatch;
                        if (hatch == null) continue;
                        if (hatch.IsGradient)
                        {
                            editor.WriteMessage("\nHCEQA handle=" + hatch.Handle +
                                " layer=" + hatch.Layer +
                                " bị loại: Hatch tô chuyển màu không phải lưới tấm trần.");
                            continue;
                        }
                        var userPattern = hatch.PatternType == HatchPatternType.UserDefined;
                        var scaleText = userPattern ? "n/a (user-defined)" : Fmt(hatch.PatternScale);
                        var spacingText = userPattern ? Fmt(hatch.PatternSpace) : "n/a";
                        var doubleText = userPattern ? hatch.PatternDouble.ToString() : "n/a";
                        editor.WriteMessage("\nHCEQA handle=" + hatch.Handle +
                            " layer=" + hatch.Layer +
                            " kiểu hatch=" + hatch.PatternName +
                            " type=" + hatch.PatternType +
                            " tỉ lệ=" + scaleText +
                            " space=" + spacingText +
                            " double=" + doubleText +
                            " origin=" + Fmt(hatch.Origin.X) + "," + Fmt(hatch.Origin.Y) +
                            " patternBase43_44=" + PatternBaseText(hatch) +
                            " angleRad=" + Fmt(hatch.PatternAngle) +
                            " area=" + SafeAreaText(hatch) +
                            " normalZ=" + Fmt(hatch.Normal.Z) +
                            " loops=" + hatch.NumberOfLoops +
                            " definitions=" + hatch.NumberOfPatternDefinitions);
                        for (var i = 0; i < hatch.NumberOfLoops; i++)
                        {
                            var loop = hatch.GetLoopAt(i);
                            var totalEdges = loop.IsPolyline ? loop.Polyline.Count : loop.Curves.Count;
                            editor.WriteMessage("\n  loop=" + i +
                                " kind=" + (loop.IsPolyline ? "polyline" : "curves") +
                                " type=" + loop.LoopType.ToString() +
                                " edges=" + totalEdges);
                        }
                        HatchInput extracted;
                        string failure;
                        if (TryExtractHatch(hatch, out extracted, out failure, diagnosticPitch))
                            editor.WriteMessage("\n  bridge=accepted areaSource=" +
                                (extracted.AreaFromBoundary ? "boundary-fallback" : "native") +
                                " phasePolicy=" + (extracted.PatternBaseVerified
                                    ? "DXF43_44" : "Hatch.Origin-FALLBACK") +
                                " chosenGridOrigin=" + Fmt(extracted.Frame.Origin.X) + "," +
                                Fmt(extracted.Frame.Origin.Y) +
                                " originVs43_44=" + PatternBaseDeltaText(hatch));
                        else
                            editor.WriteMessage("\n  bridge=rejected: " + failure);
                    }
                    catch (System.Exception ex)
                    {
                        editor.WriteMessage("\nHCEQA id=" + selected.ObjectId.ToString() +
                            " lỗi đọc đối tượng: " + ex.Message);
                    }
                }
            }
            editor.WriteMessage("\nHNL Tool - HCEQA hoàn tất; bản vẽ không thay đổi.");
        }

        // Read-only complete current-space audit. Intended for AutoCAD 2023
        // Runtime proof; printed counts must still be compared with real DXF Golden.
        [CommandMethod("HCEGOLDEN", CommandFlags.Modal)]
        public void GoldenAudit()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            var editor = document.Editor;
            var database = document.Database;
            var inputs = new List<HatchInput>();
            var total = 0;
            var rejected = 0;
            editor.WriteMessage("\nHNL Tool - HCEGOLDEN kiểm tra Hatch trong không gian hiện hành (chỉ đọc).");
            editor.WriteMessage("\nHNL Tool - Đơn vị bản vẽ=" + database.Insunits +
                "; HCEGOLDEN cố định hệ 610 mm / Kết hợp, tấm ngắn làm chính." +
                " Không sử dụng lựa chọn S/D trên Palette; HCECALC dùng cấu hình hiện hành.");
            try
            {
                using (var transaction = database.TransactionManager.StartTransaction())
                {
                    var space = (BlockTableRecord)transaction.GetObject(
                        database.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId id in space)
                    {
                        if (id.IsNull) continue;
                        var hatch = transaction.GetObject(id, OpenMode.ForRead, false) as Hatch;
                        if (hatch == null) continue;
                        total++;
                        try
                        {
                            HatchInput input;
                            string reason;
                            if (TryExtractHatch(hatch, out input, out reason))
                            {
                                inputs.Add(input);
                                editor.WriteMessage("\nHCEGOLDEN INPUT handle=" + input.Handle +
                                    " kiểu hatch=" + input.PatternName +
                                    " tỉ lệ=" + Fmt(input.PatternScale) +
                                    " area=" + Fmt(input.Area) +
                                    " segments=" + input.Segments.Count +
                                    " origin=" + Fmt(input.Frame.Origin.X) + "," +
                                    Fmt(input.Frame.Origin.Y) +
                                    " patternBase43_44=" + PatternBaseText(hatch) +
                                    " originVs43_44=" + PatternBaseDeltaText(hatch) +
                                    " areaSource=" + (input.AreaFromBoundary ? "boundary-fallback" : "native") +
                                    " angleRad=" + Fmt(input.Frame.AngleRadians) +
                                    " gridVerified=" + input.UserGridVerified +
                                    " phasePolicy=" + (input.PatternBaseVerified
                                        ? "DXF43_44" : "Hatch.Origin-FALLBACK") +
                                    " nearClosedRecovered=" + input.RecoveredNearClosed +
                                    " joinGapMm=" + Fmt(input.NearClosedGapMm));
                            }
                            else
                            {
                                rejected++;
                                editor.WriteMessage("\nHCEGOLDEN REJECT handle=" +
                                    hatch.Handle + " reason=" + reason);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            rejected++;
                            editor.WriteMessage("\nHCEGOLDEN REJECT handle=" +
                                hatch.Handle + " exception=" + ex.Message);
                        }
                    }
                }

                editor.WriteMessage("\nHCEGOLDEN SCAN total=" + total +
                    " accepted=" + inputs.Count + " rejected=" + rejected);
                if (inputs.Count == 0)
                {
                    editor.WriteMessage("\nHCEGOLDEN STATUS=NO_SUPPORTED_HATCH; không thay đổi DWG.");
                    return;
                }
                var report = CalculateOnce(inputs, editor, ref rejected, true);
                WritePreview(editor, report, rejected);
                editor.WriteMessage("\nHCEGOLDEN STATUS=" +
                    (rejected == 0 && report.Groups.Count > 0 ? "COMPLETE" : "INCOMPLETE") +
                    "; đã tính theo giả định bước lưới 610 mm.");
                editor.WriteMessage("\nHCEGOLDEN chưa được chứng nhận Runtime Golden; cần bằng chứng AutoCAD thực tế.");
                editor.WriteMessage("\nHNL Tool - Đã kiểm tra xong; không thay đổi DWG.");
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\nHCEGOLDEN ADAPTER ERROR=" + ex.Message +
                    "; không thay đổi DWG.");
            }
        }

        private static string Fmt(double value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        private static string SafeAreaText(Hatch hatch)
        {
            try { return Fmt(hatch.Area); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                return "n/a (" + ex.ErrorStatus + ")";
            }
        }

        // Match original DEMTC RC18.5B.4C: for USER double-pattern grids the
        // first pattern-definition line base (DXF 43/44) is the visible phase.
        // Hatch.Origin alone is not equivalent to this saved pattern-line base.
        private static bool TryGetFirstPatternBase(Hatch hatch, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            try
            {
                if (hatch.NumberOfPatternDefinitions < 1) return false;
                object definition = hatch.GetPatternDefinitionAt(0);
                var type = definition.GetType();
                var px = type.GetProperty("BaseX");
                var py = type.GetProperty("BaseY");
                object? vx = px == null ? null : px.GetValue(definition, null);
                object? vy = py == null ? null : py.GetValue(definition, null);
                if (vx == null)
                {
                    var fx = type.GetField("BaseX");
                    vx = fx == null ? null : fx.GetValue(definition);
                }
                if (vy == null)
                {
                    var fy = type.GetField("BaseY");
                    vy = fy == null ? null : fy.GetValue(definition);
                }
                if (vx == null || vy == null) return false;
                x = Convert.ToDouble(vx, CultureInfo.InvariantCulture);
                y = Convert.ToDouble(vy, CultureInfo.InvariantCulture);
                return !(double.IsNaN(x) || double.IsInfinity(x) ||
                         double.IsNaN(y) || double.IsInfinity(y));
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static string PatternBaseText(Hatch hatch)
        {
            double x, y;
            return TryGetFirstPatternBase(hatch, out x, out y)
                ? Fmt(x) + "," + Fmt(y)
                : "n/a";
        }

        private static string PatternBaseDeltaText(Hatch hatch)
        {
            double x, y;
            if (!TryGetFirstPatternBase(hatch, out x, out y)) return "n/a";
            var dx = hatch.Origin.X - x;
            var dy = hatch.Origin.Y - y;
            return Fmt(Math.Sqrt(dx * dx + dy * dy));
        }


        // UI-only progress instrumentation. The native status-bar meter remains
        // visible while the modal AutoCAD command runs; no DoEvents and no
        // additional calculation pass. Cancellation is not simulated.
        private sealed class HceCommandProgress : IDisposable
        {
            private readonly Editor _editor;
            private readonly ProgressMeter _meter = new ProgressMeter();
            private readonly System.Diagnostics.Stopwatch _clock =
                System.Diagnostics.Stopwatch.StartNew();
            private bool _meterActive;
            private bool _complete;
            private int _meterPercent;

            internal HceCommandProgress(Editor editor)
            {
                _editor = editor;
                HcePaletteService.BeginCalculation();
                try
                {
                    _meter.Start("HNL Tool - HCE");
                    _meter.SetLimit(100);
                    _meterActive = true;
                }
                catch (System.Exception)
                {
                    // Presentation failure must not affect Hatch calculation.
                    _meterActive = false;
                }
            }

            internal void Report(int percent, string stage,
                bool indeterminate = false, bool log = false)
            {
                percent = Math.Max(0, Math.Min(100, percent));
                if (_meterActive)
                {
                    try
                    {
                        while (_meterPercent < percent)
                        {
                            _meter.MeterProgress();
                            ++_meterPercent;
                        }
                    }
                    catch (System.Exception) { _meterActive = false; }
                }
                HcePaletteService.ReportCalculation(percent, stage, indeterminate);
                if (log)
                    _editor.WriteMessage("\nHNL Tool - HCE " + stage +
                        " (" + (indeterminate ? "đang xử lý" : percent + "%") +
                        ", " + _clock.Elapsed.TotalSeconds.ToString("0.0",
                            CultureInfo.InvariantCulture) + " s).");
            }

            internal void Complete()
            {
                Report(100, "Hoàn tất tạo bảng CAD.", false, true);
                _complete = true;
            }

            public void Dispose()
            {
                if (_meterActive)
                {
                    try { _meter.Stop(); }
                    catch (System.Exception) { /* keep cleanup fail-open */ }
                }
                _clock.Stop();
                HcePaletteService.EndCalculation(_complete);
            }
        }

        // Displayed text is Vietnamese, stable global keyword aliases remain
        // backwards-compatible; accept both returned global and local names.
        private static bool ChoiceIs(PromptResult result, string globalName, string localName)
        {
            return string.Equals(result.StringResult, globalName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(result.StringResult, localName, StringComparison.OrdinalIgnoreCase);
        }

        private static void RunCeilingEstimator()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            var editor = document.Editor;
            HceLegacyProfile profile;
            bool fromPalette;
            try { profile = HceLegacyProfiles.Consume(document.Database, out fromPalette); }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\nHNL Tool - HCECALC sai ngữ cảnh cấu hình DWG: " +
                    ex.Message + ". Không đọc Hatch hoặc tạo bảng.");
                return;
            }
            var options = profile.ToOptions();
            editor.WriteMessage("\nHNL Tool - HCE Pro v0.3.0: kiểm tra diện tích và gốc lưới (chưa chứng nhận Runtime Golden).");
            editor.WriteMessage("\nHNL Tool - " + profile.Summary());
            editor.WriteMessage("\nHCECALC CONFIG mode=" + profile.Module +
                " priority=" + profile.Priority +
                " tableTextStyle=" + (string.IsNullOrEmpty(profile.TableTextStyle) ? "TABLESTYLE" : profile.TableTextStyle) +
                " tableTextHeightMm=" + Fmt(profile.TableTextHeight) +
                " labelTextHeightMm=" + Fmt(profile.LabelTextHeight) +
                " source=" + (fromPalette ? "PaletteSnapshot" : "DWGProfile") +
                " db=" + document.Database.UnmanagedObject);

            Point2? chosenGridOrigin = null;
            double chosenGridAngle = 0.0;
            if (profile.GridMode == "2")
            {
                var origin = editor.GetPoint("\nHNL Tool - Chọn điểm gốc lưới: ");
                if (origin.Status != PromptStatus.OK) return;
                var next = new PromptPointOptions("\nHNL Tool - Chọn hướng trục X của lưới: ")
                    { UseBasePoint = true, BasePoint = origin.Value };
                var direction = editor.GetPoint(next);
                if (direction.Status != PromptStatus.OK) return;
                var ddx = direction.Value.X - origin.Value.X;
                var ddy = direction.Value.Y - origin.Value.Y;
                if (ddx * ddx + ddy * ddy <= 1e-12)
                {
                    editor.WriteMessage("\nHNL Tool - Hướng lưới quá ngắn; không thay đổi bản vẽ.");
                    return;
                }
                chosenGridOrigin = new Point2(origin.Value.X, origin.Value.Y);
                chosenGridAngle = Math.Atan2(ddy, ddx);
            }

            var selectionOptions = new PromptSelectionOptions
            {
                MessageForAdding = "\nHNL Tool - Chọn một hoặc nhiều Hatch trần: "
            };
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "HATCH") });
            var picked = editor.GetSelection(selectionOptions, filter);
            if (picked.Status != PromptStatus.OK || picked.Value == null)
            {
                editor.WriteMessage("\nHNL Tool - Đã hủy; không thay đổi bản vẽ.");
                return;
            }

            try
            {
                using var progress = new HceCommandProgress(editor);
                progress.Report(2, "Bắt đầu đọc Hatch.", false, true);
                var inputs = new List<HatchInput>();
                var rejected = 0;
                var scannedHatches = 0;
                using (var transaction = document.Database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selected in picked.Value)
                    {
                        ++scannedHatches;
                        progress.Report(2 + (int)(18.0 * scannedHatches /
                            Math.Max(1, picked.Value.Count)),
                            "Đọc Hatch: " + scannedHatches + "/" + picked.Value.Count);
                        if (selected == null || selected.ObjectId.IsNull) continue;
                        try
                        {
                            var hatch = transaction.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Hatch;
                            if (hatch == null) continue;
                            HatchInput input;
                            string reason;
                            if (TryExtractHatch(hatch, out input, out reason, profile.Family))
                            {
                                // Grid-by-Hatch must not silently use the wrong phase.
                                // Modes 1/2 are intentionally independent of Hatch base.
                                if (profile.GridMode == "3" && input.UserGridVerified &&
                                    !input.PatternBaseVerified)
                                {
                                    rejected++;
                                    editor.WriteMessage("\nHNL Tool - Loại Hatch handle=" +
                                        hatch.Handle + ": không đọc được gốc lưới DXF 43/44; " +
                                        "không thể kiểm chứng lưới theo Hatch (dùng HCEQA).");
                                    continue;
                                }
                                if (profile.GridMode == "1")
                                    input.Frame = new GridFrame(new Point2(0, 0), 0);
                                else if (profile.GridMode == "2" && chosenGridOrigin.HasValue)
                                    input.Frame = new GridFrame(chosenGridOrigin.Value, chosenGridAngle);
                                inputs.Add(input);
                                if (profile.GridMode == "3" && input.PatternBaseVerified)
                                    editor.WriteMessage("\nHNL Tool - Hatch " + input.Handle +
                                        " gốc lưới theo DXF 43/44=" +
                                        Fmt(input.Frame.Origin.X) + "," +
                                        Fmt(input.Frame.Origin.Y) + " (đối chiếu với LISP).");
                                if (input.RecoveredNearClosed)
                                    editor.WriteMessage("\nHNL Tool - Hatch handle=" + input.Handle +
                                        " khe hở chưa khép kín=" + Fmt(input.NearClosedGapMm) +
                                        " mm; đã nhận đường bao sau khi kiểm tra topology. " +
                                        "Không sửa Hatch gốc.");
                                if (input.AreaFromBoundary)
                                    editor.WriteMessage("\nHNL Tool - Hatch handle=" + input.Handle +
                                        " native Area=NotApplicable; diện tích đường bao được kiểm chứng=" +
                                        Fmt(input.Area) + " mm2 (đã kiểm tra topology).");
                            }
                            else
                            {
                                rejected++;
                                editor.WriteMessage("\nHNL Tool - Loại Hatch handle=" +
                                    hatch.Handle + " id=" + selected.ObjectId + ": " + reason);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            rejected++;
                            editor.WriteMessage("\nHNL Tool - Loại Hatch id=" +
                                selected.ObjectId + " khi mở/đọc Hatch: " +
                                ex.GetType().Name + " " + ex.Message);
                        }
                    }
                }

                progress.Report(20, "Đã đọc " + inputs.Count + " Hatch.", false, true);
                if (inputs.Count == 0)
                {
                    editor.WriteMessage("\nHNL Tool - Không có đường bao Hatch hợp lệ; bản vẽ giữ nguyên.");
                    return;
                }

                // Reject implicit assumptions. Non-user-defined PAT spacing cannot be
                // proven from PatternScale alone, so require explicit user acceptance.
                editor.WriteMessage("\nHNL Tool - Đơn vị DWG=" +
                    document.Database.Insunits.ToString() +
                    ". Hệ tọa độ kiểm chứng sử dụng milimét.");
                editor.WriteMessage("\nHNL Tool - Kích thước module=" + options.GridWidth + "x" +
                    options.GridHeight + " mm; hệ lưới=" + profile.Family + ".");
                foreach (var input in inputs)
                {
                    if (!input.UserGridVerified)
                        editor.WriteMessage("\nHNL Tool - Hatch " + input.Handle +
                            " kiểu hatch=" + input.PatternName +
                            " tỉ lệ=" + Fmt(input.PatternScale) +
                            " chưa xác minh bước lưới " + profile.Family +
                            "mm; kiểm tra trước khi tiếp tục.");
                }
                var acceptKey = profile.Family == 610 ? "Use610" : "Use600";
                // Do not ask Use600/Use610 again after the user has configured
                // the module in the Palette and USER Hatch pitch was verified.
                // If even one Hatch was rejected, output diagnostics only; never
                // request meaningless confirmation before blocking the Table.
                var needsConfirmation = rejected == 0 &&
                    (document.Database.Insunits != UnitsValue.Millimeters ||
                     inputs.Exists(input => !input.UserGridVerified));
                if (needsConfirmation)
                {
                    var acknowledge = new PromptKeywordOptions(
                        "\nHNL Tool - Chưa xác minh đơn vị DWG hoặc bước lưới. " +
                        "Chọn cách xử lý (Enter = Hủy): ")
                    {
                        AllowNone = true
                    };
                    acknowledge.Keywords.Add("Proceed", "TiepTuc", "Tiếp tục");
                    acknowledge.Keywords.Add("Cancel", "Huy", "Hủy");
                    var consent = editor.GetKeywords(acknowledge);
                    if (consent.Status != PromptStatus.OK ||
                        !ChoiceIs(consent, "Proceed", "TiepTuc"))
                    {
                        editor.WriteMessage("\nHNL Tool - Đã hủy khi lưới chưa xác minh; không thay đổi bản vẽ.");
                        return;
                    }
                }

                progress.Report(20, "Đang phân tích hình học Hatch.", false, true);
                var report = CalculateOnce(inputs, editor, ref rejected, false, options,
                    (done, total, handle) => progress.Report(
                        20 + (int)(55.0 * done / Math.Max(1, total)),
                        "Phân tích Hatch: " + done + "/" + total),
                    (done, total, group) => progress.Report(
                        75 + (int)(13.0 * done / Math.Max(1, total)),
                        "Tối ưu ghép mảnh biên: " + done + "/" + total +
                        " nhóm. Đang xử lý Core...",
                        done < total, done == 0));
                progress.Report(88, "Đã phân tích và ghép xong.", false, true);
                if (report.Groups.Count == 0)
                {
                    editor.WriteMessage("\nHNL Tool - Không có mảng trần tính toán thành công; bản vẽ giữ nguyên.");
                    return;
                }

                // DEMTC RC18.5 single-stock contract: modes S/D must NEVER
                // buy or list the opposite stock family. Validate the actual
                // Core bin types, not just the UI labels. Fail closed rather
                // than silently omitting or relabeling a wrong-stock result.
                if (!options.MixedMode)
                {
                    var expectedCode = profile.Module == "D" ? "D" : "S";
                    foreach (var group in report.Groups)
                    {
                        foreach (var bin in group.Packing.Bins)
                        {
                            if (!string.Equals(bin.Code, expectedCode, StringComparison.Ordinal))
                            {
                                editor.WriteMessage("\nHNL Tool - LỖI NGUỒN TẤM: chế độ " +
                                    (profile.Module == "D" ? "Chỉ tấm dài" : "Chỉ tấm ngắn") +
                                    " nhưng nhóm " + group.Name + " có nguồn " + bin.Code +
                                    ". Không tạo bảng CAD để tránh sai vật tư. " +
                                    "Vui lòng lưu LOGFILEON và gửi kiểm tra.");
                                return;
                            }
                        }
                    }
                }

                // Preview remains available for diagnosing accepted Hatches.
                // Never create a seemingly complete quantity table from a partial selection.
                if (rejected > 0)
                {
                    WritePreview(editor, report, rejected, profile.Module);
                    editor.WriteMessage("\nHNL Tool - KẾT QUẢ CHƯA ĐẦY ĐỦ. " +
                        "Không tạo bảng; hãy kiểm tra các mã Hatch bị loại rồi chạy lại.");
                    return;
                }

                // Preview currently means read-only numeric output. Visual tile geometry
                // is deliberately NOT fabricated: Core currently returns aggregate inner
                // full counts, not per-cell positions. That needs a separate Golden gate.
                WritePreview(editor, report, rejected, profile.Module);

                // HCECALC itself requests a new table. GetPoint is the Cancel gate.
                // Do not insert a redundant Table/Exit keyword stage.
                // Cut listing is configured on the HCE Palette. Reopening a
                // Summary/Detail/NoList prompt after the calculation used to
                // override that selection, especially when the checkbox was off.
                // Respect the saved per-DWG choice; do not ask twice. The
                // separate >2000-detail-rows confirmation stays as a safety gate.
                editor.WriteMessage("\nHNL Tool - Danh sách mảnh cắt trong bảng mới: " +
                    (profile.CutListEnabled
                        ? (profile.CutListMode == "D" ? "Chi tiết" : "Tổng hợp")
                        : "Không liệt kê") + ". Không sửa các bảng CAD đã có.");

                // Like the original LISP, detailed cut lists can be slow on large Hatches.
                // Confirm before allocating thousands of AutoCAD Table rows.
                if (profile.CutListEnabled && profile.CutListMode == "D")
                {
                    var detailedCount = 0;
                    foreach (var material in report.Groups) detailedCount += material.Cuts.Count;
                    if (detailedCount > 2000)
                    {
                        var verifyDetail = new PromptKeywordOptions(
                            "\nHNL Tool - Bảng chi tiết có " + detailedCount +
                            " dòng, có thể mất nhiều thời gian. (Enter = Hủy): ")
                        {
                            AllowNone = true
                        };
                        verifyDetail.Keywords.Add("Proceed", "TiepTuc", "Tiếp tục");
                        verifyDetail.Keywords.Add("Cancel", "Huy", "Hủy");
                        var consent = editor.GetKeywords(verifyDetail);
                        if (consent.Status != PromptStatus.OK ||
                            !ChoiceIs(consent, "Proceed", "TiepTuc"))
                        {
                            editor.WriteMessage("\nHNL Tool - Đã hủy lập bảng chi tiết; bản vẽ giữ nguyên.");
                            return;
                        }
                    }
                }

                progress.Report(90, "Chờ chọn vị trí đặt bảng.", false, true);
                var location = editor.GetPoint("\nHNL Tool - Chọn vị trí đặt bảng khối lượng: ");
                if (location.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\nHNL Tool - Đã hủy đặt bảng; bản vẽ giữ nguyên.");
                    return;
                }
                var labelCount = CountLabelCandidates(report);
                if (labelCount > 2000)
                {
                    var verifyLabels = new PromptKeywordOptions(
                        "\nHNL Tool - Có tạo " + labelCount +
                        " mã N/G/L lên bản vẽ không? (Enter = Có): ") { AllowNone = true };
                    verifyLabels.Keywords.Add("Yes", "Co", "Có");
                    verifyLabels.Keywords.Add("No", "Khong", "Không");
                    var decision = editor.GetKeywords(verifyLabels);
                    if (decision.Status == PromptStatus.Cancel) return;
                    if (decision.Status == PromptStatus.OK &&
                        ChoiceIs(decision, "No", "Khong"))
                    {
                        progress.Report(93, "Đang lập bảng CAD (không tạo mã chữ).", true, true);
                        InsertTable(document.Database, report, location.Value, profile, false,
                            (done, total) => progress.Report(94 + (int)(4.0 * done /
                                Math.Max(1, total)),
                                "Căn bảng: " + done + "/" + total + " hàng."));
                        progress.Complete();
                        editor.WriteMessage("\nHNL Tool - Đã tạo bảng; bỏ qua số lượng lớn mã chữ trên bản vẽ.");
                        return;
                    }
                }
                progress.Report(93, "Đang lập bảng CAD và mã chữ.", true, true);
                InsertTable(document.Database, report, location.Value, profile, true,
                    (done, total) => progress.Report(94 + (int)(4.0 * done /
                        Math.Max(1, total)),
                        "Căn bảng: " + done + "/" + total + " hàng."));
                progress.Complete();
                editor.WriteMessage("\nHNL Tool - Đã tạo bảng từ kết quả tính toán hiện hành.");
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\nHNL Tool - Lỗi kết nối AutoCAD: " + ex.Message);
                // Do not partially create geometry on calculation errors.
            }
        }

        private static bool TryExtractHatch(Hatch hatch, out HatchInput input, out string reason,
            double expectedPitch = 610.0)
        {
            input = new HatchInput();
            reason = string.Empty;
            var stage = "Hatch.IsGradient";
            try
            {
            // Gradients have no line-grid pattern, and some pattern APIs throw
            // eNotApplicable on gradients. Reject before accessing their properties.
            if (hatch.IsGradient)
            {
                reason = "Hatch tô chuyển màu không phải lưới tấm trần";
                return false;
            }
            stage = "Hatch.Normal";
            // HatchLoop points are in OCS; the XY WCS Core contract is valid only
            // for +Z hatches. Other normals need an explicit OCS->WCS transform gate.
            if (Math.Abs(hatch.Normal.X) > 1e-8 ||
                Math.Abs(hatch.Normal.Y) > 1e-8 ||
                hatch.Normal.Z < 1.0 - 1e-8)
            {
                reason = "hệ tọa độ OCS chưa hỗ trợ; cần Hatch WCS có pháp tuyến +Z";
                return false;
            }
            stage = "Hatch.IsSolidFill";
            if (hatch.IsSolidFill)
            {
                reason = "Hatch tô đặc không có lưới tấm trần xác định";
                return false;
            }
            stage = "Hatch.PatternType";
            var userDefined = hatch.PatternType == HatchPatternType.UserDefined;
            stage = userDefined ? "Hatch.PatternSpace / PatternDouble" : "Hatch.PatternScale";
            // Autodesk documents PatternScale for predefined/custom patterns,
            // and PatternSpace/PatternDouble for user-defined patterns only.
            // Do not query a property that does not apply to the pattern kind.
            var patternScale = userDefined ? 1.0 : hatch.PatternScale;
            if (double.IsNaN(patternScale) ||
                double.IsInfinity(patternScale) || !(patternScale > 0))
            {
                reason = "Hatch.PatternScale không hợp lệ";
                return false;
            }
            var userGridVerified = false;
            if (userDefined)
            {
                var doublePattern = hatch.PatternDouble;
                var spacing = hatch.PatternSpace;
                if (!doublePattern || double.IsNaN(spacing) ||
                    Math.Abs(spacing - expectedPitch) > 0.01)
                {
                    reason = "lưới Hatch tự định nghĩa không khớp " + Fmt(expectedPitch) +
                        "x" + Fmt(expectedPitch) + " lưới hai phương (bước=" +
                        Fmt(spacing) + ", double=" + doublePattern + ")";
                    return false;
                }
                userGridVerified = true;
            }
            stage = "Hatch.NumberOfLoops";
            if (hatch.NumberOfLoops != 1)
            {
                reason = "loopCount=" + hatch.NumberOfLoops +
                    "; đường bao nhiều vòng/có lỗ chưa được kiểm chứng tương đương";
                return false;
            }

            stage = "Hatch.GetLoopAt(0)";
            var boundary = hatch.GetLoopAt(0);
            // NotClosed is a DXF flag, not proof that a tiny seam means the
            // entire visible Hatch must be omitted. Validate the actual edges
            // and recover only a single near-zero seam in memory (never edit DWG).
            var flaggedNotClosed = (boundary.LoopType & HatchLoopTypes.NotClosed) != 0;
            if ((boundary.LoopType &
                (HatchLoopTypes.SelfIntersecting | HatchLoopTypes.Duplicate)) != 0)
            {
                reason = "loop=0 đường bao Hatch tự cắt hoặc trùng lặp";
                return false;
            }
            stage = "HatchLoop edges";
            var segments = new List<Segment2>();
            if (boundary.IsPolyline)
            {
                var polyline = boundary.Polyline;
                if (polyline == null || polyline.Count < 3)
                {
                    reason = "loop=0 Polyline có dưới 3 đỉnh";
                    return false;
                }
                // Some Hatch polylines repeat the first vertex as the last.
                // Ignore that duplicate rather than creating a zero-length closing edge.
                var vertexCount = polyline.Count;
                var first = polyline[0].Vertex;
                var last = polyline[vertexCount - 1].Vertex;
                var dxx = first.X - last.X;
                var dyy = first.Y - last.Y;
                if (vertexCount > 3 && dxx * dxx + dyy * dyy <= 1e-12)
                {
                    if (Math.Abs(polyline[vertexCount - 1].Bulge) > 1e-10)
                    {
                        reason = "loop=0 đỉnh cuối trùng nhưng có thông số cung cong";
                        return false;
                    }
                    vertexCount--;
                }
                for (var i = 0; i < vertexCount; i++)
                {
                    var vertex = polyline[i];
                    var next = polyline[(i + 1) % vertexCount];
                    if (Math.Abs(vertex.Bulge) > 1e-10)
                    {
                        reason = "loop=0 edge=" + i + " đoạn cung cong chưa được kiểm chứng tương đương";
                        return false;
                    }
                    if (!AddLine(segments, vertex.Vertex, next.Vertex, out reason))
                    {
                        reason = "loop=0 edge=" + i + ": " + reason;
                        return false;
                    }
                }
            }
            else
            {
                var edge = 0;
                foreach (Curve2d curve in boundary.Curves)
                {
                    var line = curve as LineSegment2d;
                    if (line == null)
                    {
                        reason = "loop=0 edge=" + edge + " type=" +
                            curve.GetType().Name + " chưa được kiểm chứng tương đương với cạnh cong";
                        return false;
                    }
                    if (!AddLine(segments, line.StartPoint, line.EndPoint, out reason))
                    {
                        reason = "loop=0 edge=" + edge + ": " + reason;
                        return false;
                    }
                    edge++;
                }
            }

            if (segments.Count < 3)
            {
                reason = "loop=0 có quá ít cạnh hợp lệ";
                return false;
            }
            var maxGap = 0.0;
            for (var i = 0; i < segments.Count; i++)
            {
                var current = segments[i];
                var next = segments[(i + 1) % segments.Count];
                var dx = current.X2 - next.X1;
                var dy = current.Y2 - next.Y1;
                maxGap = Math.Max(maxGap, Math.Sqrt(dx * dx + dy * dy));
            }
            if (maxGap > 0.01)
            {
                reason = "loop=0 sequential join gap=" +
                    maxGap.ToString("F5", CultureInfo.InvariantCulture) + "mm";
                return false;
            }
            var nearClosedGapMm = 0.0;
            if (flaggedNotClosed)
            {
                // A recorded NotClosed flag may be caused by a tiny seam.
                // Accept only a single terminal gap <=0.01mm, without internal
                // join gaps or crossings. This includes DEMTAM handle 88 (0.002121mm).
                if (segments.Count > 512 || HasNonAdjacentIntersections(segments))
                {
                    reason = "NotClosed: không thể khôi phục cấu trúc đường bao";
                    return false;
                }
                for (var i = 0; i < segments.Count - 1; i++)
                {
                    var a = segments[i];
                    var b = segments[i + 1];
                    var gap = Math.Sqrt((a.X2 - b.X1) * (a.X2 - b.X1) +
                        (a.Y2 - b.Y1) * (a.Y2 - b.Y1));
                    if (gap > 1e-6)
                    {
                        reason = "NotClosed: khe hở giữa các cạnh: " + Fmt(gap) + "mm";
                        return false;
                    }
                }
                var last = segments[segments.Count - 1];
                var first = segments[0];
                nearClosedGapMm = Math.Sqrt((last.X2 - first.X1) *
                    (last.X2 - first.X1) + (last.Y2 - first.Y1) *
                    (last.Y2 - first.Y1));
                if (nearClosedGapMm > 0.01)
                {
                    reason = "NotClosed: khe hở nối đầu-cuối quá lớn: " +
                        Fmt(nearClosedGapMm) + "mm";
                    return false;
                }
                // Close the seam only in the temporary input segment array.
                // The real DWG Hatch is left unchanged.
                segments[segments.Count - 1] = new Segment2(
                    last.X1, last.Y1, first.X1, first.Y1);
            }

            // Compute a numerically stable signed area from the certified
            // linear one-loop boundary before accessing the native Hatch.Area.
            // Some valid non-associative USER Hatches throw eNotApplicable at Area.
            var refX = segments[0].X1;
            var refY = segments[0].Y1;
            var twiceArea = 0.0;
            foreach (var segment in segments)
            {
                twiceArea += (segment.X1 - refX) * (segment.Y2 - refY)
                    - (segment.X2 - refX) * (segment.Y1 - refY);
            }
            var boundaryArea = Math.Abs(twiceArea) * 0.5;
            if (!(boundaryArea > 0) || double.IsNaN(boundaryArea) ||
                double.IsInfinity(boundaryArea))
            {
                reason = "diện tích đa giác khép kín không hợp lệ";
                return false;
            }

            stage = "Hatch.Area";
            var area = boundaryArea;
            var usedBoundaryArea = false;
            try
            {
                area = hatch.Area;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                if (ex.ErrorStatus.ToString() != "NotApplicable") throw;
                // The topology gate is deliberately narrower than the general
                // Core: never infer Area for holes, arcs, intersecting loops or
                // unchecked giant paths. Do not silently under-count material.
                if (segments.Count > 512 || HasNonAdjacentIntersections(segments))
                {
                    reason = "native Hatch.Area=NotApplicable, đường bao chưa " +
                        "được kiểm chứng để thay bằng diện tích hình học";
                    return false;
                }
                usedBoundaryArea = true;
                area = boundaryArea;
            }

            if (!(area > 0) || double.IsNaN(area) || double.IsInfinity(area))
            {
                reason = "Hatch.Area không hợp lệ";
                return false;
            }
            var areaTolerance = Math.Max(0.01, area * 0.00001);
            if (Math.Abs(boundaryArea - area) > areaTolerance)
            {
                reason = "boundary/Hatch.Area mismatch: boundary=" + Fmt(boundaryArea) +
                    " hatch=" + Fmt(area) + " tolerance=" + Fmt(areaTolerance);
                return false;
            }
            stage = "Hatch.Origin / PatternAngle";
            if (double.IsNaN(hatch.Origin.X) || double.IsNaN(hatch.Origin.Y) ||
                double.IsNaN(hatch.PatternAngle) || double.IsInfinity(hatch.PatternAngle))
            {
                reason = "gốc lưới hoặc góc xoay không phải số hữu hạn";
                return false;
            }

            stage = "Hatch.Layer / Color / PatternName";
            input.Handle = hatch.Handle.ToString();
            input.GroupKey = hatch.Layer + " | ACI=" +
                hatch.Color.ColorIndex.ToString(CultureInfo.InvariantCulture) +
                " | " + hatch.PatternName;
            double phaseX = 0.0, phaseY = 0.0;
            var havePatternBase = userGridVerified &&
                TryGetFirstPatternBase(hatch, out phaseX, out phaseY);
            input.PatternBaseVerified = havePatternBase;
            input.Frame = new GridFrame(havePatternBase
                ? new Point2(phaseX, phaseY)
                : new Point2(hatch.Origin.X, hatch.Origin.Y), hatch.PatternAngle);
            input.Segments = segments;
            input.Area = area;
            input.AreaFromBoundary = usedBoundaryArea;
            input.RecoveredNearClosed = flaggedNotClosed;
            input.NearClosedGapMm = nearClosedGapMm;
            input.PatternScale = patternScale;
            input.PatternName = hatch.PatternName;
            input.UserGridVerified = userGridVerified;
            return true;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                reason = "AutoCAD " + ex.ErrorStatus + " at " + stage;
                return false;
            }
            catch (System.Exception ex)
            {
                reason = ex.GetType().Name + " at " + stage + ": " + ex.Message;
                return false;
            }
        }

        // Fail closed on complex/self-crossing boundaries when native
        // Hatch.Area is unavailable. Adjacent edges share a vertex by design.
        private static bool HasNonAdjacentIntersections(List<Segment2> segments)
        {
            const double eps = 1e-8;
            for (var i = 0; i < segments.Count; i++)
            {
                var a = segments[i];
                for (var j = i + 2; j < segments.Count; j++)
                {
                    if (i == 0 && j == segments.Count - 1) continue;
                    var b = segments[j];
                    if (Math.Max(a.X1, a.X2) < Math.Min(b.X1, b.X2) - eps ||
                        Math.Max(b.X1, b.X2) < Math.Min(a.X1, a.X2) - eps ||
                        Math.Max(a.Y1, a.Y2) < Math.Min(b.Y1, b.Y2) - eps ||
                        Math.Max(b.Y1, b.Y2) < Math.Min(a.Y1, a.Y2) - eps)
                        continue;
                    var c1 = (a.X2 - a.X1) * (b.Y1 - a.Y1) -
                             (a.Y2 - a.Y1) * (b.X1 - a.X1);
                    var c2 = (a.X2 - a.X1) * (b.Y2 - a.Y1) -
                             (a.Y2 - a.Y1) * (b.X2 - a.X1);
                    var c3 = (b.X2 - b.X1) * (a.Y1 - b.Y1) -
                             (b.Y2 - b.Y1) * (a.X1 - b.X1);
                    var c4 = (b.X2 - b.X1) * (a.Y2 - b.Y1) -
                             (b.Y2 - b.Y1) * (a.X2 - b.X1);
                    if ((c1 <= eps && c2 >= -eps || c2 <= eps && c1 >= -eps) &&
                        (c3 <= eps && c4 >= -eps || c4 <= eps && c3 >= -eps))
                        return true;
                }
            }
            return false;
        }

        private static bool AddLine(
            List<Segment2> segments, Point2d start, Point2d end, out string reason)
        {
            reason = string.Empty;
            if (double.IsNaN(start.X) || double.IsNaN(start.Y) ||
                double.IsNaN(end.X) || double.IsNaN(end.Y))
            {
                reason = "tọa độ cạnh không phải số hữu hạn";
                return false;
            }
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            if (dx * dx + dy * dy < 1e-12)
            {
                reason = "cạnh suy biến có chiều dài bằng 0";
                return false;
            }
            segments.Add(new Segment2(start.X, start.Y, end.X, end.Y));
            return true;
        }

        private static CalculationReport CalculateOnce(
            List<HatchInput> inputs, Editor editor, ref int rejected,
            bool tracePerHatch = false, DemtcOptions? selectedOptions = null,
            Action<int, int, string>? onHatchProgress = null,
            Action<int, int, string>? onPackingProgress = null)
        {
            var engine = new DemtcEngine();
            // HCEGOLDEN always uses unchanged defaults. UI is a separate snapshot.
            var options = selectedOptions ?? new DemtcOptions();
            var report = new CalculationReport();
            var groups = new Dictionary<string, MaterialGroup>(StringComparer.Ordinal);
            for (var hatchIndex = 0; hatchIndex < inputs.Count; hatchIndex++)
            {
                var input = inputs[hatchIndex];
                onHatchProgress?.Invoke(hatchIndex, inputs.Count, input.Handle);
                HatchCalculationResult calc;
                try
                {
                    calc = engine.CalculateLineHatch(
                        input.Segments, input.Frame, input.Area, options);
                }
                catch (System.Exception ex)
                {
                    rejected++;
                    editor.WriteMessage("\nHNL Tool - Hatch bị loại " + input.Handle +
                        " do lỗi bộ tính: " + ex.Message);
                    continue;
                }
                if (!calc.Success || calc.PureResult == null)
                {
                    rejected++;
                    editor.WriteMessage("\nHNL Tool - Hatch bị loại " + input.Handle +
                        " do lỗi tính toán: " + calc.FailureReason);
                    continue;
                }

                MaterialGroup? group;
                if (!groups.TryGetValue(input.GroupKey, out group) || group == null)
                {
                    group = new MaterialGroup { Name = input.GroupKey };
                    groups.Add(input.GroupKey, group);
                    report.Groups.Add(group);
                }

                if (tracePerHatch)
                {
                    editor.WriteMessage("\nHCEGOLDEN RESULT handle=" + input.Handle +
                        " path=" + calc.Path +
                        " full=" + calc.PureResult.TotalFullCount +
                        " boundary=" + calc.PureResult.BoundaryCandidateCount +
                        " cutPieces=" + calc.PureResult.CutPieces.Count +
                        " slivers=" + calc.PureResult.SliverCount +
                        " areaResidual=" + Fmt(calc.PureResult.AreaResidual) +
                        " group=" + input.GroupKey);
                }
                group.HatchCount++;
                group.Full += calc.PureResult.TotalFullCount;
                group.BoundaryCells += calc.PureResult.BoundaryCandidateCount;
                group.Slivers += calc.PureResult.SliverCount;
                group.Cuts.AddRange(calc.PureResult.CutPieces);
                group.BoundaryFullPieces.AddRange(calc.PureResult.BoundaryFullPieces);
                if (calc.Path == "general-line-loop") group.GeneralFallbackCount++;
            }
            onHatchProgress?.Invoke(inputs.Count, inputs.Count, string.Empty);
            for (var groupIndex = 0; groupIndex < report.Groups.Count; groupIndex++)
            {
                var group = report.Groups[groupIndex];
                onPackingProgress?.Invoke(groupIndex, report.Groups.Count, group.Name);
                // Pack across all Hatches of one material group, not independently per Hatch.
                group.Packing = engine.PackCutPieces(group.Cuts, options);
            }
            onPackingProgress?.Invoke(report.Groups.Count, report.Groups.Count, string.Empty);
            return report;
        }

        // Pure stock modes show only their own cut source; Core stays intact.
        private static void WritePreview(Editor editor, CalculationReport report, int rejected,
            string module = "M")
        {
            editor.WriteMessage("\nHNL Tool - XEM TRƯỚC CHỈ ĐỌC (chỉ thống kê số lượng, không tạo hình học).");
            editor.WriteMessage("\nHNL Tool - Chế độ thống kê: " +
                (module == "S" ? "Chỉ tấm ngắn" : module == "D" ? "Chỉ tấm dài" : "Kết hợp") + ".");
            foreach (var group in report.Groups)
            {
                var sources = module == "S"
                    ? " | Nguồn cắt tấm ngắn=" + BinCount(group, "S")
                    : module == "D"
                        ? " | Nguồn cắt tấm dài=" + BinCount(group, "D")
                        : " | Nguồn ngắn=" + BinCount(group, "S") +
                          " | Nguồn dài=" + BinCount(group, "D");
                editor.WriteMessage("\n  " + group.Name +
                    " | Hatch=" + group.HatchCount +
                    " | Tấm nguyên=" + group.Full +
                    " | Ô biên=" + group.BoundaryCells +
                    " | Mảnh cắt=" + group.Cuts.Count +
                    " | Mảnh vụn=" + group.Slivers +
                    sources +
                    " | Mảnh vượt=" + group.Packing.Oversize.Count +
                    " | Fallback=" + group.GeneralFallbackCount);
            }
            editor.WriteMessage("\nHNL Tool - Số Hatch bị loại=" + rejected +
                ". Xem trước và bảng CAD dùng chung một kết quả tính.");
        }

        private static int BinCount(MaterialGroup group, string code)
        {
            var count = 0;
            foreach (var bin in group.Packing.Bins)
                if (string.Equals(bin.Code, code, StringComparison.Ordinal)) count++;
            return count;
        }

        // CAD Table and N/G/L labels use only the already calculated immutable report.
        // No secondary geometry/packing pass is allowed here.
        private static int CountLabelCandidates(CalculationReport report)
        {
            var count = 0;
            foreach (var group in report.Groups)
            {
                count += group.BoundaryFullPieces.Count;
                foreach (var bin in group.Packing.Bins)
                    count += bin.PiecesReversed.Count;
            }
            return count;
        }

        // LISP-compatible A..Z, AA..AZ... sequence.
        private static string AlphaLabel(int number)
        {
            var label = string.Empty;
            while (number > 0)
            {
                number--;
                label = (char)('A' + number % 26) + label;
                number /= 26;
            }
            return label;
        }

        private sealed class LabelSpec
        {
            public Piece Piece = new Piece();
            public string Code = string.Empty;
            public short ColorIndex;
            public string SourceDisplay = string.Empty;
        }

        private static List<LabelSpec> BuildLabelSpecs(CalculationReport report,
            DemtcOptions options, out int groupedPieces, out int groupedBins, out int lonePieces)
        {
            var labels = new List<LabelSpec>();
            groupedPieces = 0;
            groupedBins = 0;
            lonePieces = 0;
            var nCode = 1;
            var gCode = 1;
            var lCode = 1;
            foreach (var group in report.Groups)
            {
                // Pure interior full cells have no coordinates. Like the LISP fast path,
                // label only the boundary full cells with known real WCS positions.
                foreach (var piece in group.BoundaryFullPieces)
                    labels.Add(new LabelSpec { Piece = piece, Code = "N-" + nCode++,
                        ColorIndex = 3 });
                foreach (var bin in group.Packing.Bins)
                {
                    var sourceName = options.MixedMode && bin.Code == "D"
                        ? options.LargeStock.DisplayName : options.SmallStock.DisplayName;
                    var pieces = new List<Piece>(bin.PiecesInLispOrder());
                    if (pieces.Count > 1)
                    {
                        for (var j = 0; j < pieces.Count; j++)
                            labels.Add(new LabelSpec { Piece = pieces[j],
                                Code = "G" + gCode + "-" + AlphaLabel(j + 1), ColorIndex = 2,
                                SourceDisplay = sourceName });
                        groupedPieces += pieces.Count;
                        groupedBins++;
                        gCode++;
                    }
                    else if (pieces.Count == 1)
                    {
                        labels.Add(new LabelSpec { Piece = pieces[0],
                            Code = "L-" + lCode++, ColorIndex = 1,
                            SourceDisplay = sourceName });
                        lonePieces++;
                    }
                }
            }
            return labels;
        }

        private static void AddCadLabel(BlockTableRecord space, Transaction transaction,
            LabelSpec spec, ObjectId styleId, double labelHeight)
        {
            var p = spec.Piece.LabelPointWcs;
            if (double.IsNaN(p.X) || double.IsNaN(p.Y) ||
                double.IsInfinity(p.X) || double.IsInfinity(p.Y))
                throw new InvalidOperationException("HCE label has invalid WCS coordinates: " + spec.Code);
            var pt = new Point3d(p.X, p.Y, 0);
            var label = new DBText
            {
                TextString = spec.Code,
                Position = pt,
                Height = labelHeight,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = pt,
                ColorIndex = spec.ColorIndex
            };
            if (!styleId.IsNull) label.TextStyleId = styleId;
            space.AppendEntity(label);
            transaction.AddNewlyCreatedDBObject(label, true);
        }

        private sealed class CutSizeRow
        {
            public string Code = string.Empty;
            public int WidthMm;
            public int HeightMm;
            public string Source = string.Empty;
            public int Count = 1;
        }

        private static int RoundCutDimension(double dimension)
        {
            if (dimension < 0 || dimension > 10000000 ||
                double.IsNaN(dimension) || double.IsInfinity(dimension))
                throw new InvalidOperationException("Invalid cut piece dimension");
            return checked((int)Math.Round(dimension, 0, MidpointRounding.AwayFromZero));
        }

        // Exact LISP RC13 summary key: rounded W(mm) + rounded H(mm) + source.
        // Preserve source and orientation; do not merge rotated dimensions.
        private static List<CutSizeRow> BuildCutSizeRows(CalculationReport report,
            List<LabelSpec> labels, bool detailed)
        {
            var details = new List<CutSizeRow>();
            foreach (var label in labels)
            {
                if (label.Code.StartsWith("N-", StringComparison.Ordinal)) continue;
                details.Add(new CutSizeRow
                {
                    Code = label.Code,
                    WidthMm = RoundCutDimension(label.Piece.Width),
                    HeightMm = RoundCutDimension(label.Piece.Height),
                    Source = label.SourceDisplay
                });
            }
            var oversizeIndex = 1;
            foreach (var group in report.Groups)
                foreach (var piece in group.Packing.Oversize)
                    details.Add(new CutSizeRow
                    {
                        Code = "V-" + oversizeIndex++,
                        WidthMm = RoundCutDimension(piece.Width),
                        HeightMm = RoundCutDimension(piece.Height),
                        Source = "Vượt module"
                    });

            var expected = 0;
            foreach (var group in report.Groups) expected += group.Cuts.Count;
            if (details.Count != expected)
                throw new InvalidOperationException("HCE cut-list parity: " +
                    details.Count + " / " + expected + " pieces");
            if (detailed) return details;

            var totals = new Dictionary<string, CutSizeRow>(StringComparer.Ordinal);
            foreach (var row in details)
            {
                var key = row.WidthMm.ToString(CultureInfo.InvariantCulture) + "|" +
                    row.HeightMm.ToString(CultureInfo.InvariantCulture) + "|" + row.Source;
                if (totals.TryGetValue(key, out var item))
                    item.Count++;
                else
                    totals.Add(key, new CutSizeRow
                    {
                        Code = "Mảnh biên", WidthMm = row.WidthMm,
                        HeightMm = row.HeightMm, Source = row.Source, Count = 1
                    });
            }
            var summary = new List<CutSizeRow>(totals.Values);
            summary.Sort((a, b) =>
            {
                var c = a.WidthMm.CompareTo(b.WidthMm);
                if (c != 0) return c;
                c = a.HeightMm.CompareTo(b.HeightMm);
                return c != 0 ? c : string.CompareOrdinal(a.Source, b.Source);
            });
            return summary;
        }

        // Measure native MTEXT glyph extents in the selected DWG Table Style.
        // One transient MText + cached repeated cell values (no new DWG objects).
        private static void FitHceTableToText(Table table, Database database,
            TableStyle cadStyle, int rowCount, int groupHeadingRow,
            bool cutEnabled, int cutHeadingRow, int cutRowsCount,
            Action<int, int>? onMeasuredRow = null)
        {
            var widths = new double[4];
            var heights = new double[rowCount];
            var mergedWidth = 0.0;
            var cache = new Dictionary<string, double[]>(StringComparer.Ordinal);
            using (var probe = new MText())
            {
                probe.SetDatabaseDefaults(database);
                probe.Width = 0.0; // Natural width, no word wrap.
                for (var row = 0; row < rowCount; row++)
                {
                    if (row % 128 == 0) onMeasuredRow?.Invoke(row, rowCount);
                    var merged = row == 0 || row == groupHeadingRow ||
                        (cutEnabled && (row == cutHeadingRow ||
                         (cutRowsCount == 0 && row == cutHeadingRow + 2)));
                    var kind = row == 0 ? RowType.TitleRow :
                        row == 1 ? RowType.HeaderRow : RowType.DataRow;
                    var fallbackHeight = cadStyle.TextHeight(kind);
                    if (!(fallbackHeight > 0) || double.IsNaN(fallbackHeight) ||
                        double.IsInfinity(fallbackHeight)) fallbackHeight = 2.5;
                    var fallbackStyle = cadStyle.TextStyle(kind);
                    for (var col = 0; col < 4; col++)
                    {
                        if (merged && col > 0) continue;
                        var cell = table.Cells[row, col];
                        var value = cell.TextString ?? string.Empty;
                        if (value.Length == 0) continue;
                        double? selectedHeight = cell.TextHeight;
                        var fontHeight = selectedHeight.HasValue &&
                            selectedHeight.Value > 0 &&
                            !double.IsNaN(selectedHeight.Value) &&
                            !double.IsInfinity(selectedHeight.Value)
                            ? selectedHeight.Value : fallbackHeight;
                        ObjectId? selectedFont = cell.TextStyleId;
                        var fontStyle = selectedFont.HasValue && !selectedFont.Value.IsNull
                            ? selectedFont.Value : fallbackStyle;
                        if (fontStyle.IsNull) fontStyle = database.Textstyle;
                        var key = fontStyle.ToString() + "|" +
                            fontHeight.ToString("R", CultureInfo.InvariantCulture) +
                            "|" + value;
                        if (!cache.TryGetValue(key, out var size))
                        {
                            probe.TextHeight = fontHeight;
                            if (!fontStyle.IsNull) probe.TextStyleId = fontStyle;
                            probe.Contents = value;
                            size = new[] { probe.ActualWidth, probe.ActualHeight };
                            if (size[0] < 0 || size[1] < 0 ||
                                double.IsNaN(size[0]) || double.IsInfinity(size[0]) ||
                                double.IsNaN(size[1]) || double.IsInfinity(size[1]))
                                throw new InvalidOperationException(
                                    "HCE MTEXT measurement returned invalid dimensions");
                            cache.Add(key, size);
                        }
                        var horizontalPadding = Math.Max(4.0, fontHeight * 1.25);
                        var verticalPadding = Math.Max(3.0, fontHeight * 0.6);
                        var requiredWidth = size[0] + horizontalPadding * 2.0;
                        if (merged)
                            mergedWidth = Math.Max(mergedWidth, requiredWidth);
                        else
                            widths[col] = Math.Max(widths[col], requiredWidth);
                        heights[row] = Math.Max(heights[row],
                            Math.Max(fontHeight, size[1]) + verticalPadding * 2.0);
                    }
                    heights[row] = Math.Max(heights[row], fallbackHeight * 2.0);
                }
            }
            onMeasuredRow?.Invoke(rowCount, rowCount);
            var allWidth = 0.0;
            for (var col = 0; col < 4; col++)
            {
                widths[col] = Math.Max(16.0, widths[col]);
                allWidth += widths[col];
            }
            // Merged headings need the sum of all four widths, not a huge first column.
            if (mergedWidth > allWidth)
            {
                var extra = (mergedWidth - allWidth) / 4.0;
                for (var col = 0; col < 4; col++) widths[col] += extra;
            }
            for (var col = 0; col < 4; col++)
                table.Columns[col].Width = widths[col];
            for (var row = 0; row < rowCount; row++)
                table.Rows[row].Height = Math.Max(8.0, heights[row]);
        }

        private static void InsertTable(Database database, CalculationReport report, Point3d point,
            HceLegacyProfile profile, bool drawLabels,
            Action<int, int>? onMeasuredRow = null)
        {
            var options = profile.ToOptions();
            var labelHeight = profile.LabelTextHeight;
            if (labelHeight <= 0 || labelHeight > 10000 ||
                double.IsNaN(labelHeight) || double.IsInfinity(labelHeight))
                throw new InvalidOperationException("Invalid HCE label text height");
            var labelSpecs = BuildLabelSpecs(report, options,
                out var groupedPieces, out var groupedBins, out var lonePieces);
            var full = 0;
            var boundary = 0;
            var cuts = 0;
            var hatches = 0;
            var smallBins = 0;
            var longBins = 0;
            var oversize = 0;
            foreach (var group in report.Groups)
            {
                hatches += group.HatchCount;
                full += group.Full;
                boundary += group.BoundaryCells;
                cuts += group.Cuts.Count;
                smallBins += BinCount(group, "S");
                longBins += BinCount(group, "D");
                oversize += group.Packing.Oversize.Count;
            }

            // Build the legacy 4-column Vietnamese quantity breakdown.
            var mixed = options.MixedMode;
            var baseRows = mixed ? 11 : 9;
            var groupHeadingRow = baseRows;
            var cutDetailsEnabled = profile.CutListEnabled;
            var cutListIsDetailed = profile.CutListMode == "D";
            var cutRows = cutDetailsEnabled
                ? BuildCutSizeRows(report, labelSpecs, cutListIsDetailed)
                : new List<CutSizeRow>();
            var cutHeadingRow = groupHeadingRow + 1 + report.Groups.Count;
            var rows = cutHeadingRow + (cutDetailsEnabled ? 2 + Math.Max(1, cutRows.Count) : 0);
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var currentSpace = (BlockTableRecord)transaction.GetObject(
                    database.CurrentSpaceId, OpenMode.ForWrite);
                using (var table = new Table())
                {
                    var cadTableStyleId = database.Tablestyle;
                    if (!string.IsNullOrEmpty(profile.CadTableStyle))
                    {
                        var tableStyles = (DBDictionary)transaction.GetObject(
                            database.TableStyleDictionaryId, OpenMode.ForRead);
                        if (!tableStyles.Contains(profile.CadTableStyle))
                            throw new InvalidOperationException("HCE CAD Table Style is missing from DWG: " +
                                profile.CadTableStyle);
                        cadTableStyleId = tableStyles.GetAt(profile.CadTableStyle);
                    }
                    if (cadTableStyleId.IsNull)
                        throw new InvalidOperationException("HCE DWG has no active CAD Table Style");
                    table.TableStyle = cadTableStyleId;
                    var cadTableStyle = (TableStyle)transaction.GetObject(
                        cadTableStyleId, OpenMode.ForRead);
                    table.SetSize(rows, 4);
                    var textHeight = profile.TableTextHeight;
                    if (textHeight < 0 || textHeight > 10000 ||
                        double.IsNaN(textHeight) || double.IsInfinity(textHeight))
                        throw new InvalidOperationException("Invalid HCE Table text height");
                    // Set native Table row-type overrides as well as individual cell
                    // overrides below; 0 still inherits the drawing's TableStyle.
                    if (textHeight > 0)
                    {
                        table.SetTextHeight(textHeight, (int)RowType.TitleRow);
                        table.SetTextHeight(textHeight, (int)RowType.HeaderRow);
                        table.SetTextHeight(textHeight, (int)RowType.DataRow);
                    }
                    // Initial dimensions are temporary: exact MTEXT fit occurs below.
                    table.SetRowHeight(12);
                    table.SetColumnWidth(55);
                    var styleId = ObjectId.Null;
                    if (!string.IsNullOrEmpty(profile.TableTextStyle))
                    {
                        var styles = (TextStyleTable)transaction.GetObject(
                            database.TextStyleTableId, OpenMode.ForRead);
                        if (!styles.Has(profile.TableTextStyle))
                            throw new InvalidOperationException("HCE Table Text Style is missing from DWG: " +
                                profile.TableTextStyle);
                        styleId = styles[profile.TableTextStyle];
                    }
                    table.Position = point;
                    table.Cells[0, 0].TextString =
                        "BẢNG BÓC TÁCH KHỐI LƯỢNG TẤM TRẦN - HNL TOOL";
                    table.MergeCells(CellRange.Create(table, 0, 0, 0, 3));
                    string[] headers = { "Hạng mục", "Ký hiệu / Loại", "Số lượng", "Ghi chú" };
                    for (var i = 0; i < 4; i++) table.Cells[1, i].TextString = headers[i];

                    table.Cells[2, 0].TextString = "Tấm nguyên";
                    table.Cells[2, 1].TextString = "N-...";
                    table.Cells[2, 2].TextString = full + " tấm";
                    var modeStamp = profile.Module == "M" ? "M/" + profile.Priority : profile.Module;
                    table.Cells[2, 3].TextString = options.GridWidth + " x " +
                        options.GridHeight + " mm (module " + modeStamp + ")";

                    table.Cells[3, 0].TextString = "Ô biên kiểm tra";
                    table.Cells[3, 1].TextString = "Biên";
                    table.Cells[3, 2].TextString = boundary + " ô";
                    table.Cells[3, 3].TextString = "Không phải số mảnh cắt";

                    table.Cells[4, 0].TextString = "Mảnh ghép theo nhóm";
                    table.Cells[4, 1].TextString = "G1-A, G1-B...";
                    table.Cells[4, 2].TextString = groupedPieces + " mảnh";
                    table.Cells[4, 3].TextString = groupedBins + " nguồn";

                    table.Cells[5, 0].TextString = "Mảnh lẻ độc lập";
                    table.Cells[5, 1].TextString = "L-...";
                    table.Cells[5, 2].TextString = lonePieces + " mảnh";
                    table.Cells[5, 3].TextString = "1 mảnh / nguồn";

                    table.Cells[6, 0].TextString = "Mảnh vượt module";
                    table.Cells[6, 1].TextString = "V-...";
                    table.Cells[6, 2].TextString = oversize + " mảnh";
                    table.Cells[6, 3].TextString = oversize > 0 ? "Cần xử lý riêng" : "-";

                    if (mixed)
                    {
                        table.Cells[7, 0].TextString = "Nguồn cắt - tấm nhỏ";
                        table.Cells[7, 1].TextString = options.SmallStock.DisplayName + " mm";
                        table.Cells[7, 2].TextString = smallBins + " tấm";
                        table.Cells[7, 3].TextString = "Từ mảnh cắt";

                        table.Cells[8, 0].TextString = "Nguồn cắt - tấm dài";
                        table.Cells[8, 1].TextString = options.LargeStock.DisplayName + " mm";
                        table.Cells[8, 2].TextString = longBins + " tấm";
                        table.Cells[8, 3].TextString = "Từ mảnh cắt";

                        table.Cells[9, 0].TextString = "TỔNG MUA - TẤM NHỎ";
                        table.Cells[9, 1].TextString = options.SmallStock.DisplayName + " mm";
                        table.Cells[9, 2].TextString =
                            (smallBins + (options.MixedPrimary ==
                                MixedPrimaryMode.SmallMain ? full : 0)) + " tấm";
                        table.Cells[9, 3].TextString = "Tấm nguyên + nguồn cắt";

                        table.Cells[10, 0].TextString = "TỔNG MUA - TẤM DÀI";
                        table.Cells[10, 1].TextString = options.LargeStock.DisplayName + " mm";
                        table.Cells[10, 2].TextString =
                            (longBins + (options.MixedPrimary ==
                                MixedPrimaryMode.LargeMain ? full : 0)) + " tấm";
                        table.Cells[10, 3].TextString = "Tấm nguyên + nguồn cắt";
                    }
                    else
                    {
                        // DEMTC LISP pure-source contract: single-stock mode
                        // reports only its selected stock family. Never sum
                        // the S and D bin columns as in the mixed-stock mode.
                        var pureBins = profile.Module == "D" ? longBins : smallBins;
                        table.Cells[7, 0].TextString = profile.Module == "D"
                            ? "Nguồn cắt - tấm dài" : "Nguồn cắt - tấm ngắn";
                        table.Cells[7, 1].TextString = options.SmallStock.DisplayName + " mm";
                        table.Cells[7, 2].TextString = pureBins + " tấm";
                        table.Cells[7, 3].TextString = "Từ mảnh cắt";

                        table.Cells[8, 0].TextString = profile.Module == "D"
                            ? "TỔNG MUA - TẤM DÀI" : "TỔNG MUA - TẤM NGẮN";
                        table.Cells[8, 1].TextString = options.SmallStock.DisplayName + " mm";
                        table.Cells[8, 2].TextString = (full + pureBins) + " tấm";
                        table.Cells[8, 3].TextString = "Tấm nguyên + nguồn cắt";
                    }

                    table.Cells[groupHeadingRow, 0].TextString = "THEO NHÓM HATCH: " + hatches + " Hatch, " + cuts + " mảnh cắt";
                    table.MergeCells(CellRange.Create(table, groupHeadingRow, 0, groupHeadingRow, 3));
                    for (var i = 0; i < report.Groups.Count; i++)
                    {
                        var group = report.Groups[i];
                        var row = groupHeadingRow + 1 + i;
                        table.Cells[row, 0].TextString = group.Name;
                        table.Cells[row, 1].TextString = group.HatchCount + " Hatch";
                        table.Cells[row, 2].TextString = group.Full + " tấm nguyên";
                        table.Cells[row, 3].TextString = group.Cuts.Count + " mảnh cắt";
                    }

                    if (cutDetailsEnabled)
                    {
                        table.Cells[cutHeadingRow, 0].TextString =
                            "KÍCH THƯỚC MẢNH BIÊN - THEO MODULE";
                        table.MergeCells(CellRange.Create(table, cutHeadingRow, 0, cutHeadingRow, 3));
                        var cutHeaderRow = cutHeadingRow + 1;
                        table.Cells[cutHeaderRow, 0].TextString = cutListIsDetailed ? "Mã" : "Loại";
                        table.Cells[cutHeaderRow, 1].TextString = "Kích thước module (mm)";
                        table.Cells[cutHeaderRow, 2].TextString = "Số lượng";
                        table.Cells[cutHeaderRow, 3].TextString = "Nguồn cắt";
                        if (cutRows.Count == 0)
                        {
                            var emptyRow = cutHeaderRow + 1;
                            table.Cells[emptyRow, 0].TextString = "Không có mảnh biên";
                            table.MergeCells(CellRange.Create(table, emptyRow, 0, emptyRow, 3));
                        }
                        else
                        {
                            for (var i = 0; i < cutRows.Count; i++)
                            {
                                var cut = cutRows[i];
                                var row = cutHeaderRow + 1 + i;
                                table.Cells[row, 0].TextString = cut.Code;
                                table.Cells[row, 1].TextString = cut.WidthMm + " x " + cut.HeightMm;
                                table.Cells[row, 2].TextString = cut.Count.ToString(CultureInfo.InvariantCulture);
                                table.Cells[row, 3].TextString = cut.Source;
                            }
                        }
                    }

                    if (textHeight > 0 || !styleId.IsNull)
                    {
                        for (var row = 0; row < rows; row++)
                            for (var col = 0; col < 4; col++)
                            {
                                // Avoid styling merged child cells (only the first cell owns content).
                                if ((row == 0 || row == groupHeadingRow ||
                                    (cutDetailsEnabled && (row == cutHeadingRow ||
                                    (cutRows.Count == 0 && row == cutHeadingRow + 2)))) && col > 0)
                                    continue;
                                var cell = table.Cells[row, col];
                                if (textHeight > 0) cell.TextHeight = textHeight;
                                if (!styleId.IsNull) cell.TextStyleId = styleId;
                            }
                    }
                    FitHceTableToText(table, database, cadTableStyle, rows,
                        groupHeadingRow, cutDetailsEnabled, cutHeadingRow, cutRows.Count,
                        onMeasuredRow);
                    table.GenerateLayout();
                    currentSpace.AppendEntity(table);
                    transaction.AddNewlyCreatedDBObject(table, true);
                    if (drawLabels)
                        foreach (var spec in labelSpecs)
                            AddCadLabel(currentSpace, transaction, spec, styleId, labelHeight);
                    // Both table and labels commit in one AutoCAD transaction.
                    transaction.Commit();
                }
            }
        }

        private sealed class HatchInput
        {
            public string Handle = string.Empty;
            public string GroupKey = string.Empty;
            public GridFrame Frame = new GridFrame(new Point2(0, 0), 0);
            public List<Segment2> Segments = new List<Segment2>();
            public double Area;
            public bool AreaFromBoundary;
            public bool RecoveredNearClosed;
            public double NearClosedGapMm;
            public double PatternScale;
            public string PatternName = string.Empty;
            public bool UserGridVerified;
            public bool PatternBaseVerified;
        }

        private sealed class CalculationReport
        {
            public List<MaterialGroup> Groups = new List<MaterialGroup>();
        }

        private sealed class MaterialGroup
        {
            public string Name = string.Empty;
            public int HatchCount;
            public int Full;
            public int BoundaryCells;
            public int Slivers;
            public int GeneralFallbackCount;
            public List<Piece> BoundaryFullPieces = new List<Piece>();
            public List<Piece> Cuts = new List<Piece>();
            public PackResult Packing = new PackResult();
        }
    }
}
