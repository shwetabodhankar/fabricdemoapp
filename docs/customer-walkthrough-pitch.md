# Customer Walkthrough — ASP.NET Core Web App → Microsoft Fabric over Private Endpoint

> **Audience:** Customer who is new to Azure. Goal: tell a story, not a tour.
> **Duration:** 25–35 minutes (10 min story, 20 min portal click-through, 5 min Q&A)
> **Format:** Each step has **(1) what to click**, **(2) what to point at**, **(3) what to say**.

---

## 🎯 Opening pitch (60 seconds, before opening the portal)

> "Today I'll show you a small web application that talks to a Microsoft Fabric semantic model — and the entire path between the app and Fabric stays on Microsoft's private network. None of that traffic touches the public internet. We'll walk through the eight Azure building blocks that make this possible, from the network up to the app, and then I'll prove the traffic really is private."

Draw this on a whiteboard / slide before clicking anything:

```
   Browser ──HTTPS──▶  [App Service]  ──VNet──▶  [Private Endpoint]  ──Microsoft backbone──▶  Fabric
                            in subnet              in a different subnet
```

> "Eight pieces. We'll see each one in the portal in the order it was built."

---

## 🧭 Suggested walkthrough order (the story)

| # | Resource | Story beat |
|---|---|---|
| 1 | **Resource Group** | "Everything lives in one folder." |
| 2 | **Virtual Network (VNet)** | "We need our own private network in Azure." |
| 3 | **Two Subnets** | "We split the network into two rooms — one for the app, one for the door to Fabric." |
| 4 | **App Service Plan + App Service** | "The compute that runs the website." |
| 5 | **Managed Identity** | "The app's passwordless badge for Fabric." |
| 6 | **VNet Integration** | "We plug the app into our private network." |
| 7 | **Private Endpoint + Power BI PLS** | "We open a private door to Fabric." |
| 7B | **Fabric admin portal settings** | "On the Fabric side, the tenant admin has to unlock the door and grant the badge." |
| 8 | **Five Private DNS Zones** | "We make Fabric's public addresses resolve to private IPs inside our network." |
| 🎬 | **The running app + DNS proof** | "Here's the result. And here's the proof it's actually private." |

---

## Step 1 — The Resource Group (the container)

**Navigate:** Portal → **Resource groups** → `rg-test-group`

**Point at:**
- Region: `westus2` (top of the Overview blade)
- The list of ~10 resources

**Say:**
> "A Resource Group is just a folder. Every Azure resource lives in one. Ours is called `rg-test-group` and it's in the West US 2 region. Everything you'll see today is in this one folder — that's helpful because we can manage cost, permissions, and lifecycle as one unit. If we wanted to tear down the demo, deleting this group would delete all eleven resources in one shot."

**Pro tip to mention:**
> "Notice the resources have different icons — VNets, an App Service, Private DNS zones, a Private Endpoint. Each one we'll open in turn."

---

## Step 2 — The Virtual Network (our private corner of Azure)

**Navigate:** In the RG, click **`vnet-fabric-demo`**

**Point at:**
- **Overview** → Address space `10.30.0.0/16`
- **Subnets** (left blade) → shows `snet-appsvc` and `snet-pe`

**Say:**
> "Azure gives every customer the ability to create their own private network — a VNet — with whatever IP address ranges they want. Ours is `10.30.0.0/16`, which is about 65,000 private IP addresses we can hand out inside Azure. These IPs aren't reachable from the public internet — they're ours, isolated. Think of it as renting a private office building inside Azure's datacenter."

---

## Step 3 — The two subnets (rooms inside the building)

**Navigate:** Still on the VNet → **Subnets**

**Point at the two rows:**

| Subnet | Range | Purpose |
|---|---|---|
| `snet-appsvc` | `10.30.1.0/24` (256 IPs) | App Service lives here |
| `snet-pe` | `10.30.2.0/24` (256 IPs) | Private Endpoint NIC lives here |

