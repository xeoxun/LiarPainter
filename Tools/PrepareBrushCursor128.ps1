Add-Type -AssemblyName System.Drawing
$src=[Drawing.Bitmap]::FromFile('C:\Users\PC\.codex\generated_images\01a1147a-e65e-72c1-bb63-a90241fe7082\exec-2b96761f-7d7e-496a-9fe1-a227e2b257e2.png')
$minX=$src.Width; $minY=$src.Height; $maxX=-1; $maxY=-1
for($y=0;$y -lt $src.Height;$y++) {
 for($x=0;$x -lt $src.Width;$x++) {
  if($src.GetPixel($x,$y).A -ge 128) {
   $minX=[Math]::Min($minX,$x); $maxX=[Math]::Max($maxX,$x)
   $minY=[Math]::Min($minY,$y); $maxY=[Math]::Max($maxY,$y)
  }
 }
}
$width=$maxX-$minX+1; $height=$maxY-$minY+1
$scale=[Math]::Min(128.0/$width,128.0/$height)
$w=[int][Math]::Round($width*$scale); $h=[int][Math]::Round($height*$scale)
$dst=[Drawing.Bitmap]::new(128,128)
$g=[Drawing.Graphics]::FromImage($dst)
try {
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $rect=[Drawing.Rectangle]::new([int][Math]::Floor((128-$w)/2),[int][Math]::Floor((128-$h)/2),$w,$h)
 $g.DrawImage($src,$rect,$minX,$minY,$width,$height,[Drawing.GraphicsUnit]::Pixel)
 $path=Join-Path $PSScriptRoot '../Assets/Sprites/UI/Cursor_Brush_128_Full.png'
 $dst.Save($path,[Drawing.Imaging.ImageFormat]::Png)
 Write-Output "Saved 128x128 PNG; brush fitted to ${w}x${h}; transparent corner alpha=$($dst.GetPixel(127,0).A)"
} finally { $g.Dispose(); $dst.Dispose(); $src.Dispose() }
