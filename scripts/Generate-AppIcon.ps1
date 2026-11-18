Add-Type -AssemblyName System.Drawing
$assetPath = Join-Path $PSScriptRoot '../src/SurveillanceAI.Desktop/Assets'
New-Item -ItemType Directory -Force $assetPath | Out-Null
$frames = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
 $bitmap = [Drawing.Bitmap]::new($size,$size)
 $graphics = [Drawing.Graphics]::FromImage($bitmap)
 $graphics.SmoothingMode = 'AntiAlias'
 $graphics.Clear([Drawing.Color]::FromArgb(12,20,35))
 $points = [Drawing.PointF[]]@([Drawing.PointF]::new($size*.5,$size*.08),[Drawing.PointF]::new($size*.92,$size*.5),[Drawing.PointF]::new($size*.5,$size*.92),[Drawing.PointF]::new($size*.08,$size*.5))
 $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(27,219,199))
 $graphics.FillPolygon($brush,$points)
 $graphics.FillEllipse([Drawing.Brushes]::White,$size*.26,$size*.34,$size*.48,$size*.32)
 $graphics.FillEllipse([Drawing.Brushes]::MidnightBlue,$size*.40,$size*.40,$size*.20,$size*.20)
 $stream = [IO.MemoryStream]::new()
 $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
 $frames += ,$stream.ToArray()
 $stream.Dispose(); $graphics.Dispose(); $brush.Dispose(); $bitmap.Dispose()
}
$file = [IO.File]::Create((Join-Path $assetPath 'App.ico'))
$writer = [IO.BinaryWriter]::new($file)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
$index = 0
foreach ($size in @(16,24,32,48,64,128,256)) {
 $dimension = if ($size -eq 256) {0} else {$size}
 $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
 $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$index].Length);$writer.Write([uint32]$offset)
 $offset += $frames[$index].Length;$index++
}
foreach ($frame in $frames) {$writer.Write([byte[]]$frame)}
$writer.Dispose()
