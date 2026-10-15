Add-Type -AssemblyName System.Drawing
$src=[Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot '../Assets/Sprites/UI/Cursor_Brush_128_Full.png'))
$dst=[Drawing.Bitmap]::new(32,32)
$g=[Drawing.Graphics]::FromImage($dst)
$path=Join-Path $PSScriptRoot '../Assets/Sprites/UI/Cursor_Brush_32_Full.png'
try {
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $g.DrawImage($src,[Drawing.Rectangle]::new(0,0,32,32),0,0,$src.Width,$src.Height,[Drawing.GraphicsUnit]::Pixel)
 $dst.Save($path,[Drawing.Imaging.ImageFormat]::Png)
} finally { $g.Dispose(); $dst.Dispose(); $src.Dispose() }
$check=[Drawing.Bitmap]::FromFile($path)
try {
 if($check.Width -ne 32 -or $check.Height -ne 32) { throw 'Incorrect dimensions' }
 Write-Output "Verified 32x32 PNG; transparent corner alpha=$($check.GetPixel(31,0).A)"
} finally { $check.Dispose() }
