using System;
using System.Windows.Input;
using HNL.VXT.UI.Infrastructure;

namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        private string _diagnosticState = "Chưa kiểm tra";
        private string _diagnosticStatus = "Chưa chạy phân tích. Bấm Phân tích nhanh để kiểm tra cấu hình và tài nguyên bản vẽ.";
        private string _diagnosticPackagePath = "Chưa có gói chẩn đoán ZIP.";
        private ICommand _analyzeDiagnosticsCommand;
        private ICommand _exportDiagnosticsUiCommand;
        private ICommand _runtimeGoldenCommand;

        public string DiagnosticState
        {
            get => _diagnosticState;
            private set => Set(ref _diagnosticState, value);
        }

        public string DiagnosticStatus
        {
            get => _diagnosticStatus;
            private set => Set(ref _diagnosticStatus, value);
        }

        public string DiagnosticPackagePath
        {
            get => _diagnosticPackagePath;
            private set => Set(ref _diagnosticPackagePath, value);
        }

        public ICommand AnalyzeDiagnosticsCommand =>
            _analyzeDiagnosticsCommand ?? (_analyzeDiagnosticsCommand = new RelayCommand(RunDiagnosticAnalysis));

        public ICommand ExportDiagnosticsUiCommand =>
            _exportDiagnosticsUiCommand ?? (_exportDiagnosticsUiCommand = new RelayCommand(ExportDiagnosticPackage));

        public ICommand RuntimeGoldenCommand =>
            _runtimeGoldenCommand ?? (_runtimeGoldenCommand = new RelayCommand(RunRuntimeGolden));

        private void RunDiagnosticAnalysis()
        {
            try
            {
                DiagnosticState = "Đang kiểm tra";
                DiagnosticStatus = "HNL Tool đang kiểm tra cấu hình, biên trần và tài nguyên bản vẽ...";

                var result = _host.AnalyzeDiagnostics(_settings.Clone());
                result = string.IsNullOrWhiteSpace(result)
                    ? "Không nhận được kết quả phân tích từ AutoCAD."
                    : result.Trim();

                DiagnosticStatus = NormalizeUserFacingDiagnosticText(result);
                var passed =
                    result.StartsWith("PASS", StringComparison.OrdinalIgnoreCase) ||
                    result.StartsWith("Đạt", StringComparison.OrdinalIgnoreCase);
                DiagnosticState = passed ? "Đạt" : "Cần kiểm tra";
            }
            catch (Exception ex)
            {
                DiagnosticState = "Lỗi";
                DiagnosticStatus = "Không chạy được phân tích: " + ex.Message;
            }
        }

        private void RunRuntimeGolden()
        {
            try
            {
                DiagnosticState = "Đang kiểm tra chuẩn";
                DiagnosticStatus = "HNL Tool đang chạy kiểm tra chuẩn 6000×4000 bằng API AutoCAD. Không lưu đối tượng tạm vào DWG...";
                _host.RequestRuntimeGolden();
            }
            catch (Exception ex)
            {
                DiagnosticState = "Kiểm tra lỗi";
                DiagnosticStatus = "Không khởi động được kiểm tra chuẩn: " + ex.Message;
            }
        }

        public void SetRuntimeGoldenResult(bool passed, string summary, string diagnosticPath)
        {
            DiagnosticState = passed ? "Kiểm tra đạt" : "Kiểm tra lỗi";
            DiagnosticStatus = string.IsNullOrWhiteSpace(summary)
                ? (passed ? "Kiểm tra chuẩn đạt." : "Kiểm tra chuẩn lỗi.")
                : NormalizeUserFacingDiagnosticText(summary);
            if (!string.IsNullOrWhiteSpace(diagnosticPath))
                DiagnosticPackagePath = diagnosticPath;
        }

        private static string NormalizeUserFacingDiagnosticText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;

            var text = value.Trim();
            if (string.Equals(text, "PASS", StringComparison.OrdinalIgnoreCase))
                return "Đạt";
            if (text.StartsWith("PASS ", StringComparison.OrdinalIgnoreCase))
                return "Đạt " + text.Substring(5);
            if (string.Equals(text, "FAIL", StringComparison.OrdinalIgnoreCase))
                return "Lỗi";
            if (text.StartsWith("FAIL ", StringComparison.OrdinalIgnoreCase))
                return "Lỗi " + text.Substring(5);
            return text;
        }

        private void ExportDiagnosticPackage()
        {
            try
            {
                var path = _host.ExportDiagnostics(_settings.Clone());
                if (string.IsNullOrWhiteSpace(path))
                {
                    DiagnosticStatus = "Đã hủy xuất gói chẩn đoán ZIP hoặc không tạo được tệp.";
                    return;
                }

                DiagnosticPackagePath = path;
                DiagnosticStatus = "Đã xuất gói chẩn đoán ZIP. Gửi tệp này khi cần phân tích lỗi chi tiết.";
                if (DiagnosticState == "Chưa kiểm tra") DiagnosticState = "Đã xuất ZIP";
            }
            catch (Exception ex)
            {
                DiagnosticState = "Lỗi";
                DiagnosticStatus = "Không xuất được gói chẩn đoán ZIP: " + ex.Message;
            }
        }
    }
}