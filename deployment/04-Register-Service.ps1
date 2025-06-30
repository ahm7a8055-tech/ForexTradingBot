# ====================================================================================
# THE DEFINITIVE WINDOWS SERVICE MANAGEMENT SCRIPT (v.Final-Victory-Robust-Improved-Syntax)
# This version is simpler because the .NET app is now service-aware.
# It only needs to create the service pointing to the EXE.
# Improved error handling for service startup and corrected Get-Service syntax.
# ====================================================================================
param(
    [string]$DeployPath,
    [string]$TempPath
)

$ScriptLogFile = Join-Path $TempPath "04-Service-Log-$(Get-Date -f yyyyMMdd-HHmmss).txt"
Start-Transcript -Path $ScriptLogFile -Append
$ErrorActionPreference = 'Stop'

Write-Host "--- SCRIPT 4: REGISTER & LAUNCH WINDOWS SERVICE ---" -ForegroundColor Cyan
$ServiceName = "ForexTradingBotAPI"
$DisplayName = "Forex Trading Bot API Service"
$ExePath     = Join-Path $DeployPath "WebAPI.exe"

Write-Host "Verifying presence of executable at '$ExePath'..."
if (-not (Test-Path $ExePath)) {
    throw "FATAL: Cannot find executable at '$ExePath'."
}
Write-Host "✅ Executable found."

# --- Section: Remove and Re-register Service ---
Write-Host "Checking for existing service '$ServiceName'..."
# Corrected: Only use one ErrorAction parameter. SilentlyContinue is appropriate here.
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($service) {
    Write-Host "Existing service '$ServiceName' found. Attempting to stop and remove..."
    if ($service.Status -ne 'Stopped') {
        Write-Host "Service is running. Stopping service '$ServiceName'..."
        try {
            Stop-Service -Name $ServiceName -Force -ErrorAction Stop
            Write-Host "Service '$ServiceName' stopped successfully."
        } catch {
            Write-Warning "Failed to stop service '$ServiceName'. It might be stuck. Continuing with removal attempt."
            # Log the specific error if you want more detail
            # Write-Host "Error details: $($_.Exception.Message)"
        }
    }

    # Robust removal with a wait loop
    Write-Host "Waiting for service '$ServiceName' to be fully removed..."
    $timeout = 60 # seconds
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $serviceRemoved = $false
    while ($stopwatch.Elapsed.TotalSeconds -lt $timeout) {
        # Corrected: Get-Service without -ea 0, as -ErrorAction SilentlyContinue handles it.
        if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
            Write-Host "`n✅ Service '$ServiceName' confirmed as removed."
            $serviceRemoved = $true
            break
        }
        Write-Host -NoNewline "."
        Start-Sleep -Seconds 2
    }

    if (-not $serviceRemoved) {
        throw "FATAL: Timed out after $timeout seconds waiting for service '$ServiceName' to be deleted. It may be stuck. Please check the server and manually remove it if necessary."
    }
} else {
    Write-Host "Service '$ServiceName' not found. Proceeding with registration."
}

Write-Host "Creating a new, clean Windows Service '$ServiceName'..."
try {
    New-Service -Name $ServiceName -BinaryPathName "`"$ExePath`"" -DisplayName $DisplayName -StartupType Automatic
    Write-Host "✅ New service '$ServiceName' created successfully."

    # Configure failure actions immediately after creation
    Write-Host "Configuring service for automatic restart on failure..."
    # Reset after 1 day (86400 seconds), restart after 1 minute (60000 milliseconds)
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000
    Write-Host "✅ Service failure actions configured."

} catch {
    throw "FATAL: Failed to create service '$ServiceName'. Error: $($_.Exception.Message)"
}

# --- Section: Start and Verify Service ---
Write-Host "Starting the service '$ServiceName'..."
try {
    Start-Service -Name $ServiceName -Verbose
    Write-Host "✅ Service '$ServiceName' started successfully."
} catch {
    # Catch specific start errors for better diagnosis
    throw "FATAL: Failed to start service '$ServiceName'. Error: $($_.Exception.Message). Please check Windows Event Viewer for details related to the service startup."
}

Write-Host "Waiting 15 seconds and performing final status check..."
Start-Sleep -Seconds 15

try {
    $finalService = Get-Service -Name $ServiceName
    if ($finalService.Status -eq 'Running') {
        Write-Host "✅✅✅✅✅✅✅✅✅ VICTORY! The Windows Service is RUNNING! ✅✅✅✅✅✅✅✅✅" -ForegroundColor Green
    } elseif ($finalService.Status -eq 'Stopped') {
        # If it's stopped, it likely failed to start. Provide more context.
        throw "FATAL: Service '$ServiceName' is in state 'Stopped'. This often means it failed to start. Check Windows Event Viewer (Application and System logs) for specific errors from '$($DisplayName)'. Ensure the executable '$ExePath' is correct and has necessary permissions."
    } else {
        throw "FATAL: Service '$ServiceName' is in an unexpected state: '$($finalService.Status)'. Check Windows Event Viewer for details."
    }
} catch {
    # Catch errors during Get-Service if the service somehow disappeared again
    throw "FATAL: Error during final service status check for '$ServiceName'. Error: $($_.Exception.Message)"
}

Stop-Transcript