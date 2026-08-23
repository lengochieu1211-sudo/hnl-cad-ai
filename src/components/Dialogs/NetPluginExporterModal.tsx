import React, { useState } from "react";
import { Download, Copy, Check, FileCode, Package, Layers, Terminal, X, ExternalLink } from "lucide-react";
import { HNL_APP_VERSION } from "../../lib/version";

interface NetPluginExporterModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const NetPluginExporterModal: React.FC<NetPluginExporterModalProps> = ({
  isOpen,
  onClose,
}) => {
  const [selectedFile, setSelectedFile] = useState<string>("PackageContents.xml");
  const [copied, setCopied] = useState(false);

  if (!isOpen) return null;

  const pluginFiles: Record<string, { desc: string; code: string; language: string }> = {
    "PackageContents.xml": {
      desc: "Autodesk Autoloader manifest đặt tại %APPDATA%\\Autodesk\\ApplicationPlugins\\HnlCadTool.bundle",
      language: "xml",
      code: `<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage 
  SchemaVersion="1.0" 
  AutodeskProduct="AutoCAD" 
  ProductType="Application" 
  Name="HnlCadTool" 
  AppVersion="${HNL_APP_VERSION}" 
  Description="HNL CAD AI Tool for AutoCAD 2023+" 
  Author="HNL Architecture &amp; Construction" 
  ProductCode="{C8D3A56F-9E4B-4E92-9C8F-7B32D15A2026}" 
  UpgradeCode="{F2B1E4A3-6D5C-4F81-8B9A-0D12E34F5678}">
  <CompanyDetails Name="HNL Architecture &amp; CAD AI Solutions" Url="https://hnlcad.vn" />
  <Components Description="AutoCAD 2023+ .NET Module">
    <RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R23.0" SeriesMax="R25.1" />
    <ComponentEntry 
      AppName="HnlCadTool" 
      Version="${HNL_APP_VERSION}" 
      ModuleName="./Contents/Windows/HnlCadTool.dll" 
      AppDescription="HNL CAD AI Copilot &amp; Smart Shopdrawing Tools" 
      LoadOnAutoCADStartup="True">
      <Commands GroupName="HNL">
        <Command Local="HNL_AI" Global="HNL_AI" />
        <Command Local="HNL_MLEADER" Global="HNL_MLEADER" />
        <Command Local="HNL_WALL" Global="HNL_WALL" />
        <Command Local="HNL_CEILING" Global="HNL_CEILING" />
        <Command Local="HNL_AREA" Global="HNL_AREA" />
        <Command Local="HNL_TABLE" Global="HNL_TABLE" />
        <Command Local="HNL_TRANSLATE" Global="HNL_TRANSLATE" />
        <Command Local="HNL_AUDIT" Global="HNL_AUDIT" />
        <Command Local="HNL_LISP" Global="HNL_LISP" />
      </Commands>
    </ComponentEntry>
  </Components>
</ApplicationPackage>`,
    },

    "HnlCadPlugin.cs": {
      desc: "C# .NET Plugin entry point với Ribbon, WebView2 PaletteSet và AutoCAD Command Methods",
      language: "csharp",
      code: `using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Ribbon;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Microsoft.Web.WebView2.WinForms;

[assembly: ExtensionApplication(typeof(Hnl.Cad.Plugin.HnlExtension))]
[assembly: CommandClass(typeof(Hnl.Cad.Plugin.HnlCommands))]

namespace Hnl.Cad.Plugin
{
    public class HnlExtension : IExtensionApplication
    {
        private static PaletteSet _paletteSet;
        private static WebView2 _webView;

        public void Initialize()
        {
            Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\\n[HNL CAD AI TOOL] Loaded successfully.\\n");
            
            // Build AutoCAD Ribbon "HNL CAD TOOL"
            CreateRibbonTab();
        }

        public void Terminate()
        {
            if (_paletteSet != null)
            {
                _paletteSet.Dispose();
                _paletteSet = null;
            }
        }

        public static void ShowPalette()
        {
            if (_paletteSet == null)
            {
                _paletteSet = new PaletteSet("HNL CAD AI ASSISTANT", new Guid("9D32F4C1-4B7E-4F21-9310-8C5A20261122"));
                _paletteSet.Style = PaletteSetStyles.ShowAutoHideButton | 
                                    PaletteSetStyles.ShowCloseButton | 
                                    PaletteSetStyles.ShowPropertiesMenu;
                _paletteSet.Dock = DockSides.Right;
                _paletteSet.MinimumSize = new System.Drawing.Size(380, 500);

                _webView = new WebView2 { Dock = DockStyle.Fill };
                _webView.EnsureCoreWebView2Async().ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        // Connect to local or bundled Web App
                        string localHtml = Path.Combine(Path.GetDirectoryName(typeof(HnlExtension).Assembly.Location), "wwwroot", "index.html");
                        if (File.Exists(localHtml))
                            _webView.CoreWebView2.Navigate("file:///" + localHtml.Replace('\\\\', '/'));
                        else
                            _webView.CoreWebView2.Navigate("http://localhost:3000");

                        // Add AutoCAD .NET IPC Bridge
                        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                    }
                });

                _paletteSet.Add("HNL COPILOT", _webView);
            }
            _paletteSet.Visible = true;
        }

        private static void OnWebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            string json = e.TryGetWebMessageAsString();
            // Dispatch command safely to AutoCAD Active Document
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            doc.SendStringToExecute(json + "\\n", true, false, false);
        }

        private void CreateRibbonTab()
        {
            // Implementation of AutoCAD Ribbon Tab "HNL CAD TOOL"
            RibbonControl ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            RibbonTab tab = new RibbonTab { Title = "HNL CAD TOOL", Id = "HNL_CAD_TOOL_TAB" };
            ribbon.Tabs.Add(tab);

            // Group 1: Vẽ nhanh & MLeader
            RibbonPanelSource pSource1 = new RibbonPanelSource { Title = "Vẽ nhanh & Chú thích" };
            RibbonButton btnMLeader = new RibbonButton { Text = "MLeader Chú Thích", CommandParameter = "HNL_MLEADER " };
            RibbonButton btnWall100 = new RibbonButton { Text = "Tường 100", CommandParameter = "HNL_WALL100 " };
            pSource1.Items.Add(btnMLeader);
            pSource1.Items.Add(btnWall100);
            tab.Panels.Add(new RibbonPanel { Source = pSource1 });
        }
    }

    public class HnlCommands
    {
        [CommandMethod("HNL_AI", CommandFlags.Modal)]
        public void OpenAiPalette()
        {
            HnlExtension.ShowPalette();
        }

        [CommandMethod("HNL_MLEADER", CommandFlags.Modal)]
        public void DrawMLeader()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            ed.WriteMessage("\\n[HNL CAD] Kích hoạt vẽ Multileader chuẩn Shopdrawing...");
        }
    }
}`,
    },

    "HnlCADTool_Setup.iss": {
      desc: "Inno Setup script cho plugin AutoCAD 2023+ trên Windows 10/11",
      language: "inno",
      code: `; Script generated by HNL CAD AI TOOL
#define MyAppName "HNL CAD AI TOOL"
#define MyAppVersion "${HNL_APP_VERSION}"
#define MyAppPublisher "HNL Architecture & CAD AI Studio"
#define MyAppURL "https://hnlcad.vn"

[Setup]
AppId={{C8D3A56F-9E4B-4E92-9C8F-7B32D15A2026}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={userappdata}\\Autodesk\\ApplicationPlugins\\HnlCadTool.bundle
DisableDirPage=yes
OutputBaseFilename=HnlCADTool_AutoCAD_Setup_v${HNL_APP_VERSION}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest

[Files]
Source: "PackageContents.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\\Release\\net8.0-windows\\*"; DestDir: "{app}\\Contents\\Windows"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "dist\\*"; DestDir: "{app}\\Contents\\Windows\\wwwroot"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\\{#MyAppName} Settings"; Filename: "{app}\\Contents\\Windows\\HnlSettings.exe"

[Code]
function InitializeSetup(): Boolean;
begin
  // Kiểm tra AutoCAD đã cài đặt
  Result := True;
end;`,
    },

    "HnlCadTool.csproj": {
      desc: ".NET 8 / C# Project file với tham chiếu thư viện AutoCAD API (accoremgd.dll, acdbmgd.dll, acmgd.dll)",
      language: "xml",
      code: `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <Platforms>x64</Platforms>
    <AssemblyName>HnlCadTool</AssemblyName>
    <RootNamespace>Hnl.Cad.Plugin</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AutoCAD.NET" Version="24.3.0" ExcludeAssets="runtime" />
    <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2592.51" />
    <PackageReference Include="System.Text.Json" Version="8.0.4" />
  </ItemGroup>
</Project>`,
    },

    "hnl-core.lsp": {
      desc: "AutoLISP Core bridge script nạp tự động khi khởi động AutoCAD",
      language: "lisp",
      code: `;;; =========================================================================
;;; HNL CAD AI TOOL - AUTOLISP CORE ENGINE (Shopdrawing & MLeader Edition)
;;; =========================================================================

(vl-load-com)

;; Shortcut lệnh nhanh
(defun c:HNL () (c:HNL_AI))
(defun c:HNL_AI ()
  (vl-cmdf "HNL_AI")
  (princ "\\n[HNL CAD] Đang mở AI Copilot Palette...")
  (princ)
)

;; MLeader Ghi chú nhanh
(defun c:MLD ()
  (vl-cmdf "MLEADER")
  (princ)
)

(princ "\\n[HNL CAD TOOL] AutoLISP Core Bridge Loaded. Type 'HNL' to start.\\n")
(princ)`,
    },
  };

  const currentFile = pluginFiles[selectedFile];

  const handleCopy = () => {
    navigator.clipboard.writeText(currentFile.code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDownloadFile = () => {
    const blob = new Blob([currentFile.code], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = selectedFile;
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="w-full max-w-5xl h-[85vh] bg-[#1E1F22] rounded-xl border border-neutral-700 shadow-2xl overflow-hidden flex flex-col">
        {/* Header */}
        <div className="h-14 px-6 bg-[#141517] border-b border-neutral-800 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="w-8 h-8 rounded bg-gradient-to-r from-blue-600 to-cyan-500 flex items-center justify-center text-white">
              <Package className="w-5 h-5" />
            </div>
            <div>
              <h2 className="font-bold text-white text-sm">BỘ CÀI ĐẶT .NET C# PLUGIN HNL CHO AUTOCAD 2023+</h2>
              <p className="text-xs text-neutral-400">
                Tích hợp trực tiếp vào AutoCAD dưới dạng .bundle hoặc Inno Setup .exe tự động
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-neutral-800 text-neutral-400 hover:text-white transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content Layout */}
        <div className="flex-1 flex overflow-hidden">
          {/* File Sidebar */}
          <div className="w-72 bg-[#18191C] border-r border-neutral-800 p-3 flex flex-col space-y-1">
            <span className="text-[11px] font-semibold text-neutral-500 uppercase px-2 mb-1">
              Cấu trúc File Plugin (.bundle)
            </span>
            {Object.keys(pluginFiles).map((fileName) => (
              <button
                key={fileName}
                onClick={() => setSelectedFile(fileName)}
                className={`w-full text-left px-3 py-2 rounded-lg text-xs font-mono flex items-center space-x-2.5 transition ${
                  selectedFile === fileName
                    ? "bg-cyan-500/20 text-cyan-400 font-bold border border-cyan-500/40"
                    : "text-neutral-300 hover:bg-neutral-800/60"
                }`}
              >
                <FileCode className="w-4 h-4 shrink-0" />
                <span className="truncate">{fileName}</span>
              </button>
            ))}

            <div className="mt-auto p-3 bg-neutral-900/80 rounded-lg border border-neutral-800 text-[11px] text-neutral-400 space-y-1.5">
              <div className="font-bold text-neutral-200">Đường dẫn cài đặt chuẩn:</div>
              <div className="font-mono text-[10px] text-sky-400 break-all">
                %APPDATA%\Autodesk\ApplicationPlugins\HnlCadTool.bundle
              </div>
            </div>
          </div>

          {/* Code Viewer */}
          <div className="flex-1 flex flex-col bg-[#141517]">
            <div className="h-10 px-4 bg-[#1C1D20] border-b border-neutral-800 flex items-center justify-between text-xs">
              <div className="text-neutral-300 font-mono flex items-center space-x-2">
                <span className="font-semibold text-cyan-400">{selectedFile}</span>
                <span className="text-neutral-500">—</span>
                <span className="text-[11px] text-neutral-400 truncate max-w-md">{currentFile.desc}</span>
              </div>

              <div className="flex items-center space-x-2">
                <button
                  onClick={handleCopy}
                  className="flex items-center space-x-1.5 px-3 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200 transition text-xs"
                >
                  {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                  <span>{copied ? "Đã chép" : "Sao chép"}</span>
                </button>
                <button
                  onClick={handleDownloadFile}
                  className="flex items-center space-x-1.5 px-3 py-1 rounded bg-cyan-600 hover:bg-cyan-500 text-white font-medium transition text-xs shadow"
                >
                  <Download className="w-3.5 h-3.5" />
                  <span>Tải file</span>
                </button>
              </div>
            </div>

            <div className="flex-1 p-4 overflow-auto font-mono text-xs text-neutral-300 leading-relaxed">
              <pre className="whitespace-pre">{currentFile.code}</pre>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
