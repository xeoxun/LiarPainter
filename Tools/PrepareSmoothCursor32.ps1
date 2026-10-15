Add-Type -AssemblyName System.Drawing
$src=[Drawing.Bitmap]::FromFile('C:\Users\PC\.codex\generated_images\01a1147a-e65e-72c1-bb63-a90241fe7082\exec-bd0e8b17-0cf3-438a-bd48-571f5891e83f.png')
$dst=[Drawing.Bitmap]::new(32,32)
$g=[Drawing.Graphics]::FromImage($dst)
try {
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
 $g.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
 $g.DrawImage($src,[Drawing.Rectangle]::new(0,0,32,32),0,0,$src.Width,$src.Height,[Drawing.GraphicsUnit]::Pixel)
 $path=Join-Path $PSScriptRoot '../Assets/Sprites/UI/Cursor_GoldArrow_Smooth_32.png'
 $dst.Save($path,[Drawing.Imaging.ImageFormat]::Png)
 Write-Output "Saved $($dst.Width)x$($dst.Height); corner alpha=$($dst.GetPixel(31,0).A)"
} finally { $g.Dispose(); $dst.Dispose(); $src.Dispose() }
