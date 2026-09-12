param([string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$catalog = Get-Content -LiteralPath (Join-Path $root 'catalog/mods.json') -Raw | ConvertFrom-Json
if ($catalog.schemaVersion -ne 1 -or $catalog.mods.Count -eq 0) { throw 'Invalid catalog schema or empty catalog.' }
$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$checked = 0
foreach ($mod in $catalog.mods) {
    if (-not $mod.id -or -not $ids.Add($mod.id) -or $mod.versions.Count -eq 0) { throw "Duplicate/empty identity: $($mod.id)" }
    $versions = [Collections.Generic.HashSet[string]]::new()
    foreach ($release in $mod.versions) {
        if (-not $versions.Add($release.version) -or $release.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid version/hash.' }
        $prefix = 'https://raw.githubusercontent.com/MKfeel/SephiriaMods/main/'
        if (-not $release.url.StartsWith($prefix)) { throw 'Unexpected package host or repository.' }
        $relative = $release.url.Substring($prefix.Length)
        if ($relative -notmatch '^packages/[A-Za-z0-9._-]+/[A-Za-z0-9._-]+\.zip$') { throw 'Invalid package path.' }
        $file = Join-Path $root $relative
        if ((Get-Item -LiteralPath $file).Length -ne $release.size -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $release.sha256) { throw "Size/hash mismatch: $relative" }
        $zip = [IO.Compression.ZipFile]::OpenRead($file)
        try {
            $manifestEntry = $zip.GetEntry('mod-manifest.json')
            if (-not $manifestEntry) { throw 'Missing package manifest.' }
            $reader = [IO.StreamReader]::new($manifestEntry.Open())
            try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            if ($manifest.schemaVersion -ne 1 -or $manifest.mods.Count -ne 1 -or $manifest.mods[0].id -cne $mod.id -or $manifest.mods[0].version -cne $release.version) { throw 'Package identity mismatch.' }
            $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            foreach ($item in $manifest.mods[0].files) {
                if ($item.source -cne $item.target -or $item.target -notmatch '^(BepInEx/plugins|AddOns)/' -or $item.target -match '(^|/)\.\.?(/|$)|\\|:' -or -not $paths.Add($item.target)) { throw 'Unsafe or duplicate package target.' }
                $entry = $zip.GetEntry($item.source)
                if (-not $entry) { throw "Missing payload: $($item.source)" }
                $stream = $entry.Open()
                try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
                if ($hash -ne $item.sha256) { throw 'Payload hash mismatch.' }
            }
            if ($zip.Entries.Count -ne $paths.Count+1) { throw 'Unlisted archive entries.' }
        } finally { $zip.Dispose() }
        $checked++
    }
}
Write-Output "Validated $($catalog.mods.Count) mods / $checked packages."
