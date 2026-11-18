# Woogy Play upload key - run ONCE by the owner (password is typed by you, never stored in the repo).
#   powershell -ExecutionPolicy Bypass -File C:\woopang\apps\woogy-android\tools\make-upload-key.ps1
# Creates  %USERPROFILE%\.android-signing\woogy\woogy-upload.jks + signing.properties
# BACK UP BOTH FILES (USB / private cloud). Play App Signing keeps the real app key; if this upload key is lost,
# Play Console > App integrity > "Request upload key reset" can replace it, but it takes days.

$ErrorActionPreference = 'Stop'
$dir = Join-Path $env:USERPROFILE '.android-signing\woogy'
$jks = Join-Path $dir 'woogy-upload.jks'
$props = Join-Path $dir 'signing.properties'
if (Test-Path $jks) { Write-Host "Already exists: $jks  (not overwriting)" -ForegroundColor Yellow; exit 0 }

$keytool = 'C:\Program Files\Eclipse Adoptium\jdk-17.0.17.10-hotspot\bin\keytool.exe'
if (-not (Test-Path $keytool)) { $keytool = 'keytool' }

$p1 = Read-Host 'New upload key password (min 6 chars)' -AsSecureString
$p2 = Read-Host 'Type it again' -AsSecureString
$plain1 = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($p1))
$plain2 = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($p2))
if ($plain1 -ne $plain2) { throw 'Passwords do not match' }
if ($plain1.Length -lt 6) { throw 'Password must be at least 6 characters' }

New-Item -ItemType Directory -Force $dir | Out-Null
& $keytool -genkeypair -v -keystore $jks -alias upload -keyalg RSA -keysize 2048 -validity 10000 `
  -storepass $plain1 -keypass $plain1 -dname 'CN=QUE Co. Ltd., O=QUE Co. Ltd., C=KR'
if ($LASTEXITCODE -ne 0) { throw 'keytool failed' }

$jksEsc = $jks -replace '\\', '/'
@"
storeFile=$jksEsc
storePassword=$plain1
keyAlias=upload
keyPassword=$plain1
"@ | Set-Content -Encoding ascii $props
$plain1 = $null; $plain2 = $null

Write-Host ''
Write-Host "Done: $jks" -ForegroundColor Green
Write-Host "      $props" -ForegroundColor Green
Write-Host 'Back up both files now. Then build:  cd C:\woopang\apps\woogy-android\android ; .\gradlew.bat bundleRelease'
& $keytool -list -v -keystore $jks -alias upload -storepass (Get-Content $props | Select-String '^storePassword=' | ForEach-Object { $_.Line.Substring(14) }) | Select-String 'SHA256:'
