using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AnalysisServices;
using Azure.Identity;
using Azure.Core;
using System.Data;

namespace FabricDemoApp.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<IndexModel> _logger;

        public string ConnectionString { get; set; } = string.Empty;
        public string TokenInfo { get; set; } = string.Empty;
        public string CredentialType { get; set; } = string.Empty;
        public string TotalNextFY { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = false;

        public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task OnGet()
        {
            await LoadFabricDataAsync();
        }

        public async Task OnPost()
        {
            await LoadFabricDataAsync();
        }

        private async Task<(Azure.Core.AccessToken token, string credType)> GetFabricTokenAsync()
        {
            var tokenRequestContext = new TokenRequestContext(
                new[] { "https://analysis.windows.net/powerbi/api/.default" });

            // Try each credential type to identify which one is being used
            var credentialTypes = new (Azure.Core.TokenCredential cred, string name)[] {
                (new ManagedIdentityCredential(), "🔷 Managed Identity (Azure)"),
                (new AzureCliCredential(), "💻 Azure CLI"),
                (new AzureDeveloperCliCredential(), "👷 Azure Developer CLI (azd)"),
                (new VisualStudioCredential(), "🎨 Visual Studio"),
                (new AzurePowerShellCredential(), "⚡ Azure PowerShell")
            };

            foreach (var (cred, name) in credentialTypes)
            {
                try
                {
                    var token = await cred.GetTokenAsync(tokenRequestContext, CancellationToken.None);
                    CredentialType = name;
                    _logger.LogInformation("Token acquired using: {CredentialType}", name);
                    return (token, name);
                }
                catch
                {
                    // Try next credential type
                }
            }

            throw new Exception("No credential source available. Run 'az login' or enable Managed Identity.");
        }

        private async Task LoadFabricDataAsync()
        {
            try
            {
                // Get connection string from appsettings.json
                ConnectionString = _configuration.GetConnectionString("FabricModel") ?? string.Empty;

                if (string.IsNullOrEmpty(ConnectionString))
                {
                    ErrorMessage = "Connection string 'FabricModel' not found in appsettings.json";
                    return;
                }

                // Step 1: Acquire Azure token
                TokenInfo = "🔄 Attempting to acquire token...";
                Azure.Core.AccessToken azureToken;
                try
                {
                    var (token, credType) = await GetFabricTokenAsync();
                    azureToken = token;
                    CredentialType = credType;
                    TokenInfo = $"✅ Token acquired via {credType}! Expires: {azureToken.ExpiresOn:yyyy-MM-dd HH:mm:ss UTC}";
                    _logger.LogInformation("Token acquired successfully using {CredType}. Expires: {ExpiresOn}", credType, azureToken.ExpiresOn);
                }
                catch (Exception tokenEx)
                {
                    TokenInfo = $"❌ Token acquisition FAILED: {tokenEx.Message}";
                    ErrorMessage = $"Failed to acquire token: {tokenEx.GetType().Name}: {tokenEx.Message}\n\n" +
                        "💡 Run 'az login' in terminal to authenticate with Azure CLI";
                    _logger.LogError(tokenEx, "Token acquisition failed");
                    return;
                }

                // Connect to Fabric and run DAX query
                using (AdomdConnection conn = new AdomdConnection(ConnectionString))
                {
                    // Set the access token before opening connection
                    conn.AccessToken = new Microsoft.AnalysisServices.AccessToken(azureToken.Token, azureToken.ExpiresOn);
                    
                    conn.Open();
                    _logger.LogInformation("Connection opened successfully");

                    // DAX query against the model's actual 'Table'.
                    // The semantic model has a single table named 'Table' with raw columns
                    // Column1..Column6 (Item, Category, Region, NextFY, CurrentFY, PreviousFY).
                    // Row 1 contains literal header text, so filter it out and cast Column4 to a number.
                    string daxQuery = @"EVALUATE ROW(""TotalNextFY"", SUMX(FILTER('Table', NOT(ISBLANK([Column4])) && [Column4] <> ""NextFY""), VALUE([Column4])))";
                    
                    AdomdDataAdapter adapter = new AdomdDataAdapter(daxQuery, conn);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        // ADOMD wraps ROW() column names in brackets, matching the IndexDataTable page.
                        decimal total = Convert.ToDecimal(dt.Rows[0]["[TotalNextFY]"]);
                        TotalNextFY = total.ToString("N2");
                        IsSuccess = true;
                        _logger.LogInformation("Query executed successfully. Total: {Total}", TotalNextFY);
                    }
                    else
                    {
                        ErrorMessage = "Query returned no data";
                        _logger.LogWarning("Query returned no rows");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                _logger.LogError(ex, "Error loading Fabric data");
                
                // Add more detailed error info for common issues
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    ErrorMessage += "\n\n💡 Tips:\n" +
                        "- Ensure managed identity is added to Fabric workspace with Member/Admin role\n" +
                        "- Check tenant settings allow service principals\n" +
                        "- Wait 10-15 minutes after permission changes\n" +
                        "- Locally: Run 'az login' to authenticate with Azure CLI";
                }
            }
        }
    }
}
