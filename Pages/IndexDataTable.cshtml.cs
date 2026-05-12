using AzureCore = Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;

namespace FabricDemoApp.Pages
{
    public class IndexDataTableModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<IndexDataTableModel> _logger;

        public IndexDataTableModel(IConfiguration configuration, ILogger<IndexDataTableModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string? TotalNextFY { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ConnectionString { get; set; }
        public string? TokenInfo { get; set; }
        public string? CredentialType { get; set; }

        private async Task<(AzureCore.AccessToken token, string credType)> GetFabricTokenAsync()
        {
            var tokenRequestContext = new AzureCore.TokenRequestContext(
                new[] { "https://analysis.windows.net/powerbi/api/.default" });

            // Try each credential type to identify which one is being used
            var credentialTypes = new (AzureCore.TokenCredential cred, string name)[] {
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

        public async Task OnGet()
        {
            await LoadFYAsync();
        }

        private async Task LoadFYAsync()
        {
            try
            {
                // Step 1: Acquire Azure token
                TokenInfo = "🔄 Attempting to acquire token...";
                AzureCore.AccessToken azureToken;
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

                string connection = _configuration.GetConnectionString("FabricModel") ?? string.Empty;
                ConnectionString = "Connection: " + connection;
                _logger.LogInformation("The connection is: {Connection}", connection);

                using (AdomdConnection conn = new AdomdConnection(connection))
                {
                    // Using the pattern from the shared code - hardcoded 1 hour expiration
                    conn.AccessToken = new Microsoft.AnalysisServices.AccessToken(azureToken.Token, DateTimeOffset.UtcNow.AddHours(1));

                    conn.Open();
                    _logger.LogInformation("Connection opened successfully");

                    TotalNextFY = "0.00";

                    // Using correct table name 'Table' instead of 'RnO ItemDetails'
                    string daxQuery = @"EVALUATE ROW(""TotalRows"", COUNTROWS('Table'))";

                    // Using DataAdapter and DataTable pattern from the shared code
                    AdomdDataAdapter adapter = new AdomdDataAdapter(daxQuery, conn);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        TotalNextFY = Convert.ToDecimal(dt.Rows[0]["[TotalRows]"]).ToString("N0");
                        _logger.LogInformation("Query executed successfully. Total rows: {TotalRows}", TotalNextFY);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Error in LoadFYAsync");
            }
        }
    }
}
