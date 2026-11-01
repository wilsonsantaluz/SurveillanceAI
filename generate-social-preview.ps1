Add-Type -AssemblyName System.Drawing

$outputDirectory = Join-Path $PSScriptRoot 'assets'
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$outputPath = Join-Path $outputDirectory 'social-preview.png'

$width = 1280
$height = 640
$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

function Get-Color([string]$hex) {
    return [System.Drawing.ColorTranslator]::FromHtml($hex)
}

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $w - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $w - $diameter, $y + $h - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $h - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Fill-Rounded([System.Drawing.Graphics]$g, [System.Drawing.Brush]$brush, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-RoundedPath $x $y $w $h $r
    $g.FillPath($brush, $path)
    $path.Dispose()
}

function Draw-Rounded([System.Drawing.Graphics]$g, [System.Drawing.Pen]$pen, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-RoundedPath $x $y $w $h $r
    $g.DrawPath($pen, $path)
    $path.Dispose()
}

function Draw-Text([System.Drawing.Graphics]$g, [string]$text, [string]$family, [float]$size, [string]$color, [float]$x, [float]$y, [System.Drawing.FontStyle]$style = [System.Drawing.FontStyle]::Regular) {
    $font = New-Object System.Drawing.Font($family, $size, $style, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush((Get-Color $color))
    $g.DrawString($text, $font, $brush, $x, $y)
    $font.Dispose()
    $brush.Dispose()
}

try {
    $background = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.Rectangle]::new(0, 0, $width, $height),
        (Get-Color '#0a1519'), (Get-Color '#14272b'), 18
    )
    $graphics.FillRectangle($background, 0, 0, $width, $height)
    $background.Dispose()

    $gridPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(15, 145, 190, 166), 1)
    for ($x = 20; $x -lt $width; $x += 48) { $graphics.DrawLine($gridPen, $x, 0, $x, $height) }
    for ($y = 24; $y -lt $height; $y += 48) { $graphics.DrawLine($gridPen, 0, $y, $width, $y) }
    $gridPen.Dispose()

    $accentBrush = New-Object System.Drawing.SolidBrush((Get-Color '#58e0b0'))
    Fill-Rounded $graphics $accentBrush 65 67 5 31 2
    $accentBrush.Dispose()
    Draw-Text $graphics 'REAL-TIME VIDEO ANALYTICS' 'Bahnschrift' 15 '#91a9a7' 83 70 ([System.Drawing.FontStyle]::Bold)

    Draw-Text $graphics 'Surveillance' 'Bahnschrift SemiBold' 49 '#f3f6ef' 64 142 ([System.Drawing.FontStyle]::Bold)
    Draw-Text $graphics 'AI' 'Bahnschrift SemiBold' 49 '#58e0b0' 401 142 ([System.Drawing.FontStyle]::Bold)
    Draw-Text $graphics 'Intelligent video' 'Bahnschrift SemiBold' 28 '#f3f6ef' 67 226 ([System.Drawing.FontStyle]::Bold)
    Draw-Text $graphics 'beyond object detection.' 'Bahnschrift SemiBold' 28 '#f3f6ef' 67 263 ([System.Drawing.FontStyle]::Bold)
    Draw-Text $graphics 'Analyze proximity and movement in video' 'Bahnschrift' 17 '#a9bab5' 68 324
    Draw-Text $graphics 'to flag interactions for human review.' 'Bahnschrift' 17 '#a9bab5' 68 350

    $pillBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(235, 28, 48, 50))
    $pillPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(110, 93, 177, 151), 1)
    $pillText = New-Object System.Drawing.SolidBrush((Get-Color '#c8ded4'))
    $pillFont = New-Object System.Drawing.Font('Bahnschrift', 13, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $pillFormat = New-Object System.Drawing.StringFormat
    $pillFormat.Alignment = [System.Drawing.StringAlignment]::Center
    $pillFormat.LineAlignment = [System.Drawing.StringAlignment]::Center
    $pillLabels = @(@{ Text = '.NET'; X = 68; W = 73 }, @{ Text = 'YOLOv8'; X = 151; W = 88 }, @{ Text = 'ONNX'; X = 249; W = 78 }, @{ Text = 'OpenCV'; X = 337; W = 91 })
    foreach ($pill in $pillLabels) {
        Fill-Rounded $graphics $pillBrush $pill.X 409 $pill.W 34 8
        Draw-Rounded $graphics $pillPen $pill.X 409 $pill.W 34 8
        $graphics.DrawString($pill.Text, $pillFont, $pillText, [System.Drawing.RectangleF]::new($pill.X, 409, $pill.W, 34), $pillFormat)
    }
    $pillBrush.Dispose(); $pillPen.Dispose(); $pillText.Dispose(); $pillFont.Dispose(); $pillFormat.Dispose()

    $frameBrush = New-Object System.Drawing.SolidBrush((Get-Color '#101e20'))
    $framePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 95, 137, 122), 1.4)
    Fill-Rounded $graphics $frameBrush 548 72 668 412 17
    Draw-Rounded $graphics $framePen 548 72 668 412 17
    $frameBrush.Dispose(); $framePen.Dispose()

    $scenePath = New-RoundedPath 561 85 642 386 10
    $sceneBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.Rectangle]::new(561, 85, 642, 386),
        (Get-Color '#263b39'), (Get-Color '#172625'), 90
    )
    $graphics.FillPath($sceneBrush, $scenePath)
    $sceneBrush.Dispose()

    $ceilingPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90, 173, 194, 164), 1)
    $graphics.DrawLine($ceilingPen, 561, 171, 1203, 171)
    $graphics.DrawLine($ceilingPen, 561, 171, 755, 253)
    $graphics.DrawLine($ceilingPen, 1203, 171, 1008, 253)
    $graphics.DrawLine($ceilingPen, 561, 471, 755, 253)
    $graphics.DrawLine($ceilingPen, 1203, 471, 1008, 253)
    $graphics.DrawLine($ceilingPen, 755, 253, 1008, 253)
    $graphics.DrawLine($ceilingPen, 561, 350, 1203, 350)
    $graphics.DrawLine($ceilingPen, 561, 410, 1203, 410)
    $ceilingPen.Dispose()

    $lampBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(28, 213, 230, 177))
    Fill-Rounded $graphics $lampBrush 843 105 74 9 4
    Fill-Rounded $graphics $lampBrush 1046 105 74 9 4
    $lampBrush.Dispose()

    $hudBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 12, 27, 28))
    Fill-Rounded $graphics $hudBrush 582 103 170 29 8
    Draw-Text $graphics 'SAMPLE ANALYSIS' 'Bahnschrift' 12 '#bed1c8' 595 110 ([System.Drawing.FontStyle]::Bold)
    Fill-Rounded $graphics $hudBrush 1059 103 122 29 8
    Draw-Text $graphics 'LIVE' 'Bahnschrift' 12 '#58e0b0' 1092 110 ([System.Drawing.FontStyle]::Bold)
    $hudBrush.Dispose()

    $personBrush = New-Object System.Drawing.SolidBrush((Get-Color '#a8b8a7'))
    $personDark = New-Object System.Drawing.SolidBrush((Get-Color '#657e75'))
    $graphics.FillEllipse($personBrush, 785, 216, 37, 43)
    $torsoOne = [System.Drawing.Point[]]@([System.Drawing.Point]::new(780, 260), [System.Drawing.Point]::new(827, 260), [System.Drawing.Point]::new(844, 361), [System.Drawing.Point]::new(768, 361))
    $graphics.FillPolygon($personBrush, $torsoOne)
    $graphics.FillEllipse($personDark, 978, 228, 35, 40)
    $torsoTwo = [System.Drawing.Point[]]@([System.Drawing.Point]::new(973, 269), [System.Drawing.Point]::new(1018, 269), [System.Drawing.Point]::new(1033, 361), [System.Drawing.Point]::new(959, 361))
    $graphics.FillPolygon($personDark, $torsoTwo)
    $personBrush.Dispose(); $personDark.Dispose()

    $boxOnePen = New-Object System.Drawing.Pen((Get-Color '#58e0b0'), 2)
    $boxTwoPen = New-Object System.Drawing.Pen((Get-Color '#f2c56b'), 2)
    $graphics.DrawRectangle($boxOnePen, 754, 205, 103, 173)
    $graphics.DrawRectangle($boxTwoPen, 945, 216, 102, 162)
    $boxOnePen.Dispose(); $boxTwoPen.Dispose()

    $distancePen = New-Object System.Drawing.Pen((Get-Color '#f2c56b'), 2)
    $distancePen.DashPattern = [single[]]@(5, 5)
    $graphics.DrawLine($distancePen, 851, 287, 947, 287)
    $distancePen.Dispose()
    $measureBrush = New-Object System.Drawing.SolidBrush((Get-Color '#f2c56b'))
    Fill-Rounded $graphics $measureBrush 865 263 68 28 7
    $measureBrush.Dispose()
    Draw-Text $graphics '1.4 m' 'Bahnschrift' 14 '#18231e' 879 268 ([System.Drawing.FontStyle]::Bold)

    $alertBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(236, 54, 39, 31))
    $alertPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(230, 242, 197, 107), 1)
    Fill-Rounded $graphics $alertBrush 866 397 222 38 9
    Draw-Rounded $graphics $alertPen 866 397 222 38 9
    Draw-Text $graphics '!' 'Bahnschrift' 17 '#f2c56b' 880 403 ([System.Drawing.FontStyle]::Bold)
    Draw-Text $graphics 'Proximity under review' 'Bahnschrift' 13 '#f5ddaa' 902 406 ([System.Drawing.FontStyle]::Bold)
    $alertBrush.Dispose(); $alertPen.Dispose()
    $scenePath.Dispose()

    $dividerPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(65, 158, 190, 168), 1)
    $graphics.DrawLine($dividerPen, 65, 520, 1216, 520)
    $dividerPen.Dispose()
    Draw-Text $graphics 'FROM VIDEO TO CONTEXT' 'Bahnschrift' 12 '#91a9a7' 68 545 ([System.Drawing.FontStyle]::Bold)

    $steps = @(
        @{ N = '01'; T = 'Capture'; X = 328 },
        @{ N = '02'; T = 'Detection'; X = 533 },
        @{ N = '03'; T = 'Proximity'; X = 738 },
        @{ N = '04'; T = 'Visual alert'; X = 967 }
    )
    $stepFont = New-Object System.Drawing.Font('Bahnschrift', 15, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $stepBrush = New-Object System.Drawing.SolidBrush((Get-Color '#e6eee8'))
    $arrowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(110, 111, 157, 139), 1.5)
    foreach ($step in $steps) {
        Draw-Text $graphics $step.N 'Bahnschrift' 12 '#58e0b0' $step.X 577 ([System.Drawing.FontStyle]::Bold)
        $graphics.DrawString($step.T, $stepFont, $stepBrush, $step.X + 28, 575)
    }
    foreach ($x in @(492, 697, 926)) {
        $graphics.DrawLine($arrowPen, $x, 584, $x + 20, 584)
        $graphics.DrawLine($arrowPen, $x + 15, 579, $x + 20, 584)
        $graphics.DrawLine($arrowPen, $x + 15, 589, $x + 20, 584)
    }
    $stepFont.Dispose(); $stepBrush.Dispose(); $arrowPen.Dispose()

    $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Output "Created $outputPath ($width x $height)"