Add-Type -AssemblyName System.IO
$rate = 44100
$count = 22050
$rng = [Random]::new(72)
$samples = [double[]]::new($count)
$smooth = 0.0
$peak = 0.0
for ($i=0; $i -lt $count; $i++) {
    $t=$i/$rate
    $smooth=0.72*$smooth+0.28*($rng.NextDouble()*2-1)
    $attack=1-[Math]::Exp(-$t/0.0008)
    $wood=0.55*[Math]::Sin(2*[Math]::PI*620*$t)*[Math]::Exp(-$t/0.018)+0.23*[Math]::Sin(2*[Math]::PI*1030*$t)*[Math]::Exp(-$t/0.012)
    $brass=0.075*[Math]::Sin(2*[Math]::PI*2350*$t)*[Math]::Exp(-$t/0.038)+0.035*[Math]::Sin(2*[Math]::PI*3260*$t)*[Math]::Exp(-$t/0.027)
    $v=$attack*($wood+$brass+0.42*$smooth*[Math]::Exp(-$t/0.008))
    $samples[$i]=$v
    $peak=[Math]::Max($peak,[Math]::Abs($v))
}
$out=Join-Path $PSScriptRoot '../Assets/Audio/SFX/UI_Museum_Click.wav'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($out)) | Out-Null
$writer=[IO.BinaryWriter]::new([IO.File]::Create($out))
try {
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36+$count*2))
    $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16)
    $writer.Write([int16]1); $writer.Write([int16]1); $writer.Write([int]$rate)
    $writer.Write([int]($rate*2)); $writer.Write([int16]2); $writer.Write([int16]16)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]($count*2))
    foreach($v in $samples){ $writer.Write([int16][Math]::Round($v/$peak*14636)) }
} finally { $writer.Dispose() }
Write-Output "Created: $out | 0.5s | mono PCM 16-bit | 44100 Hz | peak -7 dBFS"
