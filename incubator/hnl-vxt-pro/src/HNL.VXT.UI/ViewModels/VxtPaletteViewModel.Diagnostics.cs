using System;
using System.Windows.Input;
using HNL.VXT.UI.Infrastructure;

namespace HNL.VXT.UI.ViewModels
{
    public sealed partial class VxtPaletteViewModel
    {
        private string _diagnosticState = "CHƯA KIỂM TRA";
        private string _diagnosticStatus = "Chưa chạy phân tích. Bấm Phân tích nhanh để kiểm tra cấu hình và tài nguyên CAD.";
        private string _diagnosticPackagePath = "Chưa có gói Diagnostic ZIP.";
        private ICommand _analyzeDiagnosticsCommand;
        private ICommand _exportDiagnosticsUiCommand;

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

        private void RunDiagnosticAnalysis()
        {
            try
            {
                DiagnosticState = "ĐANG KIỂM TRA";
                DiagnosticStatus = "HNL Tool đang kiểm tra cấu hình, biên trần và tài nguyên bản vẽ...";

                var result = _host.AnalyzeDiagnostics(_settings.Clone());
                result = string.IsNullOrWhiteSpace(result)
                    ? "Không nhận được kết quả phân tích từ AutoCAD."
                    : result.Trim();

                DiagnosticStatus = result;
                DiagnosticState = result.StartsWith("PASS", StringComparison.OrdinalIgnoreCase)
                    ? "PASS"
                    : "CẦN KIỂM TRA";
            }
            catch (Exception ex)
            {
                DiagnosticState = "LỖI";
                DiagnosticStatus = "Không chạy được phân tích: " + ex.Message;
            }
        }

        private void ExportDiagnosticPackage()
        {
            try
            {
                var path = _host.ExportDiagnostics(_settings.Clone());
                if (string.IsNullOrWhiteSpace(path))
                {
                    DiagnosticStatus = "Đã hủy xuất Diagnostic ZIP hoặc không tạo được file.";
                    return;
                }

                DiagnosticPackagePath = path;
                DiagnosticStatus = "Đã xuất Diagnostic ZIP. Gửi file này khi cần phân tích lỗi sâu.";
                if (DiagnosticState == "CHƯA KIỂM TRA") DiagnosticState = "ĐÃ XUẤT ZIP";
            }
            catch (Exception ex)
            {
                DiagnosticState = "LỖI";
                DiagnosticStatus = "Không xuất được Diagnostic ZIP: " + ex.Message;
            }
        }
    }
}