param(
    [Parameter(Mandatory=$true)][string]$WorkspaceDirectory,
    [Parameter(Mandatory=$true)][string]$GameDirectory
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath($PSScriptRoot)
$workspace = (Resolve-Path -LiteralPath $WorkspaceDirectory).Path
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$records = [Collections.Generic.List[object]]::new()
function Save-File($file, $relative, $origin) {
    $destination = Join-Path $repo $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash) { throw "Copy mismatch: $relative" }
    $records.Add([pscustomobject]@{Path=$relative.Replace('\','/'); Origin=$origin; Bytes=$file.Length; SHA256=$hash})
}
function Save-Workspace($directory) {
    foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
        if ($item.PSIsContainer) {
            if ($item.Name -in @('.git','.tools','.nuget-appdata','.codex','.agents','obj','obj5','node_modules','github-mod-backup')) { continue }
            Save-Workspace $item.FullName
        } else {
            $relative = $item.FullName.Substring($workspace.Length + 1)
            if ($relative -match '(^|\\)(bin|bin5)\\' -and $item.Name -notlike 'Sephiria*.dll') { continue }
            if ($item.Name -like '*.user') { continue }
            Save-File $item (Join-Path 'workspace' $relative) 'workspace'
        }
    }
}
Save-Workspace $workspace
foreach ($folder in @('BepInEx/plugins','BepInEx/config','AddOns','AddOns_Disabled')) {
    $source = Join-Path $game $folder
    if (Test-Path -LiteralPath $source) {
        foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File -Force) {
            Save-File $file (Join-Path 'installed' $file.FullName.Substring($game.Length + 1)) 'installed'
        }
    }
}
$records | Sort-Object Path | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $repo 'manifest.json') -Encoding utf8
Write-Output "Exported and SHA256 verified $($records.Count) files. Files removed at source are retained; review git diff before committing."
