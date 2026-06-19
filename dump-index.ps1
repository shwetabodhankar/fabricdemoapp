$url = "https://360-app-fabric-demo-cpdvg8evc4gza2bc.westus2-01.azurewebsites.net/Index"
$r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60
Write-Host "Status: $($r.StatusCode)"
Write-Host "Length: $($r.Content.Length)"
Write-Host "----- BODY (last 4000 chars) -----"
$c = $r.Content
$start = [Math]::Max(0, $c.Length - 4000)
Write-Host $c.Substring($start)
