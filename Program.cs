using System.Net;
using System.Net.Sockets;
using FabricDemoApp.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Bind Fabric options
builder.Services.Configure<FabricOptions>(
    builder.Configuration.GetSection(FabricOptions.SectionName));

var fabricOptions = builder.Configuration
    .GetSection(FabricOptions.SectionName)
    .Get<FabricOptions>() ?? new FabricOptions();

// Add services to the container.
builder.Services.AddRazorPages();

// Named HttpClient targeting the Fabric / Power BI REST API.
// When PrivateEndpoint.Enabled is true we:
//  - Optionally bypass any system / corporate proxy (common in VNet-integrated
//    App Service so traffic to the private endpoint isn't routed out via a proxy).
//  - Optionally pin the destination IP via ConnectCallback (host name is still
//    used for SNI / Host header so the TLS cert validates correctly).
builder.Services.AddHttpClient("FabricApi", client =>
{
    client.BaseAddress = new Uri(fabricOptions.ApiBaseUrl);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new SocketsHttpHandler();
    var pe = fabricOptions.PrivateEndpoint;

    if (pe.Enabled && pe.BypassSystemProxy)
    {
        handler.UseProxy = false;
        handler.Proxy = null;
    }

    if (pe.Enabled && !string.IsNullOrWhiteSpace(pe.OverrideIp))
    {
        var pinnedIp = IPAddress.Parse(pe.OverrideIp);
        handler.ConnectCallback = async (context, ct) =>
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(pinnedIp, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };
    }

    return handler;
});

// Keep a default HttpClient available for any other consumers.
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

// Redirect root to IndexDataTable
app.MapGet("/", () => Results.Redirect("/IndexDataTable"));

app.Run();
