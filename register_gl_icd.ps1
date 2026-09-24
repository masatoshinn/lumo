$ErrorActionPreference = "Stop"

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "NOT ADMIN - attempting elevation..."
    $script = $MyInvocation.MyCommand.Path
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$script`""
    exit
}

Write-Host "Running as ADMIN - registering OpenGL ICD..."

$icdPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\OpenGLDrivers"
$dllName = "ig4icd64.dll"
$dllPath = "C:\Windows\System32\$dllName"

if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: $dllPath not found!"
    exit 1
}

# Create ICD registration entry
$subKey = Join-Path $icdPath "icd64"
if (-not (Test-Path $subKey)) {
    New-Item -Path $subKey -Force | Out-Null
}

Set-ItemProperty -Path $subKey -Name "Driver" -Value $dllName -Type String
Set-ItemProperty -Path $subKey -Name "DLL" -Value $dllName -Type String
Set-ItemProperty -Path $subKey -Name "Description" -Value "Intel OpenGL ICD (ig4icd64)" -Type String
Set-ItemProperty -Path $subKey -Name "Flags" -Value 1 -Type DWord

Write-Host "Registered ICD at: $subKey"
Write-Host "  Driver = $dllName"
Write-Host "  DLL = $dllName"
Write-Host "  Flags = 1"

# Verify
Write-Host "`nVerification:"
Get-ChildItem $icdPath | ForEach-Object {
    $n = $_.PSChildName
    $v = Get-ItemProperty $_.PSPath
    Write-Host "  $n => Driver=$($v.Driver), Flags=$($v.Flags)"
}

Write-Host "`nDone! OpenGL should work now. Test it."
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
