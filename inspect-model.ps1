$ErrorActionPreference = 'Stop'

$workspaceName = "360_poc"
$datasetName   = "RG_TestModel"

# 1. Get token for Power BI
$tok = az.cmd account get-access-token --resource "https://analysis.windows.net/powerbi/api" -o tsv --query accessToken
$h   = @{ Authorization = "Bearer $tok"; "Content-Type" = "application/json" }

# 2. Find workspace + dataset IDs
$ws = (Invoke-RestMethod -Uri "https://api.powerbi.com/v1.0/myorg/groups" -Headers $h).value |
      Where-Object { $_.name -ieq $workspaceName } | Select-Object -First 1
if (-not $ws) { throw "Workspace '$workspaceName' not found" }

$ds = (Invoke-RestMethod -Uri "https://api.powerbi.com/v1.0/myorg/groups/$($ws.id)/datasets" -Headers $h).value |
      Where-Object { $_.name -ieq $datasetName } | Select-Object -First 1
if (-not $ds) { throw "Dataset '$datasetName' not found" }

Write-Host "Workspace=$($ws.id)  Dataset=$($ds.id)"

function Run-Dax {
  param([string]$Dax)
  $body = @{ queries = @(@{ query = $Dax }); serializerSettings = @{ includeNulls = $true } } | ConvertTo-Json -Depth 5
  try {
    $r = Invoke-RestMethod -Uri "https://api.powerbi.com/v1.0/myorg/groups/$($ws.id)/datasets/$($ds.id)/executeQueries" -Method Post -Headers $h -Body $body
    return ,@{ ok = $true; rows = $r.results[0].tables[0].rows }
  } catch {
    $msg = if ($_.ErrorDetails) { $_.ErrorDetails.Message } else { $_.Exception.Message }
    return ,@{ ok = $false; err = $msg }
  }
}

foreach ($candidate in @('RG_TestModel','Table','RnO ItemDetails')) {
  Write-Host ""
  Write-Host "=== TOPN(3, '$candidate') ==="
  $res = Run-Dax "EVALUATE TOPN(3, '$candidate')"
  if ($res.ok) {
    $res.rows | Format-Table -AutoSize
  } else {
    $short = $res.err
    if ($short -and $short.Length -gt 400) { $short = $short.Substring(0,400) + "..." }
    Write-Host "FAILED: $short"
  }
}

Write-Host ""
Write-Host "=== SUM of NextFY (Column4) skipping header row ==="
$dax = @'
EVALUATE ROW(
  "TotalNextFY",
  SUMX(
    FILTER('Table', NOT(ISBLANK([Column4])) && [Column4] <> "NextFY"),
    VALUE([Column4])
  )
)
'@
$res = Run-Dax $dax
if ($res.ok) {
  $res.rows | Format-Table -AutoSize
} else {
  Write-Host "FAILED: $($res.err)"
}
