$ErrorActionPreference = 'Stop'

$bodyPath = "$env:TEMP\siteconfig.json"
'{ "properties": { "vnetRouteAllEnabled": true } }' | Set-Content -Path $bodyPath -Encoding utf8

$uri = "https://management.azure.com/subscriptions/6dad7b6f-2e7c-4911-aa3e-2ac5885ee0e8/resourceGroups/rg-test-group/providers/Microsoft.Web/sites/360-app-fabric-demo/config/web?api-version=2024-04-01"

Write-Host "PATCH $uri"
az.cmd rest --method PATCH --uri $uri --body "@$bodyPath" --output json | Set-Content "$env:TEMP\siteconfig_resp.json" -Encoding utf8

Write-Host "--- response (vnetRouteAllEnabled) ---"
Get-Content "$env:TEMP\siteconfig_resp.json" | ConvertFrom-Json |
    Select-Object -ExpandProperty properties |
    Select-Object vnetRouteAllEnabled |
    ConvertTo-Json