**Click into `snet-appsvc`** → point at:
- **Subnet delegation:** `Microsoft.Web/serverFarms`
- **Connected devices:** the App Service's integration NIC

**Click into `snet-pe`** → point at:
- **Private endpoint network policies:** `Disabled`
- **Connected devices:** the Private Endpoint NIC with `10.30.2.4` etc.

**Say:**
> "We split our network into two rooms. The first, `snet-appsvc`, is dedicated to the App Service. You can see it's *delegated* to the `Microsoft.Web/serverFarms` service — that's Azure-speak for 'only App Services can plug into this subnet.' The second, `snet-pe`, holds the Private Endpoint — the actual network card that represents our connection into Fabric. We disabled network policies on it because Private Endpoints require that. Separating these two into different subnets is a best practice: it lets us apply different security rules to each room independently."

---

## Step 4 — The App Service (the website itself)

**Navigate:** RG → **`360-app-fabric-demo`** (App Service)

**Point at the Overview blade:**
- **Default domain:** `360-app-fabric-demo-cpdvg8evc4gza2bc.westus2-01.azurewebsites.net`
- **App Service Plan:** `asp-360-app-fabric-demo`
- **Status:** Running
- **Runtime stack:** .NET 8 (Windows)

**Open the URL in a new tab** to show the running app — just to set context, we'll come back to it.

**Say:**
> "This is the app itself. App Service is Azure's platform-as-a-service for hosting web apps — we hand it our compiled ASP.NET Core code, and Azure runs it, patches the OS, scales it, and gives it a public HTTPS URL. We don't manage VMs. The 'plan' next to it is the SKU — the size and price tier of the underlying compute. This one is a P1v3, which is the cheapest tier that supports the networking features we need."

> "The website itself uses a connection string to Fabric — `powerbi://api.powerbi.com/v1.0/myorg/360_poc` — but the magic isn't in that URL, it's in how we route the traffic that goes *to* that URL. That's what the next four steps are about."

---

## Step 5 — Managed Identity (the passwordless badge)

**Navigate:** Still on the App Service → left blade → **Settings → Identity**

**Point at:**
- **System assigned** tab → Status **On**, Object (principal) ID `5fe5cf96-54ec-4a96-a3e8-876265608260`

**Say:**
> "Before we get to networking, let me show you how the app authenticates to Fabric. The old way would be to put a username and password in a config file — terrible idea, never expires, leaks easily. Instead, Azure gives every App Service a *Managed Identity* — it's an automatically managed Microsoft Entra ID (formerly Azure AD) identity that only this app can use, and the password is rotated for us by Azure every hour. We added this identity as a Member of the Fabric workspace in the Fabric admin portal. No secrets in code, no secrets in config, nothing for us to rotate."

**Optional tangent (only if customer asks):**
> "The DefaultAzureCredential class in the .NET SDK automatically picks up this identity when running in Azure, and falls back to my `az login` credentials when I run the app locally. Same code, no `if (production)` branching."

---

## Step 6 — VNet Integration (plugging the app into the network)

**Navigate:** App Service → left blade → **Settings → Networking**

**Point at the Networking blade overview:**
- **Outbound traffic → Virtual network integration:** `vnet-fabric-demo / snet-appsvc`
- Click **"Virtual network integration"** to drill in
- Show: subnet `snet-appsvc`, **"Outbound internet traffic"** → **VNet routing: Enabled**

**Say:**
> "By default, an App Service sends outbound traffic — like a call to Fabric — straight out Azure's shared public network. We don't want that. So we did two things:"

> "**One**, we plugged the App Service into our VNet. You can see it's connected to `snet-appsvc` here. From the app's perspective, it now lives *inside* our private network."

> "**Two** — and this is the critical setting — we turned on **'Route All Outbound Traffic'**. Without this, only traffic to private IPs (10.x ranges) would go through the VNet; everything else would still go out the public way. With it on, *every single byte* the app sends — including calls to `api.powerbi.com` — is forced into our VNet first. This is the lever that makes the private endpoint actually get used."

