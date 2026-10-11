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
$hostBridgeSource = Source 'src/HNL.VXT.AutoCAD/VxtHostBridge.cs'
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

$called = @([regex]::Matches($hostBridgeSource, '"(?<cmd>(?:HNLVXT|HNLCF)[A-Z0-9]+)\s') |
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

# Guard the redundant Lay-in DIM captions and the point-based door picker from returning.
# This catches source/UI regressions but not an old AutoCAD DLL already in memory.
if ($xaml.Contains('Text="DIM ô nguyên + mép cắt."') -or
    $xaml.Contains('Text="Số ô gộp"') -or
    $xaml.Contains('Text="Tự tính theo lưới và ô nguyên"') -or
    -not $xaml.Contains('Content="Chọn block cửa"') -or
    $xaml.Contains('Content="Chọn điểm cửa"') -or
    -not $registeredSource.Contains('LayInCadRuntime.PickDoorBlock();')) {
    throw 'HCF audit: old Lay-in DIM captions or door point UI were restored.'
}

# Concealed preview controls must fill the card like the Lay-in three-column actions.
# Check both the three equal star columns and the unchanged button commands/icons.
$concealedPreviewActions = [regex]::Match($xaml,
    '(?s)<Grid x:Name="ConcealedPreviewActions"(?<body>.*?)</Grid>')
if (-not $concealedPreviewActions.Success) {
    throw 'HCF audit: missing the concealed preview 3-column action row.'
}
$actionRow = $concealedPreviewActions.Value
if ([regex]::Matches($actionRow, '<ColumnDefinition Width="\*"').Count -ne 3 -or
    [regex]::Matches($actionRow, '<Button Grid.Column=').Count -ne 3) {
    throw 'HCF audit: concealed preview actions no longer span three equal-width columns.'
}
foreach ($entry in @(
    @{ Index = 0; Label = 'Cập nhật'; Command = 'RefreshPreviewCommand'; Style = 'RefreshButton' },
    @{ Index = 1; Label = 'Ẩn'; Command = 'ClearPreviewCommand'; Style = 'HidePreviewButton' },
    @{ Index = 2; Label = 'Đặt lại'; Command = 'ResetCommand'; Style = 'ResetButton' }
)) {
    $expectedButton = '<Button Grid.Column="' + $entry.Index +
        '" Content="' + $entry.Label + '" Command="{Binding ' + $entry.Command +
        '}" Style="{StaticResource ' + $entry.Style + '}"'
    if (-not $actionRow.Contains($expectedButton)) {
        throw "HCF audit: concealed preview action button/command changed: $($entry.Label)"
    }
}

# Lay-in source/visibility toggles must keep Preview/Create in parity:
# an existing Hatch is used as read-only source and must never be duplicated.
$layInUi = $xaml.Substring($layInStart, $layInEnd - $layInStart)
foreach ($toggle in @('DrawHatch','DrawHangers','DrawDimensions','DrawStartTileBlock')) {
    $signature = 'IsChecked="{Binding ' + $toggle + ', Mode=TwoWay}"'
    if (@([regex]::Matches($layInUi, [regex]::Escape($signature))).Count -ne 1) {
        throw "HCF audit: Lay-in $toggle right-side switch is missing or duplicated."
    }
}
if (-not $layInUi.Contains('SelectExistingHatchesCommand') -or
    -not $layInUi.Contains('Chọn Hatch có sẵn') -or
    -not $registeredSource.Contains('LayInCadRuntime.PickExistingHatches();') -or
    -not $hostBridgeSource.Contains('Send("HNLCFLAYINHATCHSOURCE ");') -or
    $registered -notcontains 'HNLCFLAYINHATCHSOURCE') {
    throw 'HCF audit: Hatch source picker is not connected end to end.'
}
$layInCore = Source 'src/HNL.VXT.Core/Preview/LayInCeilingPlanner.cs'
$layInRuntime = Source 'src/HNL.VXT.AutoCAD/LayInRuntimePlanner.cs'
$layInCad = Source 'src/HNL.VXT.AutoCAD/LayInCadRuntime.cs'
$layInSettings = Source 'src/HNL.VXT.Core/Models/LayInCeilingSettings.cs'
if (-not $layInCore.Contains('settings.ExistingHatchOrigin.HasValue') -or
    -not $layInCore.Contains('settings.DrawDimensions') -or
    -not $layInRuntime.Contains('perRegionSettings.ExistingHatchOrigin = hatch.Origin;') -or
    -not $layInCad.Contains('if (settings.DrawHatch)') -or
    -not $layInCad.Contains('if (session.LayInSettings.DrawHatch)') -or
    -not $layInCad.Contains('hatchCount != (settings.DrawHatch ? plans.Count : 0)') -or
    -not $layInSettings.Contains('public bool DrawHatch') -or
    -not $layInSettings.Contains('public bool DrawDimensions')) {
    throw 'HCF audit: selected Hatch phase or zero-Hatch Create parity gate missing.'
}
if ($layInUi.Contains('Content="Vẽ Ty"') -or
    $layInUi.Contains('Content="Chèn Block ô đầu"')) {
    throw 'HCF audit: duplicate legacy Lay-in toggles were reintroduced.'
}

# Lay-in Create MUST first materialize its debounced Preview and require another
# click rather than produce unpreviewed changes in the drawing.
if (-not $hostBridgeSource.Contains('if (_pendingLayInPreviewSettings != null)') -or
    -not $hostBridgeSource.Contains('Send(session.HasBoundary ? "HNLCFLAYINPREVIEW " : "HNLCFLAYINCLEAR ");') -or
    -not $hostBridgeSource.Contains('Send("HNLCFLAYINCREATE ");')) {
    throw 'HCF audit: Lay-in Preview/Create WYSIWYG gate missing.'
}
if (-not $xaml.Contains('Click="StopHcfButton_Click"') -or
    -not $hostBridgeSource.Contains('Send("HNLCFSTOP ");') -or
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
# STOP is a tall, independent rail control, not a 27-DIP form button.
# The old blanket compaction clipped its power icon and truncated the word "Tắt".
$compact = Source 'src/HNL.VXT.UI/Views/VxtPaletteCompactTuner.cs'
$stop = [regex]::Match($xaml, '(?s)<Button\s+x:Name="StopHcfButton"(?<body>.*?)</Button>')
if (-not $stop.Success -or -not $stop.Value.Contains('Height="64"') -or
    $stop.Value.Contains('RotateTransform') -or
    -not $stop.Value.Contains('Text="Tắt"') -or
    -not $compact.Contains('string.Equals(button.Name, "StopHcfButton", StringComparison.Ordinal)')) {
    throw 'HCF audit: STOP navigation power icon and horizontal label can be clipped by global compact sizing.'
}
if ([regex]::Matches($xaml, '<RotateTransform Angle="-90"').Count -ne 2) {
    throw 'HCF audit: only the two mode tabs may use -90 degree labels.'
}

# Audit *publicly displayed* legacy branding, while preserving HNL.VXT
# namespaces, CAD command IDs, Registry licensing and historical log paths.
$legacyUserLabel = 'HNL\s*Tool\s*-\s*VXT\s*Pro|HNL\s+VXT\s+Pro|HNL\s+VXT\s+Diagnostic|HNL\s+TOOL\s*-\s*VXT'
$sourceRoot = Join-Path $Root 'src'
foreach ($sourceFile in (Get-ChildItem -LiteralPath $sourceRoot -Recurse -File |
        Where-Object { $_.Extension -eq '.cs' -or $_.Extension -eq '.xaml' })) {
    $lineNo = 0
    foreach ($line in (Get-Content -LiteralPath $sourceFile.FullName -Encoding UTF8)) {
        $lineNo++
        $trimmed = $line.TrimStart()
        if ($trimmed.StartsWith('//') -or $trimmed.StartsWith('*') -or
            $trimmed.StartsWith('<!--')) { continue }
        if ($line -match $legacyUserLabel -and
            $line -match '["\x27]') {
            throw "HCF audit: visible old VXT branding remains at $($sourceFile.Name):$lineNo"
        }
    }
}
Write-Host 'HNL HCF navigation rail and user-visible brand label audit PASS.'

Write-Host "HNL HCF button audit PASS: $($bindings.Count) XAML command bindings, $($called.Count) host command calls, $($registered.Count) registered commands, 3 manifests."
