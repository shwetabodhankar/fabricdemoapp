namespace FabricDemoApp.Configuration;

/// <summary>
/// Options controlling how the app talks to the Microsoft Fabric / Power BI
/// REST API. Bound from the "Fabric" section of configuration.
/// </summary>
public class FabricOptions
{
    public const string SectionName = "Fabric";

    /// <summary>Base URL of the Fabric / Power BI REST API.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.powerbi.com/";

    /// <summary>OAuth scope used to acquire an access token.</summary>
    public string TokenScope { get; set; } = "https://analysis.windows.net/powerbi/api/.default";

    /// <summary>Fabric workspace (group) name.</summary>
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>Semantic model (dataset) name.</summary>
    public string DatasetName { get; set; } = string.Empty;

    /// <summary>Private endpoint specific settings.</summary>
    public PrivateEndpointOptions PrivateEndpoint { get; set; } = new();
}

public class PrivateEndpointOptions
{
    /// <summary>
    /// When true the HTTP client is configured for private-endpoint usage
    /// (proxy bypass and optional IP pinning).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Bypass any system / corporate proxy. Required in most VNet-integrated
    /// App Service scenarios so traffic to the private endpoint does not get
    /// routed out to the internet via the proxy.
    /// </summary>
    public bool BypassSystemProxy { get; set; } = true;

    /// <summary>
    /// Optional. Host name the public URL should be rewritten to (e.g.
    /// "api.privatelink.analysis.windows.net"). Leave null to keep DNS-based
    /// resolution via the private DNS zone.
    /// </summary>
    public string? OverrideHost { get; set; }

    /// <summary>
    /// Optional. Pin outbound connections to this IP address. Useful when the
    /// private DNS zone is not yet wired up. The original host name is still
    /// sent for SNI / Host header so TLS validation succeeds.
    /// </summary>
    public string? OverrideIp { get; set; }
}