**Whiteboard moment:**
> "Picture it like this: the App Service used to have two exits — a back door to our VNet, and a front door to the public internet. We just sealed the front door."

---

## Step 7 — The Private Endpoint (the private door into Fabric)

**Navigate:** RG → **`pe-fabric`** (Private endpoint)

**Point at the Overview blade:**
- **Connection state:** Approved
- **Target resource:** `pls-fabric` (Microsoft.PowerBI/privateLinkServicesForPowerBI)
- **Network interface:** the PE's NIC

**Click "DNS configuration" in the left blade** — this is the most important view of the entire walkthrough:

**Point at the table — you'll see rows like:**

| FQDN | Private IP | Private DNS Zone |
|---|---|---|
| `api.privatelink.analysis.windows.net` | `10.30.2.4` | `privatelink.analysis.windows.net` |
| `app.privatelink.analysis.windows.net` | `10.30.2.6` | `privatelink.analysis.windows.net` |
| `mwc-global.privatelink.analysis.windows.net` | `10.30.2.7` | `privatelink.analysis.windows.net` |
| `onelake.privatelink.pbidedicated.windows.net` | `10.30.2.8` | `privatelink.pbidedicated.windows.net` |
| … | … | … |

**Click "Network interface"** to show the NIC and its private IPs `10.30.2.4` through `10.30.2.11`.

**Say:**
> "This is the star of the show. A Private Endpoint is a network card that lives in *our* subnet, but on the other side of it is a private connection to a Microsoft-managed service — in our case, the Fabric/Power BI service. It's like having a direct, private leased line from our network into Fabric."

> "Eight private IPs in the `10.30.2.x` range — one per Fabric service endpoint. Each of those IPs corresponds to a public Fabric hostname. When the app calls `api.powerbi.com`, the response goes to `10.30.2.4` — which only exists inside our network — and from there it travels over Microsoft's backbone to Fabric. The packet never touches the public internet."

> "The 'Connection state: Approved' note is important. Because Fabric is a tenant-level resource, a Fabric tenant admin had to approve our request to connect. That happens once, in the Fabric admin portal. After that, the door is open."

**Optional, advanced:**
> "Behind this Private Endpoint is a resource called `pls-fabric` — a `Microsoft.PowerBI/privateLinkServicesForPowerBI` resource. Think of it as the registration that ties our tenant's Fabric subscription to a private link target. It's a one-time setup per tenant."

---

## Step 7B — Fabric admin portal settings (the other half of the story)

> Everything so far was on the **Azure side**. None of it works until the Fabric tenant admin flips a few switches on the **Fabric side**. There are three categories: **(a)** turn private endpoints on for the tenant, **(b)** approve our specific PE request, and **(c)** grant the app's Managed Identity permission to use Fabric APIs and the workspace.
>
> All of this is done at <https://app.fabric.microsoft.com> by a user with the **Fabric Administrator** role (or Microsoft 365 Global Admin / Power Platform Admin).

### 7B.1 Open the Fabric admin portal

**Navigate:** <https://app.fabric.microsoft.com> → top-right gear ⚙ → **Admin portal**

**Say:**
> "This is the control plane for the entire Fabric tenant. Workspace creation policies, capacity assignment, tenant-wide security — it all lives here. Only Fabric Admins see this menu item."

### 7B.2 Enable Azure Private Link for the tenant

**Navigate:** Admin portal → **Tenant settings** → search for *"private link"* → **Azure Private Link**

**Toggle:**
- **"Azure Private Link"** → **Enabled**
- (Optional, hardening) **"Block public internet access"** → **Enabled** *only if every consumer of this tenant comes through a Private Endpoint*. Leave **Disabled** for the demo so you can keep using the Fabric portal from your laptop.

