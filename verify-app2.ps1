$ErrorActionPreference = 'Stop'

# 1. Confirm vnetRouteAllEnabled on the actual config/web sub-resource
Write-Host "=== siteConfig/web (canonical location) ==="
$cfgUri = "https://management.azure.com/subscriptions/6dad7b6f-2e7c-4911-aa3e-2ac5885ee0e8/resourceGroups/rg-test-group/providers/Microsoft.Web/sites/360-app-fabric-demo/config/web?api-version=2024-04-01"
$cfg = az.cmd rest --method GET --uri $cfgUri --output json | ConvertFrom-Json
[PSCustomObject]@{
  vnetRouteAllEnabled = $cfg.properties.vnetRouteAllEnabled
  ipSecurityRestrictionsAllowed = $cfg.properties.ipSecurityRestrictionsDefaultAction
  publicNetworkAccess = $cfg.properties.publicNetworkAccess
} | Format-List

# 2. Hit each page and dump the rendered text content (strip HTML)
$base = "https://360-app-fabric-demo-cpdvg8evc4gza2bc.westus2-01.azurewebsites.net"
foreach ($p in @("/IndexDataTable", "/IndexRestApi", "/Index")) {
  Write-Host ""
  Write-Host "=== GET $base$p ==="
  try {
    $resp = Invoke-WebRequest -Uri "$base$p" -UseBasicParsing -TimeoutSec 60
    Write-Host "HTTP $($resp.StatusCode), $($resp.RawContentLength) bytes"
    # Save raw HTML for offline inspection
    $safe = $p.Trim('/').Replace('/', '_')
    if ([string]::IsNullOrWhiteSpace($safe)) { $safe = 'root' }
    $resp.Content | Set-Content "$env:TEMP\fabric_$safe.html" -Encoding utf8

    # Extract visible text by stripping tags and collapsing whitespace
    $text = ($resp.Content -replace '(?is)<script.*?</script>', '' `
                            -replace '(?is)<style.*?</style>', '' `
                            -replace '<[^>]+>', ' ' `
                            -replace '\s+', ' ').Trim()
    # Print snippets relevant to status / error / total
    Write-Host "--- visible text (first 1500 chars) ---"
    Write-Host ($text.Substring(0, [Math]::Min(1500, $text.Length)))
  } catch {
    Write-Host "Request failed: $($_.Exception.Message)"
  }
}
