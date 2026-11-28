param([string] $Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$directory = Join-Path $Root 'Resources/Textures/_BlackM/Objects/Misc/rule_books.rsi'
$image = [System.Drawing.Bitmap]::new((Join-Path $directory 'command.png'))
try {
    # Retain the existing books' silhouette, page edges and native 32px grid.
    $palette = @{
        'FF17203D' = '#17342D'
        'FF11182D' = '#10271F'
        'FF525D7E' = '#577367'
        'FF22305A' = '#245141'
        'FF1C284C' = '#1D4335'
        'FF1B2648' = '#1B3B2F'
    }
    for ($y = 0; $y -lt $image.Height; $y++) {
        for ($x = 0; $x -lt $image.Width; $x++) {
            $key = $image.GetPixel($x, $y).ToArgb().ToString('X8')
            if ($palette.ContainsKey($key)) {
                $image.SetPixel($x, $y, [System.Drawing.ColorTranslator]::FromHtml($palette[$key]))
            }
        }
    }

    $cover = [System.Drawing.ColorTranslator]::FromHtml('#245141')
    for ($y = 9; $y -le 21; $y++) {
        for ($x = 11; $x -le 21; $x++) { $image.SetPixel($x, $y, $cover) }
    }
    $emblem = @(
        '.....H.....'
        '....HHL....'
        '....HLR....'
        '....HLR..H.'
        '.H..HLR.HLR'
        'HHL.HLR.HLR'
        'HLR.HLR.HLR'
        'HLR.HLR.HLR'
        '.RR.LRR.LR.'
    )
    $ink = @{
        H = [System.Drawing.ColorTranslator]::FromHtml('#FFE69A')
        L = [System.Drawing.ColorTranslator]::FromHtml('#E2B84A')
        R = [System.Drawing.ColorTranslator]::FromHtml('#9F762A')
    }
    for ($y = 0; $y -lt $emblem.Count; $y++) {
        for ($x = 0; $x -lt $emblem[$y].Length; $x++) {
            $pixel = $emblem[$y][$x].ToString()
            if ($ink.ContainsKey($pixel)) { $image.SetPixel(11 + $x, 9 + $y, $ink[$pixel]) }
        }
    }
    $caption = [System.Drawing.ColorTranslator]::FromHtml('#96B8A4')
    for ($x = 14; $x -le 18; $x++) { $image.SetPixel($x, 20, $caption) }
    for ($x = 15; $x -le 17; $x++) { $image.SetPixel($x, 21, $caption) }
    $image.Save((Join-Path $directory 'crystal_research.png'), [System.Drawing.Imaging.ImageFormat]::Png)
}
finally { $image.Dispose() }
Write-Output 'Generated crystal cultivation book sprite (32x32, native pixels).'
