param (
  [string[]]$projects = @(),
  [string]$buildNumber,
  [string]$packageSuffix = "0",
  [bool]$isLocal = $false,
  [string]$outputDirectory = (Join-Path -Path $PSScriptRoot -ChildPath "buildoutput"),
  [bool]$pack = $false,
  [string]$Configuration
)

if (-not $Configuration) {
  Write-Host "Configuration not specified, defaulting to 'Release'" -ForegroundColor Yellow
  $Configuration = "Release"
}

if ($null -eq $buildNumber) {
  throw 'Parameter $buildNumber cannot be null or empty. Exiting script.'
}

if ($isLocal){
  $packageSuffix = "dev" + [datetime]::UtcNow.Ticks.ToString()
  Write-Host "Local build - setting package suffixes to $packageSuffix" -ForegroundColor Yellow
}

dotnet --version

if (-not $?) { exit 1 }

foreach ($project in $projects)
{ 
  # This assumes we've already built the package
  if ($pack)
  {
    $cmd = "pack", "src\$project\$project.csproj", "-c", $Configuration, "-o", $outputDirectory, "--no-build"

    if ($packageSuffix -ne "0")
    {
      $cmd += "--version-suffix", "-$packageSuffix"
    }
  } 
  else 
  {
    $cmd = "build",  "-c", $Configuration, "src\$project\$project.csproj", "-v", "m"
  }  

  Write-Host dotnet $cmd
  & { dotnet $cmd }
}