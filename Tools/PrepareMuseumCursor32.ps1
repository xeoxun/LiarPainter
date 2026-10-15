Add-Type -AssemblyName System.Drawing
$src=[Drawing.Bitmap]::FromFile('C:\Users\PC\.codex\generated_images\01a1147a-e65e-72c1-bb63-a90241fe7082\exec-0c263c3c-bf53-4677-9494-9599d8a0fe7c.png')
$minX=$src.Width; $minY=$src.Height; $maxX=-1; $maxY=-1
for($y=0;$y -lt $src.Height;$y++){for($x=0;$x -lt $src.Width;$x++){if($src.GetPixel($x,$y).A -ge 128){$minX=[Math]::Min($minX,$x);$minY=[Math]::Min($minY,$y);$maxX=[Math]::Max($maxX,$x);$maxY=[Math]::Max($maxY,$y)}}}
$width=$maxX-$minX+1;$height=$maxY-$minY+1
$scale=[Math]::Min(30.0/$width,30.0/$height)
$w=[int][Math]::Round($width*$scale);$h=[int][Math]::Round($height*$scale)
$dst=[Drawing.Bitmap]::new(32,32)
$g=[Drawing.Graphics]::FromImage($dst)
try {
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
 $g.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
 $g.DrawImage($src,[Drawing.Rectangle]::new(1,1,$w,$h),$minX,$minY,$width,$height,[Drawing.GraphicsUnit]::Pixel)
 $dst.Save((Join-Path $PSScriptRoot '../Assets/Sprites/UI/Cursor_Museum_Ivory_32.png'),[Drawing.Imaging.ImageFormat]::Png)
 Write-Output "Verified $($dst.Width)x$($dst.Height); corner alpha=$($dst.GetPixel(31,0).A)"
} finally {$g.Dispose();$dst.Dispose();$src.Dispose()}
