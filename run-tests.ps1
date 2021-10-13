function RunTest([string] $project, [string] $description,[bool] $skipBuild = $false, $filter = $null) {
    Write-Host "Running test: $description" -ForegroundColor DarkCyan
    Write-Host "-----------------------------------------------------------------------------" -ForegroundColor DarkCyan
    Write-Host

    $cmdargs = "test", ".\test\$project\", "-v", "q"
    
    if ($filter) {
       $cmdargs += "--filter", "$filter"
    }

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


$tests = @(
  @{project ="WebJobs.Extensions.Tests"; description="Core extension Tests"}
)

$success = $true
$testRunSucceeded = $true

# timer tests require Daylight Savings Time, so switch settings, then reset.
$originalTZ = Get-TimeZone
Write-Host "Current TimeZone: '$originalTZ'"
Set-TimeZone -Name "Pacific Standard Time"
$currentTZ = Get-TimeZone
Write-Host "Changing TimeZone for Timer tests. Now '$currentTZ'"

foreach ($test in $tests){
    $testRunSucceeded = RunTest $test.project $test.description $testRunSucceeded $test.filter
    $success = $testRunSucceeded -and $success
}

Set-TimeZone -Id $originalTZ.Id
$currentTZ = Get-TimeZone
Write-Host "Changing TimeZone back to original. Now '$currentTZ'"

if (-not $success) { exit 1 }