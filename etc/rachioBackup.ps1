# This script is obsolete, you should run the RachioTools.ConsoleApp instead.
# Run this process at the end of a season.

# https://rachio.readme.io/reference/getting-started
# Note that rate limit is 1,700 calls per day, or just over one per minute.

[CmdletBinding()]
param (
  [Parameter(Mandatory = $false)]
  [string]$ApiKey
)

# If no ApiKey is provided, try to read it from appsettings.Production.json
if ([string]::IsNullOrEmpty($ApiKey)) {
  $appsettingsPath = Join-Path $PSScriptRoot '..\src\RachioTools.ConsoleApp\appsettings.Production.json'

  if (Test-Path $appsettingsPath) {
    try {
      $appsettings = Get-Content $appsettingsPath | ConvertFrom-Json
      $ApiKey = $appsettings.RachioApi.ApiKey

      if ([string]::IsNullOrEmpty($ApiKey)) {
        Write-Error 'ApiKey not found in appsettings.Production.json'
        return
      }

      Write-Host 'Using ApiKey from appsettings.Production.json'
    } catch {
      Write-Error "Failed to read ApiKey from appsettings.Production.json: $($_.Exception.Message)"
      return
    }
  } else {
    Write-Error "No ApiKey provided and appsettings.Production.json not found at: $appsettingsPath"
    return
  }
}

$headers = @{
  'Authorization' = "Bearer $ApiKey"
  'Accept'        = 'application/json'
}

$personInfo = Invoke-RestMethod -Uri 'https://api.rach.io/1/public/person/info' -Method GET -Headers $headers
[string]$personId = $personInfo.id

if ($null -eq $personId) {
  Write-Error 'Unable to authenticate'
  return
}

$person = Invoke-RestMethod -Uri "https://api.rach.io/1/public/person/$personId" -Method GET -Headers $headers
$person | ConvertTo-Json -Depth 100 | Out-File "./rachio-person.$personId.json"

$devices = $person.devices

# Get the past events of a device (can only do one month at a time)
$allEvents = @()

foreach ($device in $devices) {
  $endTime = [DateTimeOffset]::Now
  $startTime = $endTime.AddMonths(-1)

  while ($startTime.ToUnixTimeMilliseconds() -ge $device.createDate) {
    $events = Invoke-RestMethod -Uri "https://api.rach.io/1/public/device/$($device.id)/event?startTime=$($startTime.ToUnixTimeMilliseconds())&endTime=$($endTime.ToUnixTimeMilliseconds())" -Method GET -Headers $headers
    $allEvents += $events

    Write-Host "Found $($events.Count) events for the month of $($startTime)"

    $endTime = $startTime.AddMilliseconds(-1)
    $startTime = $endTime.AddMonths(-1)
  }
}

$allEvents | ConvertTo-Json -Depth 100 | Out-File "./rachio-events.$personId.json"

# Use the following to import to a DB
# Keep the json just in case.

# gc -raw .\rachio-events.*.json | convertfrom-Json | select -property Id, DeviceId, Category, Type, EventDate, Summary, SubType, Hidden, Topic | export-csv events.csv

# Import the csv as a flat file into the database as a new table. Use the following to merge. Drop table.
# SELECT [Id]
#       ,[DeviceId]
#       ,[Category]
#       ,[Type]
#       ,[EventDate]
#       ,[Summary]
#       ,[SubType]
#       ,[Hidden]
#       ,[Topic]
#   FROM [Rachio].[dbo].[Events]

#   Insert into [Rachio].[dbo].[Events] ([Id]
#       ,[DeviceId]
#       ,[Category]
#       ,[Type]
#       ,[EventDate]
#       ,[Summary]
#       ,[SubType]
#       ,[Hidden]
#       ,[Topic])
# Select [Id]
#       ,[DeviceId]
#       ,[Category]
#       ,[Type]
#       ,[EventDate]
#       ,[Summary]
#       ,[SubType]
#       ,[Hidden]
#       ,[Topic]
# From [Rachio].[dbo].[Eventsnew]

# where Id not in (select ID from Events)
