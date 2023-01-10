param (
  [string]$buildNumber,
  [string]$artifactDirectory,
  [bool]$skipAssemblySigning = $false
)

if ($null -eq $buildNumber) {
  throw 'Parameter $buildNumber cannot be null or empty. Exiting script.'
}

if (-not (Test-Path $artifactDirectory)) {
  throw "Artifact directory '$artifactDirectory' not found. Exiting script."
}

$toSignPattern = Join-Path -Path $artifactDirectory -ChildPath "*"
$toSignZipPath = Join-Path -Path $artifactDirectory -ChildPath "tosign.zip"

Write-Host "Searching for files with path matching pattern: $toSignPattern"
$items = Get-ChildItem -Path $toSignPattern -Recurse
Write-Host $items
Write-Host "$($items.Count) items found."

Compress-Archive -Path $toSignPattern -DestinationPath $toSignZipPath
Write-Host "Signing payload created at: $toSignZipPath"

if ($skipAssemblySigning) {
  "Assembly signing disabled. Skipping signing process."
  exit 0;
}

Write-Host "Uploading signing job '$buildNumber.zip' to storage."
# This will fail if the artifacts already exist.
$ctx = New-AzureStorageContext -StorageAccountName $env:FILES_ACCOUNT_NAME -StorageAccountKey $env:FILES_ACCOUNT_KEY
Set-AzureStorageBlobContent -File $toSignZipPath -Container "azure-webjobs-extensions" -Blob "$buildNumber.zip" -Context $ctx

$queue = Get-AzureStorageQueue -Name "signing-jobs" -Context $ctx

$messageBody = "SignNupkgs;azure-webjobs-extensions;$buildNumber.zip"
$queue.CloudQueue.AddMessage($messageBody)
