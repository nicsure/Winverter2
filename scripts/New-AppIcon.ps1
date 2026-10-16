# Rebuild the transparent, multi-resolution crescent icon using Windows drawing APIs.
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path $PSScriptRoot '..\Winverter2\Assets'
[System.IO.Directory]::CreateDirectory($assetDirectory) | Out-Null
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $large = [System.Drawing.Bitmap]::new($size * 4, $size * 4)
    $graphics = [System.Drawing.Graphics]::FromImage($large)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($size * 4 / 256.0, $size * 4 / 256.0)
    $moon = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 243, 204, 105))
    $clear = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::Transparent)
    $graphics.FillEllipse($moon, 26, 22, 204, 212)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.FillEllipse($clear, 96, -2, 188, 190)
    $graphics.Dispose()
    $moon.Dispose()
    $clear.Dispose()
    $small = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($small)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($large, 0, 0, $size, $size)
    $graphics.Dispose()
    $large.Dispose()
    $stream = [System.IO.MemoryStream]::new()
    $small.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,$stream.ToArray()
    if ($size -eq 256) { $small.Save((Join-Path $assetDirectory 'Winverter.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $small.Dispose()
    $stream.Dispose()
}
$output = [System.IO.File]::Create((Join-Path $assetDirectory 'Winverter.ico'))
$writer = [System.IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$index].Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image) }
}
finally { $writer.Dispose() }
