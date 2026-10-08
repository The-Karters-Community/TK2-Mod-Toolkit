param(
    [string]$Source = (Join-Path $PSScriptRoot '../assets/TheKartersLogoModified.png'),
    [string]$Output = (Join-Path $PSScriptRoot '../assets/TheKartersLogoModified.ico')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# Export the supplied artwork to native Windows icon sizes. No AI redraw or
# content changes: every frame is rendered directly from the original PNG.
$art = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Source).Path)
$frames = [System.Collections.Generic.List[byte[]]]::new()
$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
try {
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $canvas = [System.Drawing.Graphics]::FromImage($bitmap)
        $buffer = [System.IO.MemoryStream]::new()
        $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
        try {
            $canvas.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $canvas.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $canvas.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $canvas.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $canvas.DrawImage($art, [System.Drawing.Rectangle]::new(0, 0, $size, $size), 0, 0, $art.Width, $art.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
            $bitmap.Save($buffer, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($buffer.ToArray())
        } finally { $attributes.Dispose(); $buffer.Dispose(); $canvas.Dispose(); $bitmap.Dispose() }
    }
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
        [System.IO.File]::WriteAllBytes([System.IO.Path]::GetFullPath($Output), $stream.ToArray())
    } finally { $writer.Dispose(); $stream.Dispose() }
} finally { $art.Dispose() }
Write-Output "Exported icon sizes: $($sizes -join ', ') pixels -> $Output"
