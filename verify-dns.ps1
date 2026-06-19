$ErrorActionPreference = 'Stop'
$site = "360-app-fabric-demo"
$tok  = az.cmd account get-access-token --resource https://management.azure.com -o tsv --query accessToken
$body = '{"command":"nslookup api.powerbi.com \u0026 nslookup api.privatelink.analysis.windows.net \u0026 nslookup app.analysis.windows.net","dir":"site\\wwwroot"}'
try {
  $resp = Invoke-RestMethod -Uri "https://$site.scm.azurewebsites.net/api/command" -Method Post `
    -Headers @{ Authorization = "Bearer $tok"; "Content-Type" = "application/json" } `
    -Body $body -TimeoutSec 90
  "=== STDOUT ==="
  $resp.Output
  "=== STDERR ==="
  $resp.Error
  "=== EXITCODE ==="
  $resp.ExitCode
} catch {
  "ERROR: $($_.Exception.Message)"
  if ($_.Exception.Response) { "Status: $($_.Exception.Response.StatusCode.value__)" }
}
