$ErrorActionPreference = 'Stop'
$references = @(Get-ChildItem (Join-Path $PSHOME 'ref') -Filter '*.dll' | Select-Object -ExpandProperty FullName) + @([Newtonsoft.Json.Linq.JObject].Assembly.Location)
Add-Type -Path (Join-Path $PSScriptRoot 'AddonFiles.cs') -ReferencedAssemblies $references -CompilerOptions '/nowarn:1701'
$testRoot = Join-Path $PSScriptRoot ('.test-' + [guid]::NewGuid().ToString('N'))
function Assert-True($condition, $message) { if (-not $condition) { throw $message } }
function Assert-Rejected([scriptblock]$action) {
    $rejected = $false
    try { & $action } catch { $rejected = $true }
    Assert-True $rejected 'Expected operation to be rejected'
}
try {
    $active = Join-Path $testRoot 'AddOns'
    $disabled = Join-Path $testRoot 'AddOns_Disabled'
    $sample = Join-Path $active 'Sample'
    New-Item -ItemType Directory -Path $sample -Force | Out-Null
    $file = Join-Path $sample 'config.json'
    [IO.File]::WriteAllText($file, '{"enabled":true}')
    [SephiriaUnifiedModPanel.AddonFiles]::Move($sample, $active, $disabled)
    Assert-True (Test-Path (Join-Path $disabled 'Sample/config.json')) 'Move lost configuration'
    New-Item -ItemType Directory -Path $sample -Force | Out-Null
    Assert-Rejected { [SephiriaUnifiedModPanel.AddonFiles]::Move((Join-Path $disabled 'Sample'), $disabled, $active) }
    Assert-True (Test-Path (Join-Path $disabled 'Sample/config.json')) 'Collision changed source'
    Assert-Rejected { [SephiriaUnifiedModPanel.AddonFiles]::Move($sample, $disabled, $active) }
    Assert-True (@(Get-ChildItem -LiteralPath $testRoot -Recurse -Filter '*.tmp').Count -eq 0) 'Temporary files remain'
    Write-Output 'PASS: AddOn directory boundary, move with configuration preserved, same-name collision, cleanup.'
} finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $allowed = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') + '\.test-'
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
