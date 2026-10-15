param([int]$Size = 512)
Add-Type -AssemblyName System.Drawing
$source = Join-Path $PSScriptRoot '../Assets/Sprites/UI/PhonyPainter_AppIcon.png'
$destination = Join-Path $PSScriptRoot "../Assets/Sprites/UI/PhonyPainter_AppIcon_$Size.png"
$original = [System.Drawing.Bitmap]::FromFile($source)
$resized = [System.Drawing.Bitmap]::new($Size,$Size)
$graphics = [System.Drawing.Graphics]::FromImage($resized)
try {
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.DrawImage($original,[System.Drawing.Rectangle]::new(0,0,$Size,$Size),0,0,$original.Width,$original.Height,[System.Drawing.GraphicsUnit]::Pixel)
    $resized.Save($destination,[System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose()
    $resized.Dispose()
    $original.Dispose()
}
$check = [System.Drawing.Bitmap]::FromFile($destination)
try {
    if ($check.Width -ne $Size -or $check.Height -ne $Size) { throw 'Incorrect icon dimensions' }
    Write-Output "$destination | verified ${Size}x${Size} PNG"
} finally { $check.Dispose() }
