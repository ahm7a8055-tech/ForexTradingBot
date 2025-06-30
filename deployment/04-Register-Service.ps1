# ====================================================================================
# THE DEFINITIVE WINDOWS SERVICE MANAGEMENT SCRIPT (v.Final-Victory-Robust)
# This version is simpler because the .NET app is now service-aware.
# It only needs to create the service pointing to the EXE.
# ====================================================================================
param(
    [string]$DeployPath,
    [string]$TempPath
)
$ScriptLogFile = Join-Path $TempPath "04-Service-Log-$(Get-Date -f yyyyMMdd-HHmmss).txt"
Start-Transcript -Path $ScriptLogFile -Append
$ErrorActionPreference = 'Stop'
$ProcessName = (Get-Item (Join-Path $DeployPath "WebAPI.exe")).BaseName # Get the base name of the executable

Write-Host "--- SCRIPT 4: REGISTER & LAUNCH WINDOWS SERVICE ---" -ForegroundColor Cyan
$ServiceName = "ForexTradingBotAPI"
$DisplayName = "Forex Trading Bot API Service"
$ExePath     = Join-Path $DeployPath "WebAPI.exe"

Write-Host "Verifying presence of executable at '$ExePath'..."
if (-not (Test-Path $ExePath)) {
    throw "FATAL: Cannot find executable at '$ExePath'."
}
Write-Host "✅ Executable found."

Write-Host "Stopping and removing existing service '$ServiceName' for a clean install..."
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 5 # Give it a moment to process the stop command
    }
    
    sc.exe delete "$ServiceName"
    
    # --- START: Robust Wait Loop (THE FIX) ---
    # This loop checks until the service is confirmed to be gone, with a 60-second timeout.
    # It also checks for and terminates lingering processes if the service is marked for deletion.
    Write-Host "Waiting for service '$ServiceName' to be fully removed or for process to terminate..."
    $timeout = 120 # seconds (Increased timeout to allow for process termination)
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed.TotalSeconds -lt $timeout) {
        $serviceCheck = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($null -eq $serviceCheck) {
            Write-Host "`n✅ Service fully removed."
            break # Exit the loop, the service is gone
        }

        # If service is still present and marked for deletion (PID 0 often indicates this state implicitly)
        # Try to find and terminate the process
        $lingeringProcesses = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq $ProcessName }
        if ($lingeringProcesses) {
 Write-Host "`nFound lingering process(es) for $($lingeringProcesses[0].ProcessName). Attempting to terminate..."
            $lingeringProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
            Write-Host "✅ Lingering process(es) terminated."
        }

        Write-Host -NoNewline "."
        Start-Sleep -Seconds 2
    }

    if ($stopwatch.Elapsed.TotalSeconds -ge $timeout) {
        throw "FATAL: Timed out after $timeout seconds waiting for service '$ServiceName' to be deleted. It may be stuck. Please check the server."
    }
    # --- END: Robust Wait Loop ---
}

Write-Host "Creating a new, clean Windows Service '$ServiceName'..."
New-Service -Name $ServiceName -BinaryPathName "`"$ExePath`"" -DisplayName $DisplayName -StartupType Automatic
Write-Host "✅ New service created successfully."

Write-Host "Configuring service for automatic restart on failure..."
sc.exe failure $ServiceName reset= 86400 actions= restart/60000
Write-Host "✅ Service failure actions configured."

Write-Host "Starting the service '$ServiceName'..."
Start-Service -Name $ServiceName -Verbose

Write-Host "Waiting 15 seconds and performing final status check..."
Start-Sleep -Seconds 15
$finalService = Get-Service -Name $ServiceName
if ($finalService.Status -ne 'Running') {
    throw "FATAL: Service '$ServiceName' is in state '$($finalService.Status)'. Check Windows Event Viewer."
}

Write-Host "✅✅✅✅✅✅✅✅✅ VICTORY! The Windows Service is RUNNING! ✅✅✅✅✅✅✅✅✅" -ForegroundColor Green
Stop-Transcript