**Point at the page — it will show:**
- A **Tenant URI** value like `tenants/<tenantId>` — this is what binds our `pls-fabric` resource to this Fabric tenant.
- A **list of pending/approved private endpoint requests**.

**Say:**
> "This single toggle is the master switch. Without it, every Private Endpoint request from Azure is rejected outright — it doesn't matter how perfect your Bicep is. With it on, requests show up here for approval."
>
> "The second toggle, *Block public internet access*, is the lockdown setting. When it's on, even users on the public internet can't reach Fabric for this tenant — they must come through a Private Endpoint. It's the right answer for production, but we keep it off during the demo so the Fabric portal itself keeps working from your laptop."

### 7B.3 Approve the pending Private Endpoint request

**Navigate:** Same blade → scroll to **"Manage private endpoints"** (or **"Approve pending requests"**)

**Point at the row matching our PE:**
- **Name:** `fabric-conn` (the connection name from our Bicep)
- **Request message:** `Fabric PE for 360-app-fabric-demo`
- **State:** `Pending` → click **Approve**

**Say:**
> "This is the human-in-the-loop moment. Azure created the Private Endpoint and asked Fabric for permission to connect. A Fabric admin has to look at the request and click Approve — that's intentional, because it prevents anyone with Azure access from silently piping data out of Fabric into a network you don't trust."
>
> "Within about 30 seconds of approval, three things happen automatically: the connection state on the Azure side flips from `Pending` to `Approved`, the Private Endpoint NIC gets its eight private IPs, and the five Private DNS zones get populated with A records. All of that happens without us touching anything else."

### 7B.4 Allow Service Principals / Managed Identities to call Fabric APIs

> The App Service's Managed Identity is, from Entra ID's perspective, a service principal. By default, Fabric blocks service principals from calling its APIs — you have to opt-in.

**Navigate:** Admin portal → **Tenant settings** → **Developer settings** section → search for *"service principals"*

**Toggle these on, scoped to a security group containing the MI** (best practice — never "entire organization"):

| Setting | What it does | Required for |
|---|---|---|
| **"Service principals can use Fabric APIs"** | Allows the MI to authenticate against `api.powerbi.com` | The `/IndexRestApi` page + most REST calls |
| **"Service principals can use read-only admin APIs"** | Allows admin-scoped reads | Only if you need admin APIs (not for this demo) |
| **"Allow service principals to create and use profiles"** | Multi-tenant scenarios | Not for this demo |

**Say:**
> "By default, Fabric assumes APIs are called by humans. Our App Service isn't a human — it's a Managed Identity, which Entra ID treats as a service principal. So we have to explicitly tell Fabric: 'yes, this kind of identity is allowed to call APIs.' Best practice is to scope this to a security group containing only the identities that need it, not 'entire organization' — that's a common audit finding."

**Pro tip:**
> "If you turn this on for the first time, give it 10–15 minutes before testing. Tenant settings propagate asynchronously."

### 7B.5 Allow XMLA endpoint connectivity (read/write)

> XMLA is the protocol our `/Index` and `/IndexDataTable` pages use — they talk to the semantic model via ADOMD over TCP 1433. By default, XMLA is read-only or disabled on lower SKUs.

**Navigate:** Admin portal → **Capacity settings** → select the capacity (`fabrictestnttcap`) → **Power BI workloads** → **XMLA endpoint**

**Set:**
- **XMLA endpoint:** `Read` (minimum) or `Read Write` (recommended for tooling like Tabular Editor)

**Also check tenant-wide:** Tenant settings → **Integration settings** → **"Allow XMLA endpoints and Analyze in Excel with on-premises datasets"** → **Enabled**

**Say:**
> "XMLA is the same protocol Excel and Tabular Editor use to talk to a semantic model. It's how the ADOMD library in our .NET app issues DAX queries. There are two switches: one on the *capacity* — the F-SKU or P-SKU compute behind the workspace — and one tenant-wide. Both have to be on."
>
> "If your customer ever gets a `XMLA endpoint is disabled` error from a Fabric/Power BI .NET app, this is the first place to look."

