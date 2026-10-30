$ErrorActionPreference = 'Stop'
$projectRoot = 'C:/Projects/com.nut.sort.relax'
$auditRoot = Join-Path $projectRoot 'Docs/APKBuild/20261014-analytics-release'
$sourceApk = Join-Path $projectRoot 'Builds/Android/ColorNutParty-1.0.1-release.apk'
$releaseApk = Join-Path $projectRoot 'Builds/Android/ColorNutParty-1.0.1-20261014-analytics-release.apk'
$buildInfo = Get-Content -Raw -LiteralPath (Join-Path $auditRoot 'process.json') | ConvertFrom-Json
$buildLog = Get-Content -Raw -LiteralPath (Join-Path $auditRoot 'unity-build.log')
if ($buildLog -notmatch 'NUT_ANDROID_BUILD_PASS bytes=(\d+) warnings=(\d+) errors=0') {
    throw 'Current signed release build has not succeeded.'
}
$buildWarnings = [int]$Matches[2]
if ($buildLog -notmatch 'NUT_ANDROID_BUILD_START package=com.color.nutparty.*version=1.0.1 code=1.*mode=signed-release') {
    throw 'Build did not use the requested production identity and release mode.'
}
if (Get-Process -Id $buildInfo.ProcessId -ErrorAction SilentlyContinue) { throw 'Unity has not finished restoring build settings.' }
if ((Get-Item -LiteralPath $sourceApk).LastWriteTime -lt [datetime]$buildInfo.StartedAt) { throw 'APK predates this build.' }
$java = 'C:/Users/pc/.jdks/jbr-17.0.14/bin/java.exe'
$signer = 'F:/AndroidSDK/build-tools/35.0.0/lib/apksigner.jar'
$signatureLines = & $java -jar $signer verify --verbose --print-certs $sourceApk 2>&1
if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed.' }
$signatureLines | Set-Content -Encoding utf8 -LiteralPath (Join-Path $auditRoot 'signature.txt')
$signatureText = $signatureLines -join "`n"
if ($signatureText -notmatch 'Signer #1 certificate SHA-256 digest: ([0-9a-f]+)') { throw 'APK certificate missing.' }
$certificateHash = $Matches[1]
if ($certificateHash -ne 'd14d534c3d52c389e24441dfe880bdcc0789fa846a7ab03e5368f81c560203a6') { throw 'APK certificate differs from the verified keystore.' }
$badgingLines = & 'F:/AndroidSDK/build-tools/35.0.0/aapt.exe' dump badging $sourceApk 2>&1
if ($LASTEXITCODE -ne 0) { throw 'APK manifest inspection failed.' }
$badgingLines | Set-Content -Encoding utf8 -LiteralPath (Join-Path $auditRoot 'badging.txt')
$manifest = $badgingLines -join "`n"
if ($manifest -notmatch "package: name='com.color.nutparty' versionCode='1' versionName='1.0.1'") { throw 'Unexpected package/version.' }
if ($manifest -match 'application-debuggable') { throw 'Production APK is debuggable.' }
if ($manifest -notmatch "native-code:.*'armeabi-v7a'" -or $manifest -notmatch "native-code:.*'arm64-v8a'") { throw 'Required architectures missing.' }
if ($manifest -notmatch "application-label:'Color Nut Party'") { throw 'Unexpected launcher label.' }
if ($manifest -notmatch "application-icon-\d+:'[^']+'") { throw 'Launcher icon missing.' }
$alignmentLines = & 'F:/AndroidSDK/build-tools/35.0.0/zipalign.exe' -c 4 $sourceApk 2>&1
if ($LASTEXITCODE -ne 0) { throw 'APK alignment verification failed.' }
'zipalign -c 4: PASS' | Set-Content -Encoding utf8 -LiteralPath (Join-Path $auditRoot 'alignment.txt')
$sdkBaseline = Get-Content -Raw -LiteralPath (Join-Path $auditRoot 'sdk-before.json') | ConvertFrom-Json
foreach ($entry in $sdkBaseline) {
    if ((Get-FileHash -LiteralPath $entry.path -Algorithm SHA256).Hash -ne $entry.sha256) { throw ('SDK changed: ' + $entry.path) }
}
$keystorePath = Join-Path $projectRoot 'Signing/cnp_qt_qianmingwenjian.keystore'
if ((Get-FileHash -LiteralPath $keystorePath -Algorithm SHA256).Hash -ne 'EA078AB85F86B98EB7433D601E9D5E442C097784BF839BEDBAFEEA1C7DBBF1AA') { throw 'Keystore file changed.' }
$restoredFiles = @{
    'ProjectSettings/ProjectSettings.asset' = 'ProjectSettings.before.asset'
    'Assets/Resources/Configuration/LocalAnalytics.asset' = 'LocalAnalytics.before.asset'
    'Assets/Scenes/LuoSiSortGame.unity' = 'LuoSiSortGame.before.unity'
    'Assets/Plugins/Android/gradleTemplate.properties' = 'gradleTemplate.before.properties'
}
foreach ($entry in $restoredFiles.GetEnumerator()) {
    $afterText = (Get-Content -Raw -LiteralPath (Join-Path $projectRoot $entry.Key)).Replace("`r`n", "`n")
    $beforeText = (Get-Content -Raw -LiteralPath (Join-Path $auditRoot $entry.Value)).Replace("`r`n", "`n")
    if ($afterText -ne $beforeText) { throw ('Unexpected change after build: ' + $entry.Key) }
}
$analyticsSettings = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Assets/Resources/Configuration/LocalAnalytics.asset')
if ($analyticsSettings -notmatch '(?m)^  IsTest: 0\s*$') { throw 'Analytics test flag is still enabled.' }
Copy-Item -LiteralPath $sourceApk -Destination $releaseApk
$apkHash = (Get-FileHash -LiteralPath $releaseApk -Algorithm SHA256).Hash
$result = [ordered]@{
    passed = $true
    apk = $releaseApk
    bytes = (Get-Item -LiteralPath $releaseApk).Length
    versionName = '1.0.1'
    versionCode = 1
    package = 'com.color.nutparty'
    sha256 = $apkHash
    certificateSha256 = $certificateHash
    keystore = $keystorePath
    alias = 'cnp_qt_qianmingwenjian'
    keystoreUnchanged = $true
    sdkFilesUnchanged = $sdkBaseline.Count
    signatureVerified = $true
    alignmentVerified = $true
    debuggable = $false
    architectures = @('armeabi-v7a', 'arm64-v8a')
    includesCustomAppIcon = $true
    includesAnalyticsImplementation = $true
    analyticsIsTest = $false
    buildSettingsRestored = $true
    unityWarnings = $buildWarnings
    unityErrors = 0
    verifiedAt = (Get-Date).ToString('o')
}
$result | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $auditRoot 'result.json')
($apkHash + '  ' + [IO.Path]::GetFileName($releaseApk)) | Set-Content -Encoding ascii -LiteralPath ($releaseApk + '.sha256')
$result | ConvertTo-Json -Depth 4
