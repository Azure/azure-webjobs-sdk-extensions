param(
    [string[]]$tests = @(),
    [string]$Configuration
)

if (-not $Configuration) {
    Write-Host "Configuration not specified, defaulting to 'Release'" -ForegroundColor Yellow
    $Configuration = "Release"
}

function RunTest([string]$project, [bool]$skipBuild = $false, [string]$filter = $null) {
    Write-Host "Running test: $project" -ForegroundColor DarkCyan
    Write-Host "-----------------------------------------------------------------------------" -ForegroundColor DarkCyan
    Write-Host

    $cmdargs = "test", ".\test\$project\$project.csproj", "-v", "m", "--logger", "trx;LogFileName=TEST.xml"

    if ($Configuration)
    {
        Write-Host "Adding: --configuration $Configuration"
        $cmdargs += "--configuration", "$Configuration"
    }

    if ($filter) {
        Write-Host "Adding: --filter $filter"
        $cmdargs += "--filter", "$filter"
    }

    Write-Host "Final command: 'dotnet $cmdargs'"

# We'll always rebuild for now.
#    if ($skipBuild){
#        $cmdargs += "--no-build"
#    }
#    else {
#        Write-Host "Rebuilding project" -ForegroundColor Red
#    }
    
    & dotnet $cmdargs | Out-Host
    $r = $?
    
    Write-Host
    Write-Host "-----------------------------------------------------------------------------" -ForegroundColor DarkCyan
    Write-Host

    return $r
}

if ((-not $tests) -or ($tests.Count -lt 1))
{
    throw "No test projects specified to run. Exiting script."
}

$success = $true
$testRunSucceeded = $true

# timer tests require Daylight Savings Time, so switch settings, then reset.
$originalTZ = Get-TimeZone
Write-Host "Current TimeZone: '$originalTZ'"
Set-TimeZone -Name "Pacific Standard Time"
$currentTZ = Get-TimeZone
Write-Host "Changing TimeZone for Timer tests. Now '$currentTZ'"
Write-Host "Environment setting Configuration is '$env:Configuration'."

dotnet --version

foreach ($test in $tests){
    $testRunSucceeded = RunTest $test $testRunSucceeded
    $success = $testRunSucceeded -and $success
}

Set-TimeZone -Id $originalTZ.Id
$currentTZ = Get-TimeZone
Write-Host "Changing TimeZone back to original. Now '$currentTZ'"

if (-not $success) { exit 1 }