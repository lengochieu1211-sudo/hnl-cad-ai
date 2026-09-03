param(
  [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class HnlInstallerBranding
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DrawLogo(Graphics g, float x, float y, float w, float h)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new SolidBrush(Color.FromArgb(14, 165, 233)))
        using (var pen = new Pen(Color.White, Math.Max(2f, w * 0.065f)))
        using (var path = RoundedRect(new RectangleF(x, y, w, h), Math.Min(w, h) * 0.16f))
        {
            pen.StartCap = LineCap.Square;
            pen.EndCap = LineCap.Square;
            g.FillPath(bg, path);

            var sx = w / 108f;
            var sy = h / 68f;
            Func<float, float> X = px => x + px * sx;
            Func<float, float> Y = py => y + py * sy;

            // H
            g.DrawLine(pen, X(17.5f), Y(20), X(17.5f), Y(48));
            g.DrawLine(pen, X(37.5f), Y(20), X(37.5f), Y(48));
            g.DrawLine(pen, X(18), Y(34), X(37), Y(34));
            // N
            g.DrawLine(pen, X(52.5f), Y(20), X(52.5f), Y(48));
            g.DrawLine(pen, X(73.5f), Y(20), X(73.5f), Y(48));
            g.DrawLine(pen, X(54), Y(21), X(72), Y(47));
            // L
            g.DrawLine(pen, X(86.5f), Y(20), X(86.5f), Y(48));
            g.DrawLine(pen, X(87), Y(48), X(103), Y(48));
        }
    }

    public static void Generate(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        using (var bitmap = new Bitmap(256, 256))
        {
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                DrawLogo(g, 18, 47, 220, 139);
            }

            var handle = bitmap.GetHicon();
            try
            {
                using (var icon = Icon.FromHandle(handle))
                using (var stream = File.Create(Path.Combine(outputDirectory, "HNL-VXT.ico")))
                    icon.Save(stream);
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        using (var small = new Bitmap(64, 64))
        {
            using (var g = Graphics.FromImage(small))
            {
                g.Clear(Color.White);
                DrawLogo(g, 4, 13, 56, 35);
            }
            small.Save(Path.Combine(outputDirectory, "HNL-VXT-Small.bmp"), ImageFormat.Bmp);
        }
    }
}
"@

$out = Join-Path $Root 'artifacts\installer-assets'
[HnlInstallerBranding]::Generate($out)

$iconPath = Join-Path $out 'HNL-VXT.ico'
$smallPath = Join-Path $out 'HNL-VXT-Small.bmp'
if (-not (Test-Path $iconPath)) { throw "Missing generated installer icon: $iconPath" }
if (-not (Test-Path $smallPath)) { throw "Missing generated wizard image: $smallPath" }

Write-Host 'HNL installer branding generated:'
Write-Host "  $iconPath"
Write-Host "  $smallPath"
