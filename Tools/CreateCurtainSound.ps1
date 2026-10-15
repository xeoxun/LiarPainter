$rate = 44100
$duration = 1.8
$count = [int]($rate*$duration)
$samples = [double[]]::new($count)
$rng = [Random]::new(194)
$low = 0.0
$mid = 0.0
$soft = 0.0
$peak = 0.0
for ($i=0; $i -lt $count; $i++) {
    $t=$i/$rate
    $noise=$rng.NextDouble()*2-1
    $low += 0.024*($noise-$low)
    $mid += 0.21*($noise-$mid)
    $soft += 0.075*($noise-$soft)
    # Slow heavy fabric sweep with an irregular friction texture.
    $motion=(1-[Math]::Exp(-$t/0.025))*[Math]::Pow([Math]::Max(0,1-$t/$duration),0.7)
    $motion *= [Math]::Min(1,($duration-$t)/0.18)
    $texture=0.88+0.07*[Math]::Sin(2*[Math]::PI*3.3*$t)+0.04*[Math]::Sin(2*[Math]::PI*7.7*$t)
    $value=$motion*$texture*(1.5*$low+0.26*($mid-$soft))
    $samples[$i]=$value
    $peak=[Math]::Max($peak,[Math]::Abs($value))
}
$out=Join-Path $PSScriptRoot '../Assets/Audio/SFX/Curtain_Open_Museum.wav'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($out)) | Out-Null
$writer=[IO.BinaryWriter]::new([IO.File]::Create($out))
try {
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36+$count*2))
    $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16)
    $writer.Write([int16]1); $writer.Write([int16]1); $writer.Write([int]$rate)
    $writer.Write([int]($rate*2)); $writer.Write([int16]2); $writer.Write([int16]16)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]($count*2))
    foreach ($v in $samples) { $writer.Write([int16][Math]::Round($v/$peak*0.34*32767)) }
} finally { $writer.Dispose() }
if ((Get-Item -LiteralPath $out).Length -ne 44+$count*2) { throw 'Invalid WAV size' }
Write-Output 'Curtain_Open_Museum.wav | 1.8s | 44100 Hz mono PCM16 | WAV size verified'
