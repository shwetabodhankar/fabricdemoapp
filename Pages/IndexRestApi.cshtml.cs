using Microsoft.AspNetCore.Mvc.RazorPages;
using Azure.Identity;
using Azure.Core;
using System.Text;
using System.Text.Json;

namespace FabricDemoApp.Pages
{
    public class IndexRestApiModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<IndexRestApiModel> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public string WorkspaceName { get; set; } = string.Empty;
        public string DatasetName { get; set; } = string.Empty;
        public string TokenInfo { get; set; } = string.Empty;
        public string CredentialType { get; set; } = string.Empty;
        public string TotalNextFY { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = false;

        public IndexRestApiModel(IConfiguration configuration, ILogger<IndexRestApiModel> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task OnGet()
        {
            await LoadFabricDataAsync();
        }

        public async Task OnPost()
        {
            await LoadFabricDataAsync();
        }

        private async Task<(string token, string credType)> GetFabricTokenAsync()
        {
            var tokenRequestContext = new TokenRequestContext(
                new[] { "https://analysis.windows.net/powerbi/api/.default" });

            // Try each credential type to identify which one is being used
            var credentialTypes = new (TokenCredential cred, string name)[] {
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
                    TokenInfo = $"✅ Token acquired via {name}! Expires: {token.ExpiresOn:yyyy-MM-dd HH:mm:ss UTC}";
                    _logger.LogInformation("Token acquired using: {CredentialType}", name);
                    return (token.Token, name);
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
                // Parse workspace and dataset from configuration
                WorkspaceName = "360_poc";
                DatasetName = "RG_TestModel";

                // Acquire Azure token
                var (accessToken, credType) = await GetFabricTokenAsync();
                _logger.LogInformation("Token acquired successfully using {CredType}", credType);

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // Get workspace ID
                var workspacesResponse = await client.GetAsync("https://api.powerbi.com/v1.0/myorg/groups");
                workspacesResponse.EnsureSuccessStatusCode();
                
                var workspacesJson = await workspacesResponse.Content.ReadAsStringAsync();
                using var workspacesDoc = JsonDocument.Parse(workspacesJson);
                
                var workspace = workspacesDoc.RootElement
                    .GetProperty("value")
                    .EnumerateArray()
                    .FirstOrDefault(w => w.GetProperty("name").GetString()?.Equals(WorkspaceName, StringComparison.OrdinalIgnoreCase) == true);

                if (workspace.ValueKind == JsonValueKind.Undefined)
                {
                    ErrorMessage = $"Workspace '{WorkspaceName}' not found";
                    return;
                }

                var workspaceId = workspace.GetProperty("id").GetString();
                _logger.LogInformation("Found workspace: {WorkspaceId}", workspaceId);

                // Get dataset ID
                var datasetsResponse = await client.GetAsync($"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets");
                datasetsResponse.EnsureSuccessStatusCode();
                
                var datasetsJson = await datasetsResponse.Content.ReadAsStringAsync();
                using var datasetsDoc = JsonDocument.Parse(datasetsJson);
                
                var dataset = datasetsDoc.RootElement
                    .GetProperty("value")
                    .EnumerateArray()
                    .FirstOrDefault(d => d.GetProperty("name").GetString()?.Equals(DatasetName, StringComparison.OrdinalIgnoreCase) == true);

                if (dataset.ValueKind == JsonValueKind.Undefined)
                {
                    ErrorMessage = $"Dataset '{DatasetName}' not found in workspace '{WorkspaceName}'";
                    return;
                }

                var datasetId = dataset.GetProperty("id").GetString();
                _logger.LogInformation("Found dataset: {DatasetId}", datasetId);

                // Execute DAX query using Power BI REST API
                var daxQuery = @"EVALUATE TOPN(100, 'Table')";
                var queryPayload = new
                {
                    queries = new[]
                    {
                        new { query = daxQuery }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(queryPayload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var queryResponse = await client.PostAsync(
                    $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets/{datasetId}/executeQueries",
                    content);

                if (!queryResponse.IsSuccessStatusCode)
                {
                    var errorContent = await queryResponse.Content.ReadAsStringAsync();
                    ErrorMessage = $"Query failed ({queryResponse.StatusCode}): {errorContent}";
                    _logger.LogError("Query execution failed: {Error}", errorContent);
                    return;
                }

                var resultJson = await queryResponse.Content.ReadAsStringAsync();
                using var resultDoc = JsonDocument.Parse(resultJson);
                
                var results = resultDoc.RootElement.GetProperty("results")[0];
                var tables = results.GetProperty("tables")[0];
                var rows = tables.GetProperty("rows");

                if (rows.GetArrayLength() > 0)
                {
                    var rowCount = rows.GetArrayLength();
                    TotalNextFY = $"{rowCount} rows returned";
                    IsSuccess = true;
                    _logger.LogInformation("Query executed successfully. Row count: {Count}", rowCount);
                }
                else
                {
                    ErrorMessage = "Query returned no data";
                    _logger.LogWarning("Query returned no rows");
                }
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = $"HTTP Error: {ex.Message}";
                _logger.LogError(ex, "HTTP error loading Fabric data");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                _logger.LogError(ex, "Error loading Fabric data");
            }
        }
    }
}
