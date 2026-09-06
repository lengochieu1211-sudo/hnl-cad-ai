param(
  [Parameter(Mandatory = $true)][string]$ExePath,
  [Parameter(Mandatory = $true)][string]$IcoPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $ExePath)) { throw "Installer EXE not found: $ExePath" }
if (-not (Test-Path $IcoPath)) { throw "Source ICO not found: $IcoPath" }

$code = @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class HnlPeIconReader
{
    private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    private const uint LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;
    private static readonly IntPtr RT_ICON = new IntPtr(3);
    private static readonly IntPtr RT_GROUP_ICON = new IntPtr(14);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    private delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpszType, EnumResNameProc lpEnumFunc, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr hResData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

    private static byte[] ReadResource(IntPtr module, IntPtr name, IntPtr type)
    {
        IntPtr info = FindResource(module, name, type);
        if (info == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "FindResource failed");
        uint size = SizeofResource(module, info);
        if (size == 0) throw new InvalidOperationException("Resource size is zero.");
        IntPtr handle = LoadResource(module, info);
        if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "LoadResource failed");
        IntPtr ptr = LockResource(handle);
        if (ptr == IntPtr.Zero) throw new InvalidOperationException("LockResource failed.");
        byte[] bytes = new byte[size];
        Marshal.Copy(ptr, bytes, 0, checked((int)size));
        return bytes;
    }

    public static byte[] ReadLargestIconImage(string exePath, out int width)
    {
        IntPtr module = LoadLibraryEx(exePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE);
        if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "LoadLibraryEx failed");
        try
        {
            var names = new List<IntPtr>();
            EnumResNameProc callback = delegate(IntPtr h, IntPtr t, IntPtr n, IntPtr p)
            {
                names.Add(n);
                return true;
            };
            if (!EnumResourceNames(module, RT_GROUP_ICON, callback, IntPtr.Zero) || names.Count == 0)
                throw new InvalidOperationException("No RT_GROUP_ICON resource found in installer EXE.");

            byte[] group = ReadResource(module, names[0], RT_GROUP_ICON);
            if (group.Length < 6) throw new InvalidOperationException("Invalid GROUP_ICON resource header.");
            ushort count = BitConverter.ToUInt16(group, 4);
            if (count == 0 || group.Length < 6 + count * 14)
                throw new InvalidOperationException("Invalid GROUP_ICON resource entries.");

            int bestWidth = -1;
            ushort bestId = 0;
            for (int i = 0; i < count; i++)
            {
                int p = 6 + i * 14;
                int w = group[p] == 0 ? 256 : group[p];
                ushort id = BitConverter.ToUInt16(group, p + 12);
                if (w > bestWidth)
                {
                    bestWidth = w;
                    bestId = id;
                }
            }
            if (bestId == 0) throw new InvalidOperationException("Largest icon resource ID could not be resolved.");
            width = bestWidth;
            return ReadResource(module, new IntPtr(bestId), RT_ICON);
        }
        finally
        {
            FreeLibrary(module);
        }
    }
}
'@

Add-Type -TypeDefinition $code -Language CSharp

function Get-IcoLargestImageBytes {
  param([string]$Path)
  $bytes = [IO.File]::ReadAllBytes($Path)
  if ($bytes.Length -lt 22) { throw 'Source ICO is too small.' }
  $reserved = [BitConverter]::ToUInt16($bytes, 0)
  $type = [BitConverter]::ToUInt16($bytes, 2)
  $count = [BitConverter]::ToUInt16($bytes, 4)
  if ($reserved -ne 0 -or $type -ne 1 -or $count -lt 1) { throw 'Invalid source ICO header.' }

  $bestSize = -1
  $bestOffset = 0
  $bestLength = 0
  for ($i = 0; $i -lt $count; $i++) {
    $p = 6 + (16 * $i)
    $w = if ($bytes[$p] -eq 0) { 256 } else { [int]$bytes[$p] }
    $len = [BitConverter]::ToUInt32($bytes, $p + 8)
    $off = [BitConverter]::ToUInt32($bytes, $p + 12)
    if ($w -gt $bestSize) {
      $bestSize = $w
      $bestOffset = [int]$off
      $bestLength = [int]$len
    }
  }
  if ($bestOffset -lt 0 -or $bestLength -le 0 -or ($bestOffset + $bestLength) -gt $bytes.Length) {
    throw 'Invalid largest frame offset in source ICO.'
  }
  $frame = New-Object byte[] $bestLength
  [Array]::Copy($bytes, $bestOffset, $frame, 0, $bestLength)
  return [PSCustomObject]@{ Size = $bestSize; Data = [byte[]]$frame }
}

function Test-PngFrame {
  param([byte[]]$Data, [int]$ExpectedSize, [string]$Label)
  if ($Data.Length -lt 8 -or
      $Data[0] -ne 0x89 -or $Data[1] -ne 0x50 -or $Data[2] -ne 0x4E -or $Data[3] -ne 0x47) {
    throw "$Label largest icon frame is not PNG-compressed."
  }
  $ms = [IO.MemoryStream]::new($Data, $false)
  $img = $null
  try {
    $img = [Drawing.Image]::FromStream($ms, $true, $true)
    if ($img.Width -ne $ExpectedSize -or $img.Height -ne $ExpectedSize) {
      throw "$Label largest icon decoded as $($img.Width)x$($img.Height); expected ${ExpectedSize}x${ExpectedSize}."
    }
  }
  finally {
    if ($img -ne $null) { $img.Dispose() }
    $ms.Dispose()
  }
}

$source = Get-IcoLargestImageBytes -Path $IcoPath
$exeWidth = 0
$exeFrame = [HnlPeIconReader]::ReadLargestIconImage((Resolve-Path $ExePath).Path, [ref]$exeWidth)

if ($source.Size -ne 256) { throw "Source ICO largest frame is $($source.Size), expected 256." }
if ($exeWidth -ne 256) { throw "Installer EXE largest embedded icon is $exeWidth, expected 256." }
Test-PngFrame -Data $source.Data -ExpectedSize 256 -Label 'Source ICO'
Test-PngFrame -Data $exeFrame -ExpectedSize 256 -Label 'Installer EXE'

$sourceHash = ([Security.Cryptography.SHA256]::Create()).ComputeHash($source.Data)
$exeHash = ([Security.Cryptography.SHA256]::Create()).ComputeHash($exeFrame)
$sourceHex = ($sourceHash | ForEach-Object { $_.ToString('x2') }) -join ''
$exeHex = ($exeHash | ForEach-Object { $_.ToString('x2') }) -join ''
if ($sourceHex -ne $exeHex) {
  throw "Installer EXE 256px icon bytes differ from source ICO. source=$sourceHex exe=$exeHex"
}

Write-Host "HNL installer embedded-icon gate PASS: 256px PNG resource matches source exactly ($exeHex)"
