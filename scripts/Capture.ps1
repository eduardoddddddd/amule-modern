param([string]$OutDirectory)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutDirectory)) { $OutDirectory = Join-Path $repoPath 'artifacts\captures' }
$exe = Join-Path $repoPath 'artifacts\app\AmuleModern.exe'
if (-not (Test-Path -LiteralPath $exe)) { & (Join-Path $PSScriptRoot 'Build.ps1') -Publish }
$captureProfile = Join-Path $repoPath '.local\capture'
New-Item -ItemType Directory -Path $OutDirectory -Force | Out-Null
$cases = @(
    @{ Name = 'main'; Args = @() },
    @{ Name = 'search'; Args = @('--search') },
    @{ Name = 'servers'; Args = @('--servers') },
    @{ Name = 'shared'; Args = @('--shared') },
    @{ Name = 'settings'; Args = @('--settings') }
)
foreach ($case in $cases) {
    if (Test-Path -LiteralPath $captureProfile) { Remove-Item -LiteralPath $captureProfile -Recurse -Force }
    $png = Join-Path $OutDirectory ("0.9-{0}.png" -f $case.Name)
    $argList = @('--capture', $png, '--exercise-ui') + $case.Args
    $proc = Start-Process -FilePath $exe -WorkingDirectory $repoPath -ArgumentList $argList -Wait -PassThru
    if ($proc.ExitCode -ne 0) { throw "Captura $($case.Name) exit $($proc.ExitCode)." }
    Write-Output "OK $($case.Name) exit $($proc.ExitCode)"
}