### 7B.6 Add the Managed Identity to the workspace

> Tenant settings allow the MI to *authenticate*; workspace role assignment allows it to *read data*.

**Navigate:** <https://app.fabric.microsoft.com> → workspace **`360_poc`** → top-right **Manage access** → **+ Add people or groups**

**Add:**
- The Managed Identity by its display name: `360-app-fabric-demo`
- Role: **Member** (or **Contributor** — see table below)

| Role | What it grants | When to use |
|---|---|---|
| **Viewer** | Read reports/dashboards | Read-only consumers |
| **Contributor** | Read + write content, run queries | Most apps |
| **Member** | Contributor + manage access | Our demo, because the app may need to refresh datasets |
| **Admin** | Full control | Avoid for apps |

**Say:**
> "In Fabric, permissions on the data itself are workspace-scoped, not tenant-scoped. We have to add the App Service's Managed Identity as a Member of the `360_poc` workspace. Once that's done, the MI can query any semantic model in this workspace — that's how our app gets to `RG_TestModel`."
>
> "For least-privilege production setups, pick Viewer or Contributor instead of Member, and only grant access to the specific items the app needs."

### 7B.7 (Optional, hardening) Block public internet access to Fabric

**Navigate:** Admin portal → **Tenant settings** → **Azure Private Link** → **"Block public internet access"** → **Enabled**

**Say:**
> "This is the ultimate lockdown. With this on, even Fabric's web portal won't load from a browser that isn't coming through a Private Endpoint. Customers typically pair this with a corporate VPN or ExpressRoute that routes everyone through a hub VNet with a Fabric PE. We're leaving it off today so we can keep using the Fabric portal from this demo laptop."
>
> "Important: turning this on without a Private Endpoint in place will lock *you* out too. Always create and approve the PE first, validate it works, then turn this on."

### 7B.8 Quick checklist (give this to the customer's Fabric admin)

| ✅ | Setting | Where |
|---|---|---|
| ☐ | Azure Private Link → **Enabled** | Admin portal → Tenant settings → Azure Private Link |
| ☐ | Approve the pending PE request (`fabric-conn`) | Admin portal → Azure Private Link → Manage private endpoints |
| ☐ | Service principals can use Fabric APIs → **Enabled** for security group `sg-fabric-apps` | Admin portal → Tenant settings → Developer settings |
| ☐ | XMLA endpoint → **Read Write** on the capacity | Admin portal → Capacity settings → Power BI workloads |
| ☐ | "Allow XMLA endpoints…" → **Enabled** | Admin portal → Tenant settings → Integration settings |
| ☐ | Add MI `360-app-fabric-demo` to workspace `360_poc` as **Member** | Workspace → Manage access |
| ☐ | (Optional, prod) Block public internet access → **Enabled** | Admin portal → Tenant settings → Azure Private Link |

---

## Step 8 — The five Private DNS Zones (the address-book trick)

**Navigate:** RG → filter by type **"Private DNS zone"**

**Point at the five zones:**
- `privatelink.analysis.windows.net`
- `privatelink.pbidedicated.windows.net`
- `privatelink.tds.pbidedicated.windows.net`
- `privatelink.dfs.fabric.microsoft.com`
- `privatelink.prod.powerquery.microsoft.com`

**Click into `privatelink.analysis.windows.net`:**
- **Recordsets** → show the A records: `api → 10.30.2.4`, `app → 10.30.2.6`, `mwc-global → 10.30.2.7`
- **Virtual network links** → show `vnet-fabric-demo` is linked

**Say:**
> "Here's the trick that ties it all together. Fabric has dozens of public hostnames — `api.powerbi.com`, `app.powerbi.com`, `onelake.dfs.fabric.microsoft.com`, and so on. We can't change those public addresses, and we don't want to. Instead, we use **Private DNS Zones** — Azure's private DNS service — to override what those names resolve to *inside our VNet*."

