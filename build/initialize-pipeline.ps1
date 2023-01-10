# Adapted from https://github.com/Azure/azure-functions-host/blob/a4a3ba51fe291c546de0e1f578c7352a83203ca2/build/initialize-pipeline.ps1
$buildReason = $env:BUILD_REASON
$sourceBranch = $env:BUILD_SOURCEBRANCH
$isPr = ($buildReason -eq "PullRequest")

function GetPrTitle() {
  $prTitle = ""
  if ($isPr) {
    $response = Invoke-RestMethod api.github.com/repos/$env:BUILD_REPOSITORY_ID/pulls/$env:SYSTEM_PULLREQUEST_PULLREQUESTNUMBER
    $prTitle = $response.title.ToLowerInvariant()
    Write-Host "Pull request '$prTitle'"
  }
  else 
  {
    Write-Host "Build not triggered by a PR; no title."
  }

  return $prTitle
}

Write-Host "BUILD_REASON: '$buildReason'"
Write-Host "BUILD_SOURCEBRANCH: '$sourceBranch'"

$prTitle = GetPrTitle

$signPackages = $false
if ((-not $isPr) -or ($prTitle.Contains("[pack]"))) {
  Write-Host "Package signing conditions met."
  $signPackages = $true
}

Write-Host "Setting 'SignPackages' to $signPackages"
Write-Host "##vso[task.setvariable variable=SignPackages;isOutput=true]$signPackages"