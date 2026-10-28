Add-Type -AssemblyName System.Drawing
$src=[Drawing.Bitmap]::FromFile('C:\Users\PC\.codex\generated_images\01a1147a-e65e-72c1-bb63-a90241fe7082\exec-b4758317-fc6d-430f-9b0c-82c65eb0e3fb.png')
$dst=[Drawing.Bitmap]::new(400,200)
$g=[Drawing.Graphics]::FromImage($dst)
try {
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $g.DrawImage($src,[Drawing.Rectangle]::new(0,0,400,200),0,0,$src.Width,$src.Height,[Drawing.GraphicsUnit]::Pixel)
 $dst.Save((Join-Path $PSScriptRoot '../Assets/Sprites/UI/GameScene/Paint_Splat_Red_Wide_400x200.png'),[Drawing.Imaging.ImageFormat]::Png)
 Write-Output "Verified $($dst.Width)x$($dst.Height); corner alpha=$($dst.GetPixel(0,0).A)"
} finally {$g.Dispose();$dst.Dispose();$src.Dispose()}
