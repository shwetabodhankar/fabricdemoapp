# Fabric Demo App

ASP.NET Core application demonstrating connection to Microsoft Fabric Semantic Models using Managed Identity.

## 🎯 Features

- ✅ Connects to Fabric/Power BI semantic models
- ✅ Uses DefaultAzureCredential (works locally and in Azure)
- ✅ Executes DAX queries via ADOMD.NET
- ✅ Clean UI showing connection status and results

## 📋 Prerequisites

### Local Development
- .NET 6.0 SDK or later
- Azure CLI (for local authentication)
- Access to Fabric workspace "360_POC" with semantic model "RG_TestModel"

### Azure Deployment
- Azure App Service with Managed Identity enabled
- Managed Identity added to Fabric workspace with Member/Admin role

## 🚀 Running Locally

### 1. Authenticate with Azure CLI
```powershell
az login
```

### 2. Restore packages
```powershell
cd C:\Projects\FabricDemoApp
dotnet restore
```

### 3. Run the application
```powershell
dotnet run
```

### 4. Open in browser
Navigate to: `https://localhost:5001`

## 🔧 Configuration

Edit `appsettings.json` to change the connection string:

```json
{
  "ConnectionStrings": {
    "FabricModel": "Data Source=powerbi://api.powerbi.com/v1.0/myorg/YOUR_WORKSPACE;Initial Catalog=YOUR_MODEL"
  }
}
```

## 📦 Dependencies

- `Microsoft.AnalysisServices.AdomdClient.NetCore.retail.amd64` (19.113.0) - For Fabric/Analysis Services connectivity
- `Azure.Identity` (1.10.0) - For managed identity authentication
- `Azure.Core` (1.35.0) - Azure SDK core functionality

## 🌐 Deploying to Azure

### 1. Publish the application
```powershell
dotnet publish -c Release -o ./publish
```

### 2. Deploy to App Service
```powershell
# Create ZIP
Compress-Archive -Path ./publish/* -DestinationPath deploy.zip -Force

# Deploy
az webapp deployment source config-zip `
  --resource-group rg-test-group `
  --name 360-app-fabric-demo `
  --src deploy.zip
```

### 3. Verify Managed Identity is enabled
```powershell
az webapp identity show --name 360-app-fabric-demo --resource-group rg-test-group
```

### 4. Add Managed Identity to Fabric Workspace
- Go to app.fabric.microsoft.com
- Open workspace "360_POC"
- Settings → Manage access
- Add service principal with Member role

## 🔍 Troubleshooting

### 401 Unauthorized Error
- ✅ Ensure managed identity is added to Fabric workspace
- ✅ Check tenant settings allow service principals
- ✅ Wait 10-15 minutes after permission changes
- ✅ Locally: Run `az login` and ensure you have access to the workspace

### Connection String Issues
- ✅ Ensure no `Authentication=` parameter in connection string
- ✅ Verify workspace name and model name are correct
- ✅ Check table name in DAX query matches your data

### Token Acquisition Fails
- ✅ Locally: Ensure `az login` is completed
- ✅ Azure: Verify managed identity is enabled on App Service
- ✅ Check Azure AD permissions

## 📊 Sample Data

Use the included `RnO_ItemDetails_Sample.csv` to create test data in Fabric:
1. Upload to Fabric workspace
2. Create semantic model named "RG_TestModel"
3. Ensure table is named "RnO ItemDetails"

## 🏗️ Architecture

```
User Browser
    ↓
ASP.NET Core App (Index.cshtml)
    ↓
DefaultAzureCredential (Azure.Identity)
    ↓ (Token Request)
Azure AD
    ↓ (Access Token)
ADOMD.NET Client
    ↓ (DAX Query)
Microsoft Fabric Semantic Model
    ↓ (Results)
Display in UI
```

## 📝 Key Files

- `Program.cs` - Application startup
- `appsettings.json` - Configuration (connection strings)
- `Pages/Index.cshtml` - UI markup
- `Pages/Index.cshtml.cs` - Business logic (PageModel)

## 🔐 Security Notes

- Never commit credentials or tokens to source control
- Connection string contains no sensitive information
- Tokens are acquired at runtime using managed identity
- DefaultAzureCredential automatically selects appropriate credential source

## 📚 Additional Resources

- [Azure Managed Identity](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/)
- [Microsoft Fabric Documentation](https://learn.microsoft.com/fabric/)
- [ADOMD.NET Reference](https://learn.microsoft.com/analysis-services/adomd/)

---

**App Service:** 360-app-fabric-demo  
**Resource Group:** rg-test-group  
**Workspace:** 360_POC  
**Model:** RG_TestModel
