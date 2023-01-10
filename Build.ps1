param (
  [string[]]$projects = @(),
  [string]$buildNumber,
  [string]$packageSuffix = "0",
  [bool]$isLocal = $false,
  [bool]$signPackages = $false,
  [string]$outputDirectory = (Join-Path -Path $PSScriptRoot -ChildPath "buildoutput"),
  [bool]$skipAssemblySigning = $false
)

if ($null -eq $buildNumber) {
  throw 'Parameter $buildNumber cannot be null or empty. Exiting script.'
}

if ($isLocal){
  $packageSuffix = "dev" + [datetime]::UtcNow.Ticks.ToString()
  Write-Host "Local build - setting package suffixes to $packageSuffix" -ForegroundColor Yellow
}

dotnet --version

dotnet build -v m

if (-not $?) { exit 1 }

foreach ($project in $projects)
{
  $cmd = "pack", "src\$project\$project.csproj", "-o", $outputDirectory, "--no-build"
  
  if ($packageSuffix -ne "0")
  {
    $cmd += "--version-suffix", "-$packageSuffix"
  }
  
  & { dotnet $cmd }
}

if ($signPackages) {
  & { .\tools\RunSigningJob.ps1 -artifactDirectory $outputDirectory -buildNumber $buildNumber -skipAssemblySigning $skipAssemblySigning }
  if (-not $?) { exit 1 }
}