param(
    [Parameter(Mandatory=$true)][string]$Action,
    [int]$TimeoutSeconds = 30
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
if ($Action -eq 'refresh-scripts') {
    # Existing Visual Studio integration protocol (package 2.0.22); no UI input.
    $taskInstance = Get-Content -LiteralPath (Join-Path $taskRoot 'Library/EditorInstance.json') -Raw | ConvertFrom-Json
    $taskPort = 56002 + ($taskInstance.process_id % 1000)
    $taskClient = [Net.Sockets.UdpClient]::new()
    try {
        $taskPayload = [byte[]](8,0,0,0,0,0,0,0)
        [void]$taskClient.Send($taskPayload,$taskPayload.Length,'127.0.0.1',$taskPort)
        Write-Output 'Script refresh requested through the installed IDE integration.'
    } finally { $taskClient.Dispose() }
    exit
}
$taskFolder = Join-Path $taskRoot 'Library/CRITICAL/ProceduralAutomation'
if (!(Test-Path -LiteralPath (Join-Path $taskFolder 'enabled'))) { throw 'The local Editor automation sentinel is not enabled.' }
if (Test-Path -LiteralPath (Join-Path $taskFolder 'command.json')) { throw 'Another Editor command is still pending.' }
$taskId = $Action + '-' + [guid]::NewGuid().ToString('N').Substring(0,10)
$taskResult = Join-Path $taskFolder ($taskId + '.json')
$taskTemporary = Join-Path $taskFolder ($taskId + '.tmp')
@{id=$taskId;action=$Action} | ConvertTo-Json | Set-Content -LiteralPath $taskTemporary -Encoding utf8
Move-Item -LiteralPath $taskTemporary -Destination (Join-Path $taskFolder 'command.json')
$taskWatch = [Diagnostics.Stopwatch]::StartNew()
while (!(Test-Path -LiteralPath $taskResult)) {
    if ($taskWatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw "Editor command timed out; inspect pending command before retrying. Result: $taskResult" }
    Start-Sleep -Milliseconds 300
}
$taskResponse = Get-Content -LiteralPath $taskResult -Raw
Write-Output $taskResponse
if (!(($taskResponse | ConvertFrom-Json).success)) { exit 1 }
