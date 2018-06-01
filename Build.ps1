param (
  [string]$packageSuffix = "0"
)

# Disable .NET First run experience:
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

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
  $cmd = "pack", "src\$project\$project.csproj", "-o", "..\..\buildoutput", "--no-build"
  
  if ($packageSuffix -ne "0")
  {
    $cmd += "--version-suffix", "-$packageSuffix"
  }
  
  & dotnet $cmd  
}