> "On the public internet, `api.powerbi.com` resolves to a public IP like `20.42.131.40`. But inside our VNet, the same hostname resolves to `10.30.2.4` — our Private Endpoint. Same name, different answer, depending on who's asking. That's what these five zones do. Fabric uses five different DNS suffixes for its various services, so we need all five zones, each linked to our VNet."

> "And the best part — we didn't have to type any of those IP addresses in by hand. When we created the Private Endpoint, it automatically populated the A records into the matching zones. Pure infrastructure-as-code."

**Point at the Virtual network links blade:**
> "This is the linkage that makes it work. The DNS zone is only consulted by VMs and apps inside `vnet-fabric-demo`. From the public internet, our zones don't exist."

---

## 🎬 Step 9 — The payoff: run the app + prove it's private

### 9a. Show the running app

**Navigate:** Open `https://360-app-fabric-demo-cpdvg8evc4gza2bc.westus2-01.azurewebsites.net/`

**Point at:**
- "Connection Successful! Total Rows in 'Table': 31"
- The token info line showing **Managed Identity (Azure)**

**Say:**
> "The app is querying the Fabric semantic model `RG_TestModel` in the `360_poc` workspace, returning a row count, and rendering it. It's using its Managed Identity — no passwords involved. And every single network packet between this App Service and Fabric is going through the Private Endpoint we just looked at."

**Optionally visit `/Index` and `/IndexRestApi` to show:**
- `/Index` — sum via XMLA: **Total NextFY: 5,120,500.00**
- `/IndexRestApi` — same data via Power BI REST API: 31 rows

> "Three different code paths — XMLA via ADOMD, XMLA returning a DataTable, and the Power BI REST API. All three use the same Private Endpoint."

### 9b. Prove it's actually private (the wow moment)

Open a terminal alongside the portal and run two commands the customer can see:

**From the customer's laptop (public DNS):**
```powershell
Resolve-DnsName api.powerbi.com -Server 8.8.8.8 -DnsOnly | Select Name, IP4Address
# → 20.42.131.40   (a public Azure IP)
```

**From inside the App Service (via Kudu):**
```text
nameresolver api.powerbi.com
# Server:  168.63.129.16  (Azure VNet DNS)
# Name:    api.privatelink.analysis.windows.net
# Address: 10.30.2.4   (our Private Endpoint!)
```

**Say:**
> "Same hostname, two different vantage points. From the public internet, `api.powerbi.com` resolves to a public Azure IP. From inside our App Service — inside our VNet — the *same* hostname resolves to `10.30.2.4`, which is the Private Endpoint NIC IP we just looked at. That `10.30.2.4` address is a private IP — it's unreachable from the public internet. There is no possible code path where this app could talk to Fabric publicly."

> "That's the proof."

---

## 🧱 Step 10 (optional) — Show the Bicep template

**Navigate:** VS Code → `infra/pe-fabric.bicep`

**Point at:**
- The `Microsoft.PowerBI/privateLinkServicesForPowerBI` resource
- The `Microsoft.Network/privateEndpoints` resource with the manual connection
- The `privateDnsZoneGroups` resource that auto-registers DNS records

**Say:**
> "Everything we just walked through — the PE, the DNS bindings, the zone group — is one Bicep file. Bicep is Azure's native infrastructure-as-code language. About 90 lines. Repeatable, version-controlled, reviewable. If you wanted to replicate this in dev, staging, and prod, this template is what you'd deploy three times."

---

## ❓ Anticipated questions & talking points

