param([switch]$YourTurnOnly)

$rate = 44100
$folder = Join-Path $PSScriptRoot '../Assets/Audio/SFX'
[IO.Directory]::CreateDirectory($folder) | Out-Null

function Write-Cue([string]$name, [double]$duration, $notes, [double]$peakLevel) {
    $count = [int]($rate*$duration)
    $samples = [double[]]::new($count)
    foreach ($note in $notes) {
        $offset = [int]($note[0]*$rate)
        $frequency = $note[1]
        $gain = $note[2]
        $decay = $note[3]
        for ($i=$offset; $i -lt $count; $i++) {
            $t = ($i-$offset)/$rate
            $attack = [Math]::Pow(1-[Math]::Exp(-$t/0.012),2)
            $tone = [Math]::Sin(2*[Math]::PI*$frequency*$t)*[Math]::Exp(-$t/$decay)
            $tone += 0.12*[Math]::Sin(2*[Math]::PI*$frequency*2.001*$t)*[Math]::Exp(-$t/($decay*0.65))
            $tone += 0.025*[Math]::Sin(2*[Math]::PI*$frequency*3.003*$t)*[Math]::Exp(-$t/($decay*0.45))
            $samples[$i] += $gain*$attack*$tone
        }
    }
    # Quiet, diffuse gallery reflections; no hard percussion transient.
    $dry = $samples.Clone()
    foreach ($tap in @(@(0.037,0.08),@(0.061,0.065),@(0.089,0.055),@(0.127,0.04),@(0.173,0.028),@(0.229,0.018))) {
        $delay = [int]($tap[0]*$rate)
        for ($i=$delay; $i -lt $count; $i++) { $samples[$i] += $dry[$i-$delay]*$tap[1] }
    }
    $peak = 0.0
    for ($i=0; $i -lt $count; $i++) {
        $fade = [Math]::Min(1.0,($count-1-$i)/($rate*0.25))
        $samples[$i] *= $fade
        $peak = [Math]::Max($peak,[Math]::Abs($samples[$i]))
    }
    $path = Join-Path $folder $name
    $writer = [IO.BinaryWriter]::new([IO.File]::Create($path))
    try {
        $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write([int](36+$count*2))
        $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write([int]16)
        $writer.Write([int16]1); $writer.Write([int16]1); $writer.Write([int]$rate)
        $writer.Write([int]($rate*2)); $writer.Write([int16]2); $writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes('data')); $writer.Write([int]($count*2))
        foreach ($v in $samples) { $writer.Write([int16][Math]::Round($v/$peak*$peakLevel*32767)) }
    } finally { $writer.Dispose() }
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -ne (44+$count*2)) { throw "Invalid WAV length: $name" }
    Write-Output "$name | $duration sec | 44100 Hz, mono, 16-bit PCM | verified WAV length"
}

# Keep the previous audio outside Assets so it can be restored without duplicate imports.
$backup = Join-Path $PSScriptRoot ('../AudioBackups/MuseumCues_'+[DateTime]::Now.ToString('yyyyMMdd_HHmmssfff'))
[IO.Directory]::CreateDirectory($backup) | Out-Null
$names = @('Your_Turn_Museum.wav')
if (-not $YourTurnOnly) { $names += @('Game_Start_Museum.wav','Turn_Change_Museum.wav') }
foreach ($name in $names) {
    $existing = Join-Path $folder $name
    if (Test-Path -LiteralPath $existing) { Copy-Item -LiteralPath $existing -Destination (Join-Path $backup $name) }
}

# A gentle upward invitation, distinct from the lower turn-change cue.
Write-Cue 'Your_Turn_Museum.wav' 1.3 @(@(0.0,523.25,0.62,0.19),@(0.15,659.25,0.65,0.23),@(0.30,880.0,0.55,0.26),@(0.30,440.0,0.18,0.24)) 0.30
if ($YourTurnOnly) { return }
# Warm major-sixth voicing with a restrained, spacious resolution.
Write-Cue 'Game_Start_Museum.wav' 2.1 @(@(0.0,261.63,0.28,0.40),@(0.0,392.0,0.55,0.34),@(0.22,523.25,0.56,0.36),@(0.44,659.25,0.47,0.38),@(0.44,440.0,0.24,0.36)) 0.32
Write-Cue 'Turn_Change_Museum.wav' 0.95 @(@(0.0,392.0,0.65,0.17),@(0.14,523.25,0.46,0.19)) 0.24
