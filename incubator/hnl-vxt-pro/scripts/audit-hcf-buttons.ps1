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


# Narrow Palette: each Lay-in Layer field occupies its own aligned row.
# Keep all 4 dark-theme editable layer selectors; prevent multi-column appearance regression.
$layerSectionStart = $layInUi.IndexOf('<Expander Header="6. Layer"')
$layerSectionEnd = $layInUi.IndexOf('</Expander>', $layerSectionStart)
if ($layerSectionStart -lt 0 -or $layerSectionEnd -le $layerSectionStart) {
    throw 'HCF Layer UI: missing 6. Layer section.'
}
$layInLayerUi = $layInUi.Substring($layerSectionStart, $layerSectionEnd - $layerSectionStart)
if (-not $xaml.Contains('<Style x:Key="HcfEditableLayerCombo"') -or
    -not $xaml.Contains('x:Name="PART_EditableTextBox"') -or
    @([regex]::Matches($layInLayerUi, 'Style="\{StaticResource HcfEditableLayerCombo\}"')).Count -ne 4 -or
    @([regex]::Matches($layInLayerUi, 'SelectedItem="\{Binding (Hatch|Hanger|Dimension|StartMarker)Linetype,')).Count -ne 4 -or
    $layInLayerUi.Contains('IsEditable="True"') -or
    $layInLayerUi.Contains('<ColumnDefinition Width="*"/><ColumnDefinition Width="*"/><ColumnDefinition Width="*"/>') -or
    $layInLayerUi.Contains('<ColumnDefinition Width="*"/><ColumnDefinition Width="*"/>') -or
    @([regex]::Matches($layInLayerUi, '<Grid.ColumnDefinitions><ColumnDefinition Width="132"/><ColumnDefinition Width="\*"/></Grid.ColumnDefinitions>')).Count -ne 16 -or
    @([regex]::Matches($layInLayerUi, '<TextBlock Text="(Màu|Kiểu nét|Độ dày nét)" Style="\{StaticResource FieldLabel\}"/>')).Count -ne 12 -or
    @([regex]::Matches($layInLayerUi, '<Grid MinHeight="29"')).Count -ne 16 -or
    @([regex]::Matches($layInLayerUi, 'Margin="0,7,0,0"')).Count -ne 12) {
    throw 'HCF Layer UI: invalid themed ComboBox or per-field single-row layout regression.'
}

# A selected Lay-in Hatch is never a concealed Polyline boundary.
$paletteVm = Source 'src/HNL.VXT.UI/ViewModels/VxtPaletteViewModel.cs'
$layInVm = Source 'src/HNL.VXT.UI/ViewModels/LayInCeilingViewModel.cs'
if (-not $paletteVm.Contains('if (LayIn.HasExistingHatchSource)') -or
    -not $layInVm.Contains('public bool HasExistingHatchSource => _usingExistingHatch;') -or
    -not $registeredSource.Contains('if (RejectLayInHatchSourceForConcealed()) return;')) {
    throw 'HCF audit: a Lay-in Hatch source can leak into concealed Preview/Create.'
}

