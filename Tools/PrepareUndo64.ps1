Add-Type -AssemblyName System.Drawing
$src=[Drawing.Bitmap]::FromFile('C:\Users\PC\.codex\generated_images\01a1147a-e65e-72c1-bb63-a90241fe7082\exec-8d7b6cb6-6876-4ccd-9c29-b145c154f7ab.png')
$dst=[Drawing.Bitmap]::new(64,64)
$g=[Drawing.Graphics]::FromImage($dst)
try {
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $g.DrawImage($src,[Drawing.Rectangle]::new(0,0,64,64),0,0,$src.Width,$src.Height,[Drawing.GraphicsUnit]::Pixel)
 $dst.Save((Join-Path $PSScriptRoot '../Assets/Sprites/UI/Icon_Undo_64.png'),[Drawing.Imaging.ImageFormat]::Png)
 Write-Output "Verified $($dst.Width)x$($dst.Height); corner alpha=$($dst.GetPixel(0,0).A)"
} finally {$g.Dispose();$dst.Dispose();$src.Dispose()}
