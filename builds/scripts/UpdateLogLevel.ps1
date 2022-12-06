# Update the default logging level for all host.json files to the value specified in the AFSQLEXT_TEST_LOGLEVEL environment variable

Write-Host Setting logLevel.default to $ENV:AFSQLEXT_TEST_LOGLEVEL
# Ignore bin/target output folders - we only want to update the src files
Get-ChildItem -Recurse -Filter host.json | Where-Object {$_.DirectoryName -notmatch "bin|target"} |
    ForEach-Object {
        Write-Host Updating $_.FullName...
        $json = Get-Content $_.FullName -raw | ConvertFrom-Json
        $json.logging.logLevel.default = $ENV:AFSQLEXT_TEST_LOGLEVEL
        # Default depth is only 2, so to ensure we write content correctly set depth of 32
        $json | ConvertTo-Json -depth 32 | Set-Content $_.FullName
    }