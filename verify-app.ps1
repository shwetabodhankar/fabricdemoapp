$ErrorActionPreference = 'Stop'

Write-Host "Checking webapp state..."
$site = az.cmd resource show --resource-group rg-test-group --name 360-app-fabric-demo --resource-type sites --namespace Microsoft.Web --output json | ConvertFrom-Json
[PSCustomObject]@{
  state                 = $site.properties.state
  enabled               = $site.properties.enabled
  publicNetworkAccess   = $site.properties.publicNetworkAccess
  defaultHostName       = $site.properties.defaultHostName
  vnetSubnet            = $site.properties.virtualNetworkSubnetId
  vnetRouteAllEnabled   = $site.properties.siteConfig.vnetRouteAllEnabled
  outboundAllTraffic    = $site.properties.outboundVnetRouting.allTraffic
} | Format-List

Write-Host ""
Write-Host "Pinging public app URL..."
try {
  $resp = Invoke-WebRequest -Uri "https://$($site.properties.defaultHostName)/IndexDataTable" -UseBasicParsing -TimeoutSec 60
  Write-Host ("HTTP {0} ({1} bytes)" -f $resp.StatusCode, $resp.RawContentLength)
  $body = $resp.Content
  $errMatch = [regex]::Match($body, '(?s)<div class="error-section">(.*?)</div>')
  $okMatch  = [regex]::Match($body, '(?s)Total[^<]*<[^>]*>([\d,\.]+)')
  if ($errMatch.Success) {
    Write-Host "ERROR PANEL on page:"
    ($errMatch.Value -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim() | Write-Host
  } elseif ($okMatch.Success) {
    Write-Host ("Total rendered: " + $okMatch.Groups[1].Value)
  } else {
    $snippet = $body.Substring(0, [Math]::Min(800, $body.Length))
    Write-Host "Page returned, no obvious error panel. First 800 chars:"
    Write-Host $snippet
  }
} catch {
  Write-Host "Request failed: $($_.Exception.Message)"
}
