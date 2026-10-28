$ErrorActionPreference = 'Stop'
$taskSource = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskTemp = [IO.Path]::GetFullPath((Join-Path $taskSource 'Temp'))
$taskTarget = [IO.Path]::GetFullPath((Join-Path $taskTemp 'HeadlessProject'))
if (!$taskTarget.StartsWith($taskTemp + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'The validation project must stay inside this project''s Temp folder.'
}
foreach ($taskPath in @($taskTemp,$taskTarget)) {
    if ((Test-Path -LiteralPath $taskPath) -and (Get-Item -LiteralPath $taskPath -Force).LinkType) {
        throw "The validation path must not be a symbolic link or junction: $taskPath"
    }
}
foreach ($taskFolder in @('Assets','Packages','ProjectSettings')) {
    $taskFrom = Join-Path $taskSource $taskFolder
    if (!(Test-Path -LiteralPath $taskFrom -PathType Container)) { throw "Missing project folder: $taskFrom" }
    $taskLinks = @(Get-ChildItem -LiteralPath $taskFrom -Recurse -Force | Where-Object { $_.LinkType })
    if ($taskLinks.Count -gt 0) { throw "Copy refused: $taskFrom contains symbolic links or junctions." }
}
if (Test-Path -LiteralPath $taskTarget) {
    $taskLinks = @(Get-ChildItem -LiteralPath $taskTarget -Recurse -Force | Where-Object { $_.LinkType })
    if ($taskLinks.Count -gt 0) { throw 'The validation destination contains a symbolic link or junction.' }
    $taskInstancePath = Join-Path $taskTarget 'Library/EditorInstance.json'
    if (Test-Path -LiteralPath $taskInstancePath) {
        $taskInstance = Get-Content -LiteralPath $taskInstancePath -Raw | ConvertFrom-Json
        $taskExisting = Get-Process -Id $taskInstance.process_id -ErrorAction SilentlyContinue
        if ($taskExisting -and $taskExisting.ProcessName -eq 'Unity') { throw 'The validation project is already running.' }
    }
    $taskLockPath = Join-Path $taskTarget 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $taskLockPath) {
        try { $taskLock = [IO.File]::Open($taskLockPath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
        catch { throw "The validation project is locked: $taskLockPath" }
        finally { if ($taskLock) { $taskLock.Dispose() } }
    }
}
New-Item -ItemType Directory -Path $taskTarget -Force | Out-Null
foreach ($taskFolder in @('Assets','Packages','ProjectSettings')) {
    $taskFrom = Join-Path $taskSource $taskFolder
    $taskTo = Join-Path $taskTarget $taskFolder
    # Copy only project sources and their .meta files. No Library, .git or sentinel data.
    & robocopy.exe $taskFrom $taskTo /E /R:0 /W:0 /COPY:DAT /DCOPY:DAT /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -gt 7) { throw "Copy failed for $taskFolder (robocopy exit $LASTEXITCODE)." }
}
Write-Output $taskTarget
