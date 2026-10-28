param([string]$ProjectPath, [switch]$RenderHud)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
$taskRoot = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ProjectPath).Path)
foreach ($taskRequired in @('Assets','Packages/manifest.json','ProjectSettings/ProjectVersion.txt')) {
    if (!(Test-Path -LiteralPath (Join-Path $taskRoot $taskRequired))) { throw "Not a Unity project: $taskRoot" }
}
$taskInstancePath = Join-Path $taskRoot 'Library/EditorInstance.json'
if (Test-Path -LiteralPath $taskInstancePath) {
    $taskInstance = Get-Content -LiteralPath $taskInstancePath -Raw | ConvertFrom-Json
    $taskExisting = Get-Process -Id $taskInstance.process_id -ErrorAction SilentlyContinue
    if ($taskExisting -and $taskExisting.ProcessName -eq 'Unity') {
        throw "The target project is already open. No Editor was closed or controlled: $taskRoot"
    }
}
$taskLockPath = Join-Path $taskRoot 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $taskLockPath) {
    try { $taskLock = [IO.File]::Open($taskLockPath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
    catch { throw "The target project's lock file is in use or inaccessible: $taskLockPath" }
    finally { if ($taskLock) { $taskLock.Dispose() } }
}
$taskEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
$taskLog = Join-Path $taskRoot 'Logs/HeadlessValidation.log'
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'Logs') -Force | Out-Null
$taskReport = Join-Path $taskRoot 'Library/CRITICAL/HeadlessValidation.json'
if (Test-Path -LiteralPath $taskReport) { Remove-Item -LiteralPath $taskReport }
$taskStart = [Diagnostics.ProcessStartInfo]::new($taskEditor)
$taskStart.WorkingDirectory = $taskRoot
$taskStart.UseShellExecute = $false
$taskStart.CreateNoWindow = $true
$taskStart.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
foreach ($taskArgument in @('-batchmode','-projectPath',$taskRoot,'-executeMethod','Critical.Editor.HeadlessValidation.Run','-logFile',$taskLog)) {
    $taskStart.ArgumentList.Add($taskArgument)
}
if ($RenderHud) {
    foreach ($taskArgument in @('-screen-width','1920','-screen-height','1080','-screen-fullscreen','0')) { $taskStart.ArgumentList.Add($taskArgument) }
} else { $taskStart.ArgumentList.Add('-nographics') }
$taskProcess = [Diagnostics.Process]::Start($taskStart)
Write-Output ('Headless validation started; process '+$taskProcess.Id+'. No desktop input or windows.')
Write-Output ('Project: '+$taskRoot)
Write-Output ('Log: '+$taskLog)
$taskProcess.WaitForExit()
Write-Output ('Unity batch exit: '+$taskProcess.ExitCode)
Write-Output ('Report: '+$taskReport)
if (Test-Path -LiteralPath $taskReport) { Get-Content -LiteralPath $taskReport -Raw }
exit $taskProcess.ExitCode
