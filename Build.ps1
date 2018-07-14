param (
  [string]$packageSuffix = "0",
  [bool]$isLocal = $false,
  [string]$outputDirectory = "..\..\buildoutput"
)

if ($isLocal){
  $packageSuffix = "dev" + [datetime]::UtcNow.Ticks.ToString()
  Write-Host "Local build - setting package suffixes to $packageSuffix" -ForegroundColor Yellow
}
dotnet --version

dotnet build -v q

if (-not $?) { exit 1 }

$projects =
    "WebJobs.Extensions",
    "WebJobs.Extensions.CosmosDB",
    "WebJobs.Extensions.Http",
    "WebJobs.Extensions.MobileApps",
    "WebJobs.Extensions.Twilio",
    "WebJobs.Extensions.SendGrid"

foreach ($project in $projects)
{
  $cmd = "pack", "src\$project\$project.csproj", "-o", $outputDirectory, "--no-build"
  
  if ($packageSuffix -ne "0")
  {
    $cmd += "--version-suffix", "-$packageSuffix"
  }
  
  & dotnet $cmd  
}