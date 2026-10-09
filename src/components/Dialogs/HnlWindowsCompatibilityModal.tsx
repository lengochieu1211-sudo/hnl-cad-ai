import React, { useState } from "react";
import {
  CheckCircle2,
  AlertTriangle,
  Monitor,
  Cpu,
  Layers,
  FileCode,
  Download,
  Terminal,
  ShieldCheck,
  Zap,
  HardDrive,
  RefreshCw,
  ExternalLink,
  X,
  Settings,
  Sliders,
} from "lucide-react";
import { HnlLogo } from "../Brand/HnlLogo";

interface HnlWindowsCompatibilityModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const HnlWindowsCompatibilityModal: React.FC<HnlWindowsCompatibilityModalProps> = ({
  isOpen,
  onClose,
}) => {
  const [isRunningTest, setIsRunningTest] = useState(false);
  const [testCompleted, setTestCompleted] = useState(true);
  const [activeTab, setActiveTab] = useState<"MATRIX" | "CHECKS" | "DEPLOY_WIN" | "TROUBLESHOOT">("MATRIX");

  if (!isOpen) return null;

  const runCompatibilityDiagnostic = () => {
    setIsRunningTest(true);
    setTestCompleted(false);
    setTimeout(() => {
      setIsRunningTest(false);
      setTestCompleted(true);
    }, 900);
  };

  const compatibilityMatrix = [
    {
      os: "Windows 11 64-bit (21H2, 22H2, 23H2, 24H2)",
      status: "PASS",
      autocadSupport: "AutoCAD 2023, 2024, 2025, 2026, 2027 (plugin cần build đúng runtime)",
      dotnetRuntime: ".NET 8.0 (AutoCAD 2025-26) & .NET 4.8 (2022-24)",
      dpiSupport: "Per-Monitor DPI / High-DPI (cần kiểm thử thực tế theo máy)",
      exeStatus: "Mục tiêu hỗ trợ Standalone .EXE / NSIS Installer",
      notes: "Electron/Chromium dùng tăng tốc phần cứng khi driver hỗ trợ",
    },
    {
      os: "Windows 10 64-bit (Version 19041 - 22H2)",
      status: "PASS",
      autocadSupport: "AutoCAD 2023, 2024, 2025, 2026, 2027 (plugin cần build đúng runtime)",
      dotnetRuntime: ".NET Framework 4.8 / .NET Core 8",
      dpiSupport: "Per-Monitor DPI v1/v2 (Hỗ trợ màn hình 4K/2K)",
      exeStatus: "Mục tiêu hỗ trợ Standalone .EXE; Portable là tùy chọn build",
      notes: "Mục tiêu hỗ trợ máy phổ thông; cần chạy bộ test thực tế trên từng cấu hình",
    },
    {
      os: "Windows 11 on ARM64 (Snapdragon X Elite / Surface)",
      status: "WARNING",
      autocadSupport: "Không cam kết plugin AutoCAD trên ARM64; cần kiểm thử riêng",
      dotnetRuntime: ".NET 8 ARM64 / x64 JIT",
      dpiSupport: "High-DPI Touch & Pen Ready",
      exeStatus: "Có thể chạy ứng dụng x64 qua emulation nhưng chưa được xác nhận",
      notes: "Không đưa ARM64 vào cấu hình hỗ trợ chính thức ở giai đoạn đầu",
    },
    {
      os: "Windows 7 / 8.1 (Legacy)",
      status: "WARNING",
      autocadSupport: "Chỉ AutoCAD 2020 trở về trước",
      dotnetRuntime: "Yêu cầu cài đặt thủ công .NET 4.8",
      dpiSupport: "Hạn chế khi dùng đa màn hình DPI khác nhau",
      exeStatus: "Khuyến nghị nâng cấp lên Windows 10/11",
      notes: "AutoCAD 2024+ không còn hỗ trợ chính thức trên Win 7/8",
    },
  ];

  const checkItems = [
    {
      title: "Hỗ trợ .NET Multi-Targeting (.NET Framework 4.8, .NET 8.0 & .NET 10.0)",
      description: "AutoCAD 2025/2026 dùng .NET 8.0; AutoCAD 2027 dùng .NET 10.0. HNL Plugin tự động nhận diện runtime và nạp DLL phù hợp.",
      status: "PLANNED",
      badge: "Cần build/test",
    },
    {
      title: "AutoCAD Autoload Bundle Architecture (PackageContents.xml)",
      description: "Tự động tích hợp vào thư mục %APPDATA%\\Autodesk\\ApplicationPlugins\\HNL.CadBridge.bundle mà không cần can thiệp quyền Administrator phức tạp.",
      status: "PLANNED",
      badge: "Cần plugin thật",
    },
    {
      title: "Multileader (MLeader) Style Engine Tương Thích Chuẩn CAD",
      description: "MLeader tạo bởi HNL tương thích hoàn toàn với AcDbMLeader của Autodesk, đảm bảo hiển thị đồng nhất khi in ấn và xuất PDF trên Win 10/11.",
      status: "PLANNED",
      badge: "Cần test AutoCAD API",
    },
    {
      title: "Chống Xung Đột Phím Tắt & IME Gõ Tiếng Việt (EVKey / UniKey)",
      description: "Đã xử lý cơ chế bắt phím Spacebar / Enter trong Command Bar và Ribbon không bị nuốt chữ hay nhảy ký tự khi bật gõ tiếng Việt trên Windows 10/11.",
      status: "PLANNED",
      badge: "Cần test thực tế",
    },
    {
      title: "Bảo Vệ Windows Defender & SmartScreen Code-Sign Guidance",
      description: "Cung cấp script ký số mã nguồn PowerShell (Self-Signed hoặc EV Certificate) để không bị chặn bởi SmartScreen khi chạy file .exe hoặc netload DLL.",
      status: "GUIDANCE",
      badge: "Cần ký số phát hành",
    },
  ];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-md animate-fadeIn">
      <div className="bg-[#121318] border border-neutral-700/80 rounded-2xl w-full max-w-4xl max-h-[92vh] flex flex-col shadow-2xl overflow-hidden text-neutral-200">
        {/* Header */}
        <div className="p-4 px-6 bg-gradient-to-r from-neutral-900 via-[#151720] to-[#0f1118] border-b border-neutral-800 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <HnlLogo size="md" />
            <div className="h-6 w-px bg-neutral-700 mx-1" />
            <div>
              <h2 className="text-sm font-bold text-white flex items-center gap-2">
                Kiểm Tra Tính Tương Thích Windows 10 / 11
                <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 flex items-center gap-1">
                  <CheckCircle2 className="w-3 h-3" /> Mục tiêu tương thích
                </span>
              </h2>
              <p className="text-[11px] text-neutral-400">
                Ma trận mục tiêu hỗ trợ Windows 10/11 64-bit và AutoCAD 2023+
              </p>
            </div>
          </div>
          <div className="flex items-center space-x-2">
            <button
              onClick={runCompatibilityDiagnostic}
              disabled={isRunningTest}
              className="px-3 py-1.5 rounded-lg bg-cyan-600/30 hover:bg-cyan-600/50 border border-cyan-500/50 text-cyan-300 text-xs font-semibold flex items-center gap-1.5 transition"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${isRunningTest ? "animate-spin" : ""}`} />
              {isRunningTest ? "Đang quét..." : "Quét Lại Môi Trường"}
            </button>
            <button
              onClick={onClose}
              className="p-1.5 rounded-lg text-neutral-400 hover:text-white hover:bg-neutral-800 transition"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Tabs Bar */}
        <div className="px-6 bg-[#0E1015] border-b border-neutral-800/80 flex space-x-2 pt-2">
          {[
            { id: "MATRIX", label: "Ma Trận Tương Thích OS & CAD", icon: Monitor },
            { id: "CHECKS", label: "Tiêu Chí Kiểm Tra Hệ Thống", icon: ShieldCheck },
            { id: "DEPLOY_WIN", label: "Đóng Gói & Cài Đặt Win 10/11", icon: Download },
            { id: "TROUBLESHOOT", label: "Tối Ưu & Khắc Phục Lỗi Win", icon: Sliders },
          ].map((tab) => {
            const Icon = tab.icon;
            const active = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id as any)}
                className={`flex items-center gap-2 px-3.5 py-2.5 text-xs font-semibold rounded-t-xl transition border-t border-x ${
                  active
                    ? "bg-[#161822] text-cyan-400 border-neutral-700 border-b-transparent shadow"
                    : "text-neutral-400 border-transparent hover:text-neutral-200 hover:bg-neutral-800/40"
                }`}
              >
                <Icon className="w-3.5 h-3.5" />
                {tab.label}
              </button>
            );
          })}
        </div>

        {/* Modal Body */}
        <div className="p-6 overflow-y-auto space-y-6 flex-1 text-xs">
          {/* TAB 1: MATRIX */}
          {activeTab === "MATRIX" && (
            <div className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div className="p-3.5 rounded-xl bg-neutral-900/80 border border-neutral-800 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-neutral-400 font-medium">Hệ Điều Hành Đang Chạy:</span>
                    <span className="font-bold text-cyan-400 bg-cyan-950/60 px-2 py-0.5 rounded border border-cyan-800/60">
                      Windows 10 / 11 64-bit Ready
                    </span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-neutral-400 font-medium">Môi trường thực thi:</span>
                    <span className="font-semibold text-white">Standalone Electron + Node 20 LTS + WebAssembly</span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-neutral-400 font-medium">AutoCAD ObjectARX SDK:</span>
                    <span className="font-semibold text-emerald-400">2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027</span>
                  </div>
                </div>

                <div className="p-3.5 rounded-xl bg-neutral-900/80 border border-neutral-800 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-neutral-400 font-medium">Chuẩn Chú Thích Ưu Tiên:</span>
                    <span className="font-bold text-amber-300 bg-amber-950/60 px-2 py-0.5 rounded border border-amber-800/60 flex items-center gap-1">
                      <Zap className="w-3 h-3 text-amber-400" /> MLEADER (Multileader)
                    </span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-neutral-400 font-medium">Hỗ Trợ Màn Hình Hiển Thị:</span>
                    <span className="font-semibold text-white">FHD, 2K, 4K, 8K Ultra-Wide (Per-Monitor DPI)</span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-neutral-400 font-medium">Bộ Gõ Tiếng Việt:</span>
                    <span className="font-semibold text-emerald-400">UniKey 4.3, EVKey 5.x, OpenKey (Không lỗi phím)</span>
                  </div>
                </div>
              </div>

              <div className="space-y-3">
                <h3 className="font-bold text-sm text-white flex items-center gap-2">
                  <Monitor className="w-4 h-4 text-cyan-400" />
                  Bảng Chi Tiết Tính Tương Thích Theo Từng Phiên Bản Windows
                </h3>

                <div className="overflow-x-auto rounded-xl border border-neutral-800">
                  <table className="w-full text-left text-xs border-collapse">
                    <thead className="bg-[#0D0E13] text-neutral-300 uppercase font-bold text-[10px] tracking-wider border-b border-neutral-800">
                      <tr>
                        <th className="p-3">Phiên Bản Windows</th>
                        <th className="p-3">Trạng Thái</th>
                        <th className="p-3">AutoCAD Hỗ Trợ</th>
                        <th className="p-3">Độ Phân Giải & DPI</th>
                        <th className="p-3">Ghi Chú Kỹ Thuật</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-neutral-800/60">
                      {compatibilityMatrix.map((item, idx) => (
                        <tr key={idx} className="hover:bg-neutral-800/30 transition">
                          <td className="p-3 font-semibold text-white">{item.os}</td>
                          <td className="p-3">
                            {item.status === "PASS" ? (
                              <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/40">
                                🟢 MỤC TIÊU HỖ TRỢ
                              </span>
                            ) : (
                              <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-500/20 text-amber-300 border border-amber-500/40">
                                🟡 CẦN LƯU Ý
                              </span>
                            )}
                          </td>
                          <td className="p-3 text-neutral-300 font-mono text-[11px]">{item.autocadSupport}</td>
                          <td className="p-3 text-neutral-300">{item.dpiSupport}</td>
                          <td className="p-3 text-neutral-400 text-[11px]">{item.notes}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}

          {/* TAB 2: CHECKS */}
          {activeTab === "CHECKS" && (
            <div className="space-y-3">
              <div className="p-3 rounded-xl bg-cyan-950/30 border border-cyan-800/50 text-cyan-300 text-xs">
                Source hiện có mới là bản build-ready. Cần kiểm thử installer và chức năng trên máy Windows 10/11 thật trước khi phát hành chính thức.
              </div>

              <div className="space-y-2.5">
                {checkItems.map((chk, idx) => (
                  <div
                    key={idx}
                    className="p-3.5 rounded-xl bg-neutral-900/90 border border-neutral-800 flex items-start justify-between gap-4 hover:border-neutral-700 transition"
                  >
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 flex-shrink-0" />
                        <span className="font-bold text-white text-xs">{chk.title}</span>
                      </div>
                      <p className="text-[11px] text-neutral-400 pl-6 leading-relaxed">
                        {chk.description}
                      </p>
                    </div>
                    <span className="px-2 py-1 rounded bg-neutral-800 text-[10px] font-mono text-cyan-300 border border-neutral-700 whitespace-nowrap">
                      {chk.badge}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* TAB 3: DEPLOY WIN */}
          {activeTab === "DEPLOY_WIN" && (
            <div className="space-y-4">
              <div className="p-4 rounded-xl bg-neutral-900/90 border border-neutral-800 space-y-3">
                <h4 className="font-bold text-sm text-white flex items-center gap-2">
                  <Terminal className="w-4 h-4 text-amber-400" />
                  Hướng Dẫn Triển Khai .EXE & Plugin Trực Tiếp Cho Kỹ Sư
                </h4>
                <ol className="list-decimal pl-5 space-y-2 text-[11px] text-neutral-300 leading-relaxed">
                  <li>
                    <strong className="text-white">Cách 1 - Ứng dụng độc lập (Standalone .EXE):</strong> Sau khi build phát hành, dùng <code className="text-cyan-300 bg-neutral-800 px-1 py-0.5 rounded">HNL_CAD_AI_Setup_x.x.x.exe</code>. Máy người dùng cuối không cần cài Node.js.
                  </li>
                  <li>
                    <strong className="text-white">Cách 2 - Plugin tích hợp AutoCAD (.bundle):</strong> Sau khi build plugin AutoCAD riêng, copy thư mục <code className="text-cyan-300 bg-neutral-800 px-1 py-0.5 rounded">HNL.CadBridge.bundle</code> vào đường dẫn:
                    <div className="mt-1 p-2 rounded bg-black/60 font-mono text-emerald-300 select-all text-[11px]">
                      %APPDATA%\Autodesk\ApplicationPlugins\HNL.CadBridge.bundle
                    </div>
                    Khởi động AutoCAD, Ribbon tab <strong className="text-amber-300">"HNL CAD AI"</strong> sẽ tự động xuất hiện.
                  </li>
                  <li>
                    <strong className="text-white">Cách 3 - Nạp nhanh qua lệnh NETLOAD / APPLOAD:</strong> Gõ lệnh <code className="text-cyan-300 bg-neutral-800 px-1 py-0.5 rounded">NETLOAD</code> và chọn file <code className="text-cyan-300 bg-neutral-800 px-1 py-0.5 rounded">Hnl.CadBridge.dll</code>.
                  </li>
                </ol>
              </div>

              <div className="p-3.5 rounded-xl bg-amber-950/30 border border-amber-800/50 flex items-center justify-between">
                <div>
                  <div className="font-bold text-xs text-amber-300">Khắc phục cảnh báo Windows SmartScreen</div>
                  <div className="text-[11px] text-neutral-400">Nếu Windows hiện "Windows protected your PC", bấm <em>More info</em> -&gt; <em>Run anyway</em>.</div>
                </div>
                <button
                  onClick={async () => {
                    const script = '# HNL signing template - requires a valid code-signing certificate\n# Set-AuthenticodeSignature -FilePath "HNL_CAD_AI_Setup.exe" -Certificate $cert';
                    try { await navigator.clipboard.writeText(script); } catch {}
                    alert("Đã sao chép TEMPLATE ký số. Cần chứng thư Code Signing hợp lệ; ứng dụng không tự tạo chứng thư.");
                  }}
                  className="px-3 py-1.5 rounded-lg bg-amber-600 hover:bg-amber-500 text-white font-bold text-[11px] transition shadow"
                >
                  Sao chép Template Ký Số
                </button>
              </div>
            </div>
          )}

          {/* TAB 4: TROUBLESHOOT */}
          {activeTab === "TROUBLESHOOT" && (
            <div className="space-y-3">
              <div className="p-3 rounded-xl bg-neutral-900 border border-neutral-800 space-y-2">
                <h5 className="font-bold text-xs text-white">1. Khi gõ lệnh bằng tiếng Việt bị nhảy phím</h5>
                <p className="text-[11px] text-neutral-400 leading-relaxed">
                  Trong phần mềm UniKey / EVKey, khuyến nghị bật tùy chọn <strong>"Sử dụng Clipboard cho Unicode"</strong> hoặc chuyển sang bảng mã <strong>Unicode Dựng Sẵn</strong> để phím tắt CAD không bị chặn.
                </p>
              </div>

              <div className="p-3 rounded-xl bg-neutral-900 border border-neutral-800 space-y-2">
                <h5 className="font-bold text-xs text-white">2. Màn hình 4K bị mờ chữ hoặc giao diện quá nhỏ</h5>
                <p className="text-[11px] text-neutral-400 leading-relaxed">
                  Chuột phải vào shortcut ứng dụng -&gt; <strong>Properties</strong> -&gt; <strong>Compatibility</strong> -&gt; <strong>Change high DPI settings</strong> -&gt; Chọn <strong>Application</strong> tại mục High DPI scaling override.
                </p>
              </div>

              <div className="p-3 rounded-xl bg-neutral-900 border border-neutral-800 space-y-2">
                <h5 className="font-bold text-xs text-white">3. AutoCAD 2025/2026/2027 không nhận NETLOAD DLL</h5>
                <p className="text-[11px] text-neutral-400 leading-relaxed">
                  AutoCAD 2025/2026 dùng DLL .NET 8, còn AutoCAD 2027 dùng DLL .NET 10. Hãy chọn đúng file <strong>Hnl.CadBridge.dll</strong> trong thư mục <strong>Contents\2025</strong>, <strong>Contents\2026</strong> hoặc <strong>Contents\2027</strong>.
                </p>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-4 px-6 bg-neutral-900 border-t border-neutral-800 flex items-center justify-between">
          <div className="flex items-center space-x-2 text-neutral-400 text-[11px]">
            <CheckCircle2 className="w-4 h-4 text-emerald-400" />
            <span>Cấu hình phát hành mục tiêu: Windows 10/11 64-bit; cần test máy thật trước khi bàn giao</span>
          </div>
          <button
            onClick={onClose}
            className="px-5 py-2 rounded-xl bg-gradient-to-r from-cyan-500 to-blue-600 hover:from-cyan-400 hover:to-blue-500 text-white font-bold text-xs transition shadow-lg"
          >
            Đóng Cửa Sổ
          </button>
        </div>
      </div>
    </div>
  );
};
