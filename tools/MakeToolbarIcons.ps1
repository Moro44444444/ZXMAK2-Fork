param(
    [Parameter(Mandatory = $true)][string]$OpenIcon,
    [Parameter(Mandatory = $true)][string]$SaveIcon,
    [Parameter(Mandatory = $true)][string]$ResetIcon,
    [Parameter(Mandatory = $true)][string]$ResourceDirectory
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function Convert-ToolbarIcon {
    param([string]$SourcePath, [string]$DestinationPath)

    $source = [System.Drawing.Bitmap]::FromFile($SourcePath)
    try {
        $width = $source.Width
        $height = $source.Height
        $background = New-Object 'bool[]' ($width * $height)
        $visited = New-Object 'bool[]' ($width * $height)
        $queue = New-Object 'System.Collections.Generic.Queue[int]'

        # Remove only pale, near-neutral pixels connected to the outside.
        # White details enclosed within the icon (paper and disk label) stay.
        for ($y = 0; $y -lt $height; $y++) {
            for ($x = 0; $x -lt $width; $x++) {
                if ($x -ne 0 -and $x -ne ($width - 1) -and
                    $y -ne 0 -and $y -ne ($height - 1)) { continue }
                $index = $y * $width + $x
                $pixel = $source.GetPixel($x, $y)
                $low = [Math]::Min($pixel.R, [Math]::Min($pixel.G, $pixel.B))
                $high = [Math]::Max($pixel.R, [Math]::Max($pixel.G, $pixel.B))
                if ($low -ge 190 -and $high - $low -le 25) {
                    $visited[$index] = $true
                    $queue.Enqueue($index)
                }
            }
        }
        while ($queue.Count -gt 0) {
            $index = $queue.Dequeue()
            $background[$index] = $true
            $x = $index % $width
            $y = [int][Math]::Floor($index / $width)
            foreach ($next in @(($index - 1), ($index + 1),
                    ($index - $width), ($index + $width))) {
                if ($next -lt 0 -or $next -ge $background.Length -or
                    ($next -eq ($index - 1) -and $x -eq 0) -or
                    ($next -eq ($index + 1) -and $x -eq ($width - 1)) -or
                    $visited[$next]) { continue }
                $visited[$next] = $true
                $nx = $next % $width
                $ny = [int][Math]::Floor($next / $width)
                $pixel = $source.GetPixel($nx, $ny)
                $low = [Math]::Min($pixel.R, [Math]::Min($pixel.G, $pixel.B))
                $high = [Math]::Max($pixel.R, [Math]::Max($pixel.G, $pixel.B))
                if ($low -ge 190 -and $high - $low -le 25) {
                    $queue.Enqueue($next)
                }
            }
        }

        $clean = [System.Drawing.Bitmap]::new(
            $width, $height,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $minX = $width
            $minY = $height
            $maxX = -1
            $maxY = -1
            for ($y = 0; $y -lt $height; $y++) {
                for ($x = 0; $x -lt $width; $x++) {
                    if ($background[$y * $width + $x]) { continue }
                    $pixel = $source.GetPixel($x, $y)
                    $clean.SetPixel($x, $y, $pixel)
                    if ($x -lt $minX) { $minX = $x }
                    if ($y -lt $minY) { $minY = $y }
                    if ($x -gt $maxX) { $maxX = $x }
                    if ($y -gt $maxY) { $maxY = $y }
                }
            }
            if ($maxX -lt $minX) { throw "Icon is empty: $SourcePath" }

            $output = [System.Drawing.Bitmap]::new(
                32, 32,
                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $graphics = [System.Drawing.Graphics]::FromImage($output)
                try {
                    $graphics.Clear([System.Drawing.Color]::Transparent)
                    $graphics.InterpolationMode =
                        [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $graphics.PixelOffsetMode =
                        [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                    $graphics.CompositingQuality =
                        [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                    $cropWidth = $maxX - $minX + 1
                    $cropHeight = $maxY - $minY + 1
                    $scale = 30.0 / [Math]::Max($cropWidth, $cropHeight)
                    $targetWidth = [float]($cropWidth * $scale)
                    $targetHeight = [float]($cropHeight * $scale)
                    $target = [System.Drawing.RectangleF]::new(
                        [float]((32 - $targetWidth) / 2),
                        [float]((32 - $targetHeight) / 2),
                        $targetWidth, $targetHeight)
                    $sourceRect = [System.Drawing.RectangleF]::new(
                        [float]$minX, [float]$minY,
                        [float]$cropWidth, [float]$cropHeight)
                    $graphics.DrawImage($clean, $target, $sourceRect,
                        [System.Drawing.GraphicsUnit]::Pixel)
                }
                finally { $graphics.Dispose() }
                $output.Save($DestinationPath,
                    [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally { $output.Dispose() }
        }
        finally { $clean.Dispose() }
    }
    finally { $source.Dispose() }
}

Convert-ToolbarIcon $OpenIcon (Join-Path $ResourceDirectory 'EmuFileOpen_32x32.png')
Convert-ToolbarIcon $SaveIcon (Join-Path $ResourceDirectory 'EmuFileSave_32x32.png')
Convert-ToolbarIcon $ResetIcon (Join-Path $ResourceDirectory 'EmuWarmReset_32x32.png')
