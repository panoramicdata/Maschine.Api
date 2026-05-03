$ErrorActionPreference = 'Stop'

$repo = Join-Path $env:TEMP 'adobe-75dpi'
$regular = Join-Path $repo 'helvR10.bdf'
$bold = Join-Path $repo 'helvB10.bdf'

if (-not (Test-Path $regular) -or -not (Test-Path $bold)) {
    throw "Expected BDF files not found in $repo."
}

function Get-GlyphRows {
    param([string]$Path)

    $lines = Get-Content $Path
    $fontAscent = 0
    $fontDescent = 0
    $fontHeight = 12

    foreach ($line in $lines) {
        if ($line -like 'FONT_ASCENT *') {
            $fontAscent = [int]($line.Split(' ')[1])
        }

        if ($line -like 'FONT_DESCENT *') {
            $fontDescent = [int]($line.Split(' ')[1])
        }
    }

    if ($fontAscent -gt 0 -and $fontDescent -ge 0) {
        $fontHeight = $fontAscent + $fontDescent
    }

    $glyphs = @{}
    $encoding = -1
    $bbxW = 0
    $bbxH = 0
    $bbxX = 0
    $bbxY = 0
    $bitmap = @()
    $inChar = $false
    $inBitmap = $false

    foreach ($line in $lines) {
        if ($line -like 'STARTCHAR *') {
            $inChar = $true
            $inBitmap = $false
            $encoding = -1
            $bbxW = 0
            $bbxH = 0
            $bbxX = 0
            $bbxY = 0
            $bitmap = @()
            continue
        }

        if (-not $inChar) {
            continue
        }

        if ($line -like 'ENCODING *') {
            $encoding = [int]($line.Split(' ')[1])
            continue
        }

        if ($line -like 'BBX *') {
            $parts = $line.Split(' ')
            $bbxW = [int]$parts[1]
            $bbxH = [int]$parts[2]
            $bbxX = [int]$parts[3]
            $bbxY = [int]$parts[4]
            continue
        }

        if ($line -eq 'BITMAP') {
            $inBitmap = $true
            continue
        }

        if ($line -eq 'ENDCHAR') {
            if ($encoding -ge 32 -and $encoding -le 126) {
                $rows = New-Object 'System.UInt16[]' $fontHeight
                $top = $fontAscent - ($bbxY + $bbxH)

                for ($j = 0; $j -lt $bbxH -and $j -lt $bitmap.Count; $j++) {
                    $hex = $bitmap[$j]
                    if ([string]::IsNullOrWhiteSpace($hex)) {
                        continue
                    }

                    $src = [Convert]::ToUInt32($hex, 16)
                    $totalBits = $hex.Length * 4
                    $y = $top + $j
                    if ($y -lt 0 -or $y -ge $fontHeight) {
                        continue
                    }

                    for ($x = 0; $x -lt $bbxW; $x++) {
                        $bitIndex = $totalBits - 1 - $x
                        if ($bitIndex -lt 0) {
                            continue
                        }

                        $on = ($src -shr $bitIndex) -band 1
                        if ($on -eq 0) {
                            continue
                        }

                        $dstX = $x + $bbxX
                        if ($dstX -lt 0 -or $dstX -ge 16) {
                            continue
                        }

                        $rows[$y] = [uint16]($rows[$y] -bor (1 -shl $dstX))
                    }
                }

                $glyphs[$encoding] = $rows
            }

            $inChar = $false
            $inBitmap = $false
            continue
        }

        if ($inBitmap) {
            $bitmap += $line.Trim()
        }
    }

    return ,$glyphs
}

function Emit-Array {
    param(
        [hashtable]$Glyphs,
        [string]$Name
    )

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("internal static ReadOnlySpan<ushort> $Name =>")
    [void]$sb.AppendLine("[")

    for ($c = 32; $c -le 126; $c++) {
        $rows = $Glyphs[$c]
        if (-not $rows) {
            $rows = New-Object 'System.UInt16[]' 12
        }

        $commentChar = [char]$c
        if ($c -eq 0x5C) { $commentChar = '\\' }
        if ($c -eq 0x27) { $commentChar = "'" }

        [void]$sb.AppendLine(("    // 0x{0:X2} ({1})" -f $c, $commentChar))
        $vals = @()
        foreach ($r in $rows) {
            $vals += ("0x{0:X4}" -f $r)
        }

        [void]$sb.AppendLine("    " + ($vals -join ', ') + ",")
    }

    [void]$sb.AppendLine("];")
    return $sb.ToString()
}

$regularGlyphs = Get-GlyphRows -Path $regular
$boldGlyphs = Get-GlyphRows -Path $bold

$out = Join-Path $env:TEMP 'helv12-arrays.txt'
@(
    (Emit-Array -Glyphs $regularGlyphs -Name 'Font12x12HelvRegularGlyphs')
    ''
    (Emit-Array -Glyphs $boldGlyphs -Name 'Font12x12HelvBoldGlyphs')
) | Set-Content -Path $out -Encoding UTF8

Write-Host "Generated: $out"
Get-Content $out -TotalCount 24
Write-Host '...'
(Get-Content $out | Measure-Object -Line).Lines | Write-Host
