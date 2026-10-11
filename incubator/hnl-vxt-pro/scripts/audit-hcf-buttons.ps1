# HNL Tool — HCF static button/command audit.
# Runs in CI without AutoCAD GUI; Runtime Golden still requires real AutoCAD.
param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'

function Source([string]$relative) {
    $path = Join-Path $Root $relative
    if (-not (Test-Path -LiteralPath $path)) { throw "HCF audit: missing source $relative" }
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8
}

$xaml = Source 'src/HNL.VXT.UI/Views/VxtPaletteView.xaml'
$concealed = Source 'src/HNL.VXT.UI/ViewModels/VxtPaletteViewModel.cs'
$layin = Source 'src/HNL.VXT.UI/ViewModels/LayInCeilingViewModel.cs'
$registeredSource = Source 'src/HNL.VXT.AutoCAD/HnlVxtCommands.cs'
$host = Source 'src/HNL.VXT.AutoCAD/VxtHostBridge.cs'
$legacy = Source 'src/HNL.VXT.UI/Views/VxtPaletteView.LegacyCreate.cs'
$visual = Source 'src/HNL.VXT.UI/Views/VxtPaletteVisualIdentity.cs'
$resources = Source 'src/HNL.VXT.UI/Views/VxtPaletteRuntimePolish.cs'
$session = Source 'src/HNL.VXT.AutoCAD/VxtSession.cs'
$plugin = Source 'src/HNL.VXT.AutoCAD/PluginEntry.cs'

$registered = @([regex]::Matches($registeredSource, '\[CommandMethod\("(?<name>[A-Z0-9]+)"') |
    ForEach-Object { $_.Groups['name'].Value })
if (@($registered | Where-Object { $_ -eq 'HCF' }).Count -ne 1) {
    throw 'HCF audit: HCF must be the single registered public launcher.'
}
foreach ($alias in @('HVX','VXT','CFP')) {
    if ($registered -contains $alias) { throw "HCF audit: obsolete launcher $alias is registered." }
}
if (-not $plugin.Contains('CommandClass(typeof(HNL.VXT.AutoCAD.HnlVxtCommands))')) {
    throw 'HCF audit: actual AutoCAD CommandClass registration changed.'
}

$called = @([regex]::Matches($host, '"(?<cmd>(?:HNLVXT|HNLCF)[A-Z0-9]+)\s') |
    ForEach-Object { $_.Groups['cmd'].Value } | Sort-Object -Unique)
foreach ($command in $called) {
    if ($registered -notcontains $command) {
        throw "HCF audit: bridge references an unregistered AutoCAD command: $command"
    }
}

$layInStart = $xaml.IndexOf('<StackPanel DataContext="{Binding LayIn}">', [StringComparison]::Ordinal)
if ($layInStart -lt 0) { throw 'HCF audit: Lay-in tab has no LayIn DataContext.' }
$layInEnd = $xaml.IndexOf('</TabItem>', $layInStart, [StringComparison]::Ordinal)
if ($layInEnd -le $layInStart) { throw 'HCF audit: Lay-in tab boundary not found.' }

$bindings = [regex]::Matches($xaml, 'Command="\{Binding\s+(?<path>[A-Za-z0-9_.]+)')
if ($bindings.Count -lt 20) { throw "HCF audit: too few button command bindings: $($bindings.Count)" }
foreach ($match in $bindings) {
    $binding = $match.Groups['path'].Value
    $isLayIn = $match.Index -ge $layInStart -and $match.Index -lt $layInEnd
    if ($binding.StartsWith('LayIn.', [StringComparison]::Ordinal)) {
        $model = $layin
        $property = $binding.Substring(6)
    } elseif ($isLayIn) {
        $model = $layin
        $property = $binding
    } else {
        $model = $concealed
        $property = $binding
    }
    if ($model -notmatch ('public\s+ICommand\s+' + [regex]::Escape($property) + '\s*\{')) {
        throw "HCF audit: missing ViewModel command for UI button $binding."
    }
}

# The primary concealed Create is deliberately wired by a Loaded class handler.
# PaletteSet hide/show must NOT unsubscribe its state updates while retaining
# the permanent button-patched marker.
if (-not $xaml.Contains('x:Name="ConcealedCreateButton"') -or
    -not $legacy.Contains('button.Click += LegacyCreateClick;') -or
    -not $legacy.Contains('vm.PropertyChanged += handler;') -or
    $legacy.Contains('button.Unloaded +=')) {
    throw 'HCF audit: concealed Create button loses its handler on Palette reopen.'
}

# Lay-in Create MUST first materialize its debounced Preview and require another
# click rather than produce unpreviewed changes in the drawing.
if (-not $host.Contains('if (_pendingLayInPreviewSettings != null)') -or
    -not $host.Contains('Send(session.HasBoundary ? "HNLCFLAYINPREVIEW " : "HNLCFLAYINCLEAR ");') -or
    -not $host.Contains('Send("HNLCFLAYINCREATE ");')) {
    throw 'HCF audit: Lay-in Preview/Create WYSIWYG gate missing.'
}
if (-not $xaml.Contains('Click="StopHcfButton_Click"') -or
    -not $host.Contains('Send("HNLCFSTOP ");') -or
    $registered -notcontains 'HNLCFSTOP') {
    throw 'HCF audit: Stop button cannot safely reach command context.'
}
if (-not $visual.Contains('HNL.LayerDimIdentityAttached')) {
    throw 'HCF audit: Layer/DIM header handlers can multiply on Palette reopen.'
}
if (-not $resources.Contains('new Binding(nameof(VxtPaletteViewModel.DimStyleOptions))') -or
    -not $resources.Contains('new Binding(nameof(VxtPaletteViewModel.LinetypeOptions))') -or
    -not $session.Contains('ViewModel?.RefreshDrawingResourceChoices();')) {
    throw 'HCF audit: Layer/DIM selectors do not refresh after changing DWG.'
}

foreach ($manifestName in @(
    'build/HNL.VXT.bundle/PackageContents.xml',
    'build/universal/PackageContents.2026-net8.xml',
    'build/universal/PackageContents.2026-net10.xml')) {
    [xml]$manifest = Source $manifestName
    $entries = @($manifest.SelectNodes('//ComponentEntry'))
    if ($entries.Count -lt 1) { throw "HCF audit: empty bundle $manifestName" }
    foreach ($entry in $entries) {
        $names = @($entry.SelectNodes('./Commands/Command') |
            ForEach-Object { $_.GetAttribute('Global') })
        if (@($names | Where-Object { $_ -eq 'HCF' }).Count -ne 1) {
            throw "HCF audit: $manifestName missing or duplicated HCF entry."
        }
        if (@($names | Where-Object { $_ -eq 'HVX' -or $_ -eq 'CFP' -or $_ -match '^VXT' }).Count) {
            throw "HCF audit: $manifestName exposes obsolete public command."
        }
    }
}
Write-Host "HNL HCF button audit PASS: $($bindings.Count) XAML command bindings, $($called.Count) host command calls, $($registered.Count) registered commands, 3 manifests."
