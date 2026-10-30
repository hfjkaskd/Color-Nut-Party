$ErrorActionPreference = 'Stop'
$project = 'C:/Projects/com.nut.sort.relax'
$audit = Join-Path $project 'Docs/APKBuild/20261013-reward-art'
$source = Join-Path $project 'Builds/Android/ColorNutParty-1.0.1-release.apk'
$output = Join-Path $project 'Builds/Android/ColorNutParty-1.0.1-20261013-reward-art-fix.apk'
$processInfo = Get-Content -Raw -LiteralPath (Join-Path $audit 'process.json') | ConvertFrom-Json
$buildLog = Get-Content -Raw -LiteralPath (Join-Path $audit 'unity-build.log')
if ($buildLog -notmatch 'NUT_ANDROID_BUILD_PASS bytes=(\d+) warnings=(\d+) errors=0') { throw 'Current Unity build has not succeeded.' }
$buildWarnings = [int]$Matches[2]
if ((Get-Item -LiteralPath $source).LastWriteTime -lt [datetime]$processInfo.StartedAt) { throw 'APK predates this build.' }
Copy-Item -LiteralPath $source -Destination $output
$java = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java.exe'
$signer = 'F:/AndroidSDK/build-tools/35.0.0/lib/apksigner.jar'
$signature = & $java -jar $signer verify --verbose --print-certs $output 2>&1
if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed.' }
$signature | Set-Content -Encoding utf8 -LiteralPath (Join-Path $audit 'signature.txt')
$signatureText = $signature -join "`n"
if ($signatureText -notmatch 'Signer #1 certificate SHA-256 digest: ([0-9a-f]+)') { throw 'APK signing certificate missing.' }
$certificate = $Matches[1]
$keystoreReport = Get-Content -Raw -LiteralPath (Join-Path $audit 'keystore-check.txt')
if ($keystoreReport -notmatch 'SHA256:\s*([0-9A-F:]+)') { throw 'Keystore certificate audit missing.' }
$expectedCertificate = $Matches[1].Replace(':','').ToLowerInvariant()
if ($certificate -ne $expectedCertificate) { throw 'APK certificate differs from the requested keystore.' }
$badging = & 'F:/AndroidSDK/build-tools/35.0.0/aapt.exe' dump badging $output 2>&1
if ($LASTEXITCODE -ne 0) { throw 'APK manifest verification failed.' }
$badging | Set-Content -Encoding utf8 -LiteralPath (Join-Path $audit 'badging.txt')
$manifest = $badging -join "`n"
if ($manifest -notmatch "package: name='com.color.nutparty' versionCode='1' versionName='1.0.1'") { throw 'Unexpected APK package/version.' }
if ($manifest -match 'application-debuggable') { throw 'APK is debuggable.' }
if ($manifest -notmatch "native-code:.*'armeabi-v7a'" -or $manifest -notmatch "native-code:.*'arm64-v8a'") { throw 'Required architectures missing.' }
$alignment = & 'F:/AndroidSDK/build-tools/35.0.0/zipalign.exe' -c 4 $output 2>&1
if ($LASTEXITCODE -ne 0) { throw 'APK alignment verification failed.' }
'zipalign -c 4: PASS' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $audit 'alignment.txt')
$sdkBaseline = Get-Content -Raw -LiteralPath (Join-Path $audit 'sdk-before.json') | ConvertFrom-Json
foreach ($file in $sdkBaseline) {
    if ((Get-FileHash -LiteralPath $file.path -Algorithm SHA256).Hash -ne $file.sha256) { throw ('SDK changed: ' + $file.path) }
}
# Unity can serialize its empty keystore property as an empty in-project path.
# Restore only that known normalization, and refuse to overwrite any other edit.
$settings = Join-Path $project 'ProjectSettings/ProjectSettings.asset'
$before = Get-Content -Raw -LiteralPath (Join-Path $audit 'ProjectSettings.before.asset')
$after = Get-Content -Raw -LiteralPath $settings
if ($after -ne $before) {
    $normalized = $after.Replace("  AndroidKeystoreName: '{inproject}: '", '  AndroidKeystoreName: ')
    if ($normalized.Replace("`r`n","`n") -ne $before.Replace("`r`n","`n")) { throw 'Unexpected ProjectSettings change; inspection required.' }
    Copy-Item -LiteralPath (Join-Path $audit 'ProjectSettings.before.asset') -Destination $settings
}
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
$keystoreHash = (Get-FileHash -LiteralPath (Join-Path $project 'Signing/cnp_qt_qianmingwenjian.keystore') -Algorithm SHA256).Hash
if ($keystoreHash -ne 'EA078AB85F86B98EB7433D601E9D5E442C097784BF839BEDBAFEEA1C7DBBF1AA') { throw 'Signing file changed.' }
$result = [ordered]@{
    passed = $true; apk = $output; bytes = (Get-Item -LiteralPath $output).Length
    version = '1.0.1'; versionCode = 1; package = 'com.color.nutparty'
    sha256 = $hash; certificateSha256 = $certificate
    keystore = 'Signing/cnp_qt_qianmingwenjian.keystore'; alias = 'cnp_qt_qianmingwenjian'
    keystoreUnchanged = $true; sdkFilesUnchanged = $sdkBaseline.Count
    signatureVerified = $true; alignmentVerified = $true; debuggable = $false
    architectures = @('armeabi-v7a','arm64-v8a'); unityWarnings = $buildWarnings; unityErrors = 0
    includesRewardArtFix = $true; physicalDeviceRetested = $false
    verifiedAt = (Get-Date).ToString('o')
}
$result | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $audit 'result.json')
($hash + '  ' + [IO.Path]::GetFileName($output)) | Set-Content -Encoding ascii -LiteralPath ($output + '.sha256')
$result | ConvertTo-Json -Depth 4