# Lay-in Create MUST first materialize its debounced Preview and require another
# click rather than produce unpreviewed changes in the drawing.
if (-not $hostBridgeSource.Contains('if (_pendingLayInPreviewSettings != null)') -or
    -not $hostBridgeSource.Contains('Send(session.HasBoundary ? "HNLCFLAYINPREVIEW " : "HNLCFLAYINCLEAR ");') -or
    -not $hostBridgeSource.Contains('Send("HNLCFLAYINCREATE ");')) {
    throw 'HCF audit: Lay-in Preview/Create WYSIWYG gate missing.'
}
# HCF STOP: icon-only action on a dark warning surface, independent of light/dark theme.
$stopUiStart = $xaml.IndexOf('<Button x:Name="StopHcfButton"')
$stopUiEnd = $xaml.IndexOf('</Button>', $stopUiStart)
if ($stopUiStart -lt 0 -or $stopUiEnd -le $stopUiStart -or
    -not $xaml.Contains('<Style x:Key="HcfStopRailButton"') -or
    -not $xaml.Contains('Value="#B83B45"') -or
    -not $xaml.Contains('Value="#D34B57"')) {
    throw 'HCF Stop UI: themed icon-only STOP style missing.'
}
$stopUi = $xaml.Substring($stopUiStart, $stopUiEnd - $stopUiStart)
if (-not $stopUi.Contains('Style="{StaticResource HcfStopRailButton}"') -or
    -not $stopUi.Contains('AutomationProperties.Name="Tắt HCF"') -or
    -not $stopUi.Contains('Tag="HCF_RAIL_STOP_ICON"') -or
    $stopUi.Contains('<TextBlock') -or
    $stopUi.Contains('Background="#FFF1F2"')) {
    throw 'HCF Stop UI: white STOP background or redundant visible label.'
}
if (-not [regex]::IsMatch($xaml, 'Padding="8">\s*<StackPanel DataContext="\{Binding LayIn\}">')) {
    throw 'HCF UI: lay-in ScrollViewer padding no longer matches concealed tab.'
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
# STOP is a square icon-only rail control. The compact tuner must not resize it.
$compact = Source 'src/HNL.VXT.UI/Views/VxtPaletteCompactTuner.cs'
$stop = [regex]::Match($xaml, '(?s)<Button\s+x:Name="StopHcfButton"(?<body>.*?)</Button>')
if (-not $stop.Success -or
    -not $stop.Value.Contains('Style="{StaticResource HcfStopRailButton}"') -or
    -not $stop.Value.Contains('Tag="HCF_RAIL_STOP_ICON"') -or
    -not $stop.Value.Contains('AutomationProperties.Name="Tắt HCF"') -or
    $stop.Value.Contains('RotateTransform') -or
    $stop.Value.Contains('<TextBlock') -or
    -not $compact.Contains('string.Equals(button.Name, "StopHcfButton", StringComparison.Ordinal)')) {
    throw 'HCF audit: icon-only STOP rail or compact-tuner exemption regressed.'
}
if ([regex]::Matches($xaml, '<RotateTransform Angle="-90"').Count -ne 2) {
    throw 'HCF audit: only the two mode tabs may use -90 degree labels.'
}

# Unified section number colors: both ceiling tabs use the same SectionNumber / SectionTitle styles.
$layInHeaderUi = $layInUi.Substring(0, $layerSectionStart)
foreach ($section in @(
    @('1.', 'Chọn biên'), @('2.', 'Hệ khung'), @('3.', 'Gốc lưới / Đuổi tấm'),
    @('4.', 'Ty treo'), @('5.', 'DIM'))) {
    $sectionNum = [regex]::Escape($section[0])
    $sectionLabel = [regex]::Escape($section[1])
    $sectionPattern = '<StackPanel Orientation="Horizontal"[^>]*><TextBlock Text="' +
        $sectionNum + '" Style="\{StaticResource SectionNumber\}"/><TextBlock Text="' +
        $sectionLabel + '" Style="\{StaticResource SectionTitle\}"'
    if (-not [regex]::IsMatch($layInHeaderUi, $sectionPattern)) {
        throw "HCF UI: Lay-in section $($section[0]) does not share concealed heading styles."
    }
}
if (-not $layInLayerUi.Contains('<Expander.HeaderTemplate>') -or
    -not $layInLayerUi.Contains('Text="6." Style="{StaticResource SectionNumber}"') -or
    -not $layInLayerUi.Contains('Text="Layer" Style="{StaticResource SectionTitle}"')) {
    throw 'HCF UI: Lay-in Layer header no longer shares concealed section colors.'
}
if (-not $xaml.Contains('<Style x:Key="PickButton" TargetType="Button" BasedOn="{StaticResource CompactButton}">') -or
    -not $xaml.Contains('<Style x:Key="BoundaryPickButton" TargetType="Button" BasedOn="{StaticResource CompactButton}">') -or
    $xaml.Contains('<Style x:Key="PickButton" TargetType="Button" BasedOn="{StaticResource IconCompactButton}">')) {
    throw 'HCF UI: Select buttons must be text-only without decorative Pick arrow.'
}
if (-not $xaml.Contains('<Border Grid.Row="2" HorizontalAlignment="Right" VerticalAlignment="Bottom" Width="40"') -or
    -not $xaml.Contains('<Border Grid.Row="2" Margin="0,0,40,0"')) {
    throw 'HCF UI: STOP must occupy bottom-right cell without covering Create.'
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

# Complete UI/localization audit: both tabs must be traversed even when one is inactive.
$typographyUi = Source 'src/HNL.VXT.UI/Views/VxtPaletteTypography.cs'
$numericUi = Source 'src/HNL.VXT.UI/Controls/HnlNumericBox.xaml'
$diagnosticUi = Source 'src/HNL.VXT.UI/Views/VxtPaletteView.xaml.cs'
$diagnosticVmUi = Source 'src/HNL.VXT.UI/ViewModels/VxtPaletteViewModel.Diagnostics.cs'
$themeUi = Source 'src/HNL.VXT.UI/Views/VxtPaletteEnhancer.cs'
if (-not $typographyUi.Contains('var tabs = current as TabControl;') -or
    -not $typographyUi.Contains('foreach (var item in tabs.Items)') -or
    -not $typographyUi.Contains('var sectionNumberStyle = view.TryFindResource("SectionNumber") as Style;') -or
    -not $typographyUi.Contains('var badgeStyle = view.TryFindResource("DomainBadgeText") as Style;') -or
    -not $typographyUi.Contains('combo.Height = 27.0;') -or
    -not $typographyUi.Contains('grid.MinHeight = 29.0;')) {
    throw 'HCF UI: typography, row metrics or inactive ceiling tab normalization regressed.'
}
if (-not $numericUi.Contains('MinHeight="27"') -or
    -not $numericUi.Contains('Background="{DynamicResource InputBackground}"') -or
    -not $numericUi.Contains('Foreground="{DynamicResource PrimaryText}"') -or
    -not $numericUi.Contains('BorderBrush="{DynamicResource InputBorder}"')) {
    throw 'HCF UI: numeric editor height or dark/light theme colors regressed.'
}
if (-not $themeUi.Contains('if (dark) { primary = "#F8FAFC"; secondary = "#CBD5E1"; }') -or
    -not $themeUi.Contains('if (!dark) { primary = "#172033"; secondary = "#475569"; }')) {
    throw 'HCF UI: user text-color preference can create illegible dark/light contrast.'
}
foreach ($token in @(
    'Text = "Kiểm tra bố trí"',
    'Content = "Phân tích nhanh"',
    'Content = "Xuất gói chẩn đoán ZIP"',
    'Content = "Chạy kiểm tra chuẩn 6000×4000"',
    'statusBox.SetResourceReference(Border.BackgroundProperty, "InputBackground");',
    'constraintList.SetResourceReference(ListBox.ForegroundProperty, "PrimaryText");')) {
    if (-not $diagnosticUi.Contains($token)) {
        throw "HCF UI: diagnostic layout/theme/localization missing: $token"
    }
}
if ($diagnosticUi.Contains('Xuất Diagnostic ZIP') -or
    $diagnosticUi.Contains('highlight đúng Polyline') -or
    $diagnosticVmUi.Contains('Chưa có gói Diagnostic ZIP.') -or
    $diagnosticVmUi.Contains('HNL Tool đang chạy Runtime Golden')) {
    throw 'HCF UI: mixed-language diagnostic captions were reintroduced.'
}
foreach ($technical in @('Hatch','Block','DIM','MEP','Layer')) {
    if (-not $xaml.Contains($technical)) {
        throw "HCF UI: required technical CAD term disappeared: $technical"
    }
}
