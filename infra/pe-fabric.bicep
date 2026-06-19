// Creates the Fabric/Power BI tenant-level private link service registration
// (Microsoft.PowerBI/privateLinkServicesForPowerBI) and an associated Private
// Endpoint NIC in the consumer VNet using the *manual* approval flow.
// A Fabric/Power BI tenant admin then approves it from the Fabric admin portal.

@description('Azure region for the private endpoint NIC. Must match the VNet region.')
param location string = resourceGroup().location

@description('Name of the private endpoint.')
param privateEndpointName string = 'pe-fabric'

@description('Name of the Microsoft.PowerBI/privateLinkServicesForPowerBI resource.')
param privateLinkServiceName string = 'pls-fabric'

@description('Resource ID of the subnet hosting the private endpoint NIC.')
param subnetId string

@description('Entra ID tenant GUID that owns the Fabric tenant.')
param fabricTenantId string

@description('Optional message shown to the Fabric admin during approval.')
param requestMessage string = 'Fabric PE for 360-app-fabric-demo'

@description('Resource group containing the Fabric Private DNS zones (defaults to current resource group).')
param privateDnsZoneResourceGroup string = resourceGroup().name

@description('Names of the Fabric/Power BI Private DNS zones to register the PE NIC IPs in.')
param fabricPrivateDnsZoneNames array = [
  'privatelink.analysis.windows.net'
  'privatelink.pbidedicated.windows.net'
  'privatelink.tds.pbidedicated.windows.net'
  'privatelink.dfs.fabric.microsoft.com'
  'privatelink.prod.powerquery.microsoft.com'
]

// Tenant-level Power BI / Fabric private link service. This is the resource
// the Fabric admin must approve in the Fabric admin portal once created.
resource pls 'Microsoft.PowerBI/privateLinkServicesForPowerBI@2020-06-01' = {
  name: privateLinkServiceName
  location: 'global'
  properties: {
    tenantId: fabricTenantId
  }
}

resource pe 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: privateEndpointName
  location: location
  properties: {
    subnet: {
      id: subnetId
    }
    manualPrivateLinkServiceConnections: [
      {
        name: 'fabric-conn'
        properties: {
          privateLinkServiceId: pls.id
          groupIds: [
            'tenant'
          ]
          requestMessage: requestMessage
        }
      }
    ]
  }
}

// Reference the existing Fabric Private DNS zones so we can attach the PE NIC
// to them. The PE will auto-register A records for each FQDN reported by the
// service into the matching zone after the connection is approved.
resource fabricPrivateDnsZones 'Microsoft.Network/privateDnsZones@2020-06-01' existing = [for zoneName in fabricPrivateDnsZoneNames: {
  name: zoneName
  scope: resourceGroup(privateDnsZoneResourceGroup)
}]

resource peDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  parent: pe
  name: 'fabric-zone-group'
  properties: {
    privateDnsZoneConfigs: [for (zoneName, i) in fabricPrivateDnsZoneNames: {
      name: replace(zoneName, '.', '-')
      properties: {
        privateDnsZoneId: fabricPrivateDnsZones[i].id
      }
    }]
  }
}

output privateLinkServiceId string = pls.id
output privateEndpointId string = pe.id
output nicId string = pe.properties.networkInterfaces[0].id
output privateDnsZoneGroupId string = peDnsZoneGroup.id