| Question | Answer |
|---|---|
| "Does this cost more?" | "Private Endpoint is about $0.01/hour (~$7/month) per PE plus 1¢/GB of data. Tiny. VNet, subnets, DNS zones are free or near-free." |
| "Could I do this for SQL / Storage / Key Vault too?" | "Yes — exact same pattern, different `groupId` and different privatelink DNS zone. Most Azure PaaS services support it." |
| "What if the App Service needs to reach the public internet too?" | "It still can — `vnetRouteAllEnabled` sends traffic through the VNet first, but the VNet has internet egress unless you block it. You'd typically add Azure Firewall or a NAT Gateway in the path to control/log egress." |
| "What happens if the Private Endpoint goes down?" | "The PE has SLA equivalent to the underlying VNet. If Microsoft has a regional outage that takes down the PE, Fabric would be affected anyway." |
| "Can I see the actual traffic?" | "Yes — VNet Flow Logs + Log Analytics will show the connection from `snet-appsvc` to `snet-pe`. Out of scope today but easy to enable." |
| "Is the public endpoint of Fabric still reachable?" | "Yes, from anywhere else in the world — Private Endpoint doesn't disable the public endpoint. If you want to lock Fabric down so *only* private connections work, that's a tenant-level setting in Fabric admin portal: 'Block public internet access'." |
| "What's the difference between this and a VPN?" | "VPN connects your *on-prem network* to Azure. Private Endpoint connects your *Azure VNet* to an Azure PaaS service. Both can coexist — VPN gets your office to the VNet, PE gets the VNet to Fabric." |

---

## 📝 Closing pitch (60 seconds)

> "Recap: we built eight Azure resources — one VNet, two subnets, an App Service with Managed Identity, VNet integration, one Private Endpoint, and five Private DNS zones — and now have a web app that queries Microsoft Fabric over a fully private network path. No secrets in code, no public traffic to Fabric, all defined in ~100 lines of Bicep, and it took about an hour end-to-end to build."

> "The same pattern works for Azure SQL, Storage, Key Vault, Service Bus, Cosmos DB — really anything in Azure PaaS. If you want to make 'no app talks to anything over the public internet' a standard for your environment, this is the blueprint."

> "Happy to set this up in your subscription next."

---

## 🗺️ Quick portal navigation cheat sheet (for you during the demo)

| To show | Click path |
|---|---|
| Resource list | Resource groups → `rg-test-group` |
| VNet & subnets | RG → `vnet-fabric-demo` → Subnets |
| App Service | RG → `360-app-fabric-demo` → Overview |
| Managed Identity | App Service → Settings → Identity |
| VNet Integration | App Service → Settings → Networking → Virtual network integration |
| Private Endpoint | RG → `pe-fabric` → Overview |
| **PE DNS configuration (★)** | Private endpoint → DNS configuration |
| **Fabric: enable + approve PE** | <https://app.fabric.microsoft.com> → ⚙ Admin portal → Tenant settings → Azure Private Link |
| **Fabric: allow service principals** | Admin portal → Tenant settings → Developer settings → "Service principals can use Fabric APIs" |
| **Fabric: XMLA endpoint** | Admin portal → Capacity settings → `fabrictestnttcap` → Power BI workloads → XMLA endpoint |
| **Fabric: workspace access** | <https://app.fabric.microsoft.com> → workspace `360_poc` → Manage access |
| Private DNS zone records | RG → `privatelink.analysis.windows.net` → Recordsets |
| Zone-to-VNet link | Private DNS zone → Virtual network links |
| Running app | `https://360-app-fabric-demo-cpdvg8evc4gza2bc.westus2-01.azurewebsites.net/` |

---

## 🛟 Backup plan if anything fails live

- **App page shows error?** Open `/IndexRestApi` — it uses a different code path and is more resilient.
- **DNS proof command fails?** Use the screenshots from `verify-dns.ps1` saved during testing.
- **Tenant admin question about PE approval?** "It's a one-time, ~30-second click in the Fabric admin portal. We did it before this meeting."
- **Portal slow?** Have the Bicep file (`infra/pe-fabric.bicep`) and architecture diagram ready as a fallback.
