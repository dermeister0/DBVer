#requires -Version 3.0

$outputPath = "$PSScriptRoot\Distr"

Import-Module "$PSScriptRoot\Scripts\BuildHelpers.psm1"

Initialize-BuildVariables
Invoke-NugetRestore "$PSScriptRoot\DBVer.sln"

Invoke-SolutionBuild "$PSScriptRoot\DBVer.sln" 'Release'

if (Test-Path $outputPath)
{
    Remove-Item -recurse -force $outputPath
}

New-Item $outputPath -ItemType directory

$nugetExePath = "$PSScriptRoot\Scripts\nuget.exe"
$packagesPath = "$PSScriptRoot\packages"
&$nugetExePath install ILMerge -Version '2.14.1208' -OutputDirectory $packagesPath

$releasePath = "$PSScriptRoot\bin\Release"
$ilMergePath = $packagesPath + '\ilmerge.2.14.1208\tools\ILMerge.exe'

&$ilMergePath "/out:${outputPath}\DBVer.exe" "$releasePath\DBVer.exe" "$releasePath\*.dll" '/wildcards'

Copy-Item "$releasePath\DBVer.exe.config" $outputPath