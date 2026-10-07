[CmdletBinding()]
param([switch]$CheckOnly, [switch]$EnableAutoStart)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$stageDirectory = Join-Path $projectRoot ('artifacts/release-' + [Guid]::NewGuid().ToString('N'))
$projectFile = Join-Path $projectRoot 'NoxVault/NoxVault.csproj'
$targetDirectory = Join-Path $projectRoot 'dist/NoxVault'
$targetExe = Join-Path $targetDirectory 'NoxVault.exe'
$backupExe = Join-Path $targetDirectory 'NoxVault.previous.exe'

# Build and test the exact self-contained executable before touching the live app.
$dotnetCommand = Join-Path $projectRoot 'artifacts/dotnet-10.0.401/dotnet.exe'
if (!(Test-Path -LiteralPath $dotnetCommand)) { $dotnetCommand = 'dotnet' }
& $dotnetCommand publish $projectFile -c Release -r win-x64 --self-contained true '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:DebugType=None' ('-p:BaseOutputPath=' + (Join-Path $stageDirectory 'build') + '\') -o $stageDirectory
if ($LASTEXITCODE -ne 0) { throw 'Publish fehlgeschlagen; installierte App wurde nicht verändert.' }
$stageExe = Join-Path $stageDirectory 'NoxVault.exe'
$reportFile = Join-Path $stageDirectory 'test-results.txt'
$testProcess = Start-Process -FilePath $stageExe -ArgumentList '--self-test', ('"' + $reportFile + '"') -WindowStyle Hidden -PassThru
if (!$testProcess.WaitForExit(60000)) {
    Stop-Process -Id $testProcess.Id
    throw 'Tests überschritten 60 Sekunden; installierte App wurde nicht verändert.'
}
if ($testProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $reportFile)) { throw "Tests fehlgeschlagen: $reportFile" }
$reportLines = @(Get-Content -LiteralPath $reportFile)
if ($reportLines.Count -eq 0 -or @($reportLines | Where-Object { $_ -notmatch '^PASS:' }).Count -ne 0) {
    throw "Testbericht enthält Fehler: $reportFile"
}
Write-Output ("{0} Prüfungen bestanden. Bericht: {1}" -f $reportLines.Count, $reportFile)
if ($CheckOnly) { return }

New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
$hadPrevious = Test-Path -LiteralPath $targetExe
if ($hadPrevious) { Copy-Item -LiteralPath $targetExe -Destination $backupExe -Force }
$appProcesses = @(Get-Process NoxVault -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $targetExe })
foreach ($appProcess in $appProcesses) {
    $installedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($targetExe)
    if ($installedVersion.FileMajorPart -gt 1 -or ($installedVersion.FileMajorPart -eq 1 -and $installedVersion.FileMinorPart -ge 1)) {
        Start-Process -FilePath $targetExe -ArgumentList '--prepare-update' -WindowStyle Hidden -Wait
        if (!$appProcess.WaitForExit(15000)) { throw 'Update abgebrochen: Bitte offene Dialoge und laufende Vorgänge abschließen. Die installierte App wurde nicht verändert.' }
    } else {
        # One-time compatibility path for versions predating the update handshake.
        Stop-Process -Id $appProcess.Id; $appProcess.WaitForExit()
    }
}
try {
    Copy-Item -LiteralPath $stageExe -Destination $targetExe -Force
    if ((Get-FileHash -LiteralPath $targetExe).Hash -ne (Get-FileHash -LiteralPath $stageExe).Hash) { throw 'Dateiprüfung nach Austausch fehlgeschlagen.' }
    $installArgs = @('--install-shortcuts')
    if ($EnableAutoStart) { $installArgs += '--enable-autostart' }
    $installer = Start-Process -FilePath $targetExe -ArgumentList $installArgs -WindowStyle Hidden -Wait -PassThru
    if ($installer.ExitCode -ne 0) { throw 'Startmenü/Autostart-Verknüpfungen konnten nicht eingerichtet werden.' }
    Start-Process -FilePath $targetExe -WindowStyle Hidden
} catch {
    if ($hadPrevious) {
        Copy-Item -LiteralPath $backupExe -Destination $targetExe -Force
        Start-Process -FilePath $targetExe -WindowStyle Hidden
    }
    throw
}
Write-Output 'vault aktualisiert. Vorherige Version: dist/NoxVault/NoxVault.previous.exe'
