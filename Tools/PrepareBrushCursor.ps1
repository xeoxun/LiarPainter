Add-Type -AssemblyName System.Drawing
$src=[Drawing.Bitmap]::FromFile('C:\Users\PC\.codex\generated_images\01a1147a-e65e-72c1-bb63-a90241fe7082\exec-2b96761f-7d7e-496a-9fe1-a227e2b257e2.png')
$dst=[Drawing.Bitmap]::new(64,64)
$g=[Drawing.Graphics]::FromImage($dst)
try {
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $g.DrawImage($src,[Drawing.Rectangle]::new(0,0,64,64),0,0,$src.Width,$src.Height,[Drawing.GraphicsUnit]::Pixel)
 $path=Join-Path $PSScriptRoot '../Assets/Sprites/UI/Cursor_Brush_64.png'
 $dst.Save($path,[Drawing.Imaging.ImageFormat]::Png)
 Write-Output "Saved 64x64 PNG; corner alpha: $($dst.GetPixel(0,0).A)"
} finally { $g.Dispose(); $dst.Dispose(); $src.Dispose() }
