$url = "https://360-app-fabric-demo-cpdvg8evc4gza2bc.westus2-01.azurewebsites.net/Index"
Start-Sleep -Seconds 15
for ($i = 1; $i -le 6; $i++) {
  try {
    $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60
    $html = $r.Content
    Write-Host "--- Attempt $i (status $($r.StatusCode)) ---"
    $hits = $html -split "`n" |
      Select-String -Pattern 'Total|Connection|Error|Cannot|TotalNextFY|Successful|Failed' -SimpleMatch |
      Select-Object -First 30
    foreach ($h in $hits) { Write-Host ($h.Line.Trim()) }
    if ($html -match 'TotalNextFY|Connection Successful|Cannot find table|Failed') { break }
  } catch {
    Write-Host "Attempt $i failed:" $_.Exception.Message
  }
  Start-Sleep -Seconds 10
}
