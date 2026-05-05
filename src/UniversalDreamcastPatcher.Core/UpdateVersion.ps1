# Read version from ../../version.txt and regenerate Constants.cs.
# Strips a leading "v" so callers that prepend "v" themselves (About / title bar)
# don't end up with "vv2.0.0".

$versionFile = Join-Path $PSScriptRoot ".." | Join-Path -ChildPath ".." | Join-Path -ChildPath "version.txt"

if (-not (Test-Path -LiteralPath $versionFile)) {
    Write-Error "Version file not found at: $versionFile"
    exit 1
}

$version = (Get-Content -LiteralPath $versionFile | Out-String).Trim()
$version = $version -replace '^[vV]', ''

$constantsContent = @"
// AUTO-GENERATED FILE - Version is read from ../../version.txt during build.
// Do not edit by hand - update ../../version.txt for the version, or
// UpdateVersion.ps1 to change anything else in this file.
// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core;

public static class Constants
{
    public const string AppName = "Universal Dreamcast Patcher";
    public const string AppExecutableBase = "UniversalDreamcastPatcher";
    public const string Version = "$version";
    public const string Repo = "DerekPascarella/UniversalDreamcastPatcher";
    public const string AppUrl = "https://github.com/DerekPascarella/UniversalDreamcastPatcher";
    public const string ChangelogUrl = "https://github.com/DerekPascarella/UniversalDreamcastPatcher#changelog";
}
"@

$constantsFile = Join-Path $PSScriptRoot "Constants.cs"
[System.IO.File]::WriteAllText($constantsFile, $constantsContent, [System.Text.Encoding]::UTF8)

Write-Host "Updated version to $version in Constants.cs"
