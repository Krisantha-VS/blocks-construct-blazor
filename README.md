# blocks-construct-blazor

A production-ready SELISE Blocks Blazor WASM application with **Interactive Auto** per-page rendering. Built with .NET 10, Tailwind CSS v4, REST APIs, and OIDC authentication.

## Overview

This is a full-stack .NET 10 application showcasing:
- **Blazor WASM** frontend (Interactive Auto rendering mode)
- **ASP.NET Core** backend hosting UI + REST APIs
- **Tailwind CSS v4** for styling (utility-first, no scoped CSS)
- **OIDC authentication** via SELISE Blocks identity
- **Feature-based architecture** with clean separation of concerns
- **Multi-project structure**: Client, Server, Services (shared), Worker, Tests

Perfect for building enterprise applications with modern .NET stack and SELISE Blocks platform integration.

## Technology Stack

| Layer | Technology |
|-------|-----------|
| **Frontend** | Blazor WASM (.NET 10), Tailwind CSS v4 |
| **Backend** | ASP.NET Core 10, REST API (ApiController), Swagger/OpenAPI |
| **Authentication** | OIDC (SELISE Blocks identity) |
| **Data** | GraphQL queries/mutations, S3 file uploads |
| **Testing** | xUnit + bUnit |
| **Deployment** | Docker, Kubernetes |
| **CSS** | Tailwind CSS v4 (only styling method) |

## Prerequisites

- **.NET 10 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Node.js 18+** — Required for Tailwind CSS CLI (optional, can use MSBuild integration)
- **Docker** (optional) — For containerized deployment
- **Git** — For version control

## Quick Start

### 1. Clone and Setup

```bash
git clone https://github.com/SELISEdigitalplatforms/blocks-construct-blazor.git
cd blocks-construct-blazor
```

### 2. Configure Environment

Create a `.env` file in the project root:

```bash
MICROSERVICE_API_BASE_URL=<your_blocks_api_url>
X_BLOCKS_KEY=<your_blocks_api_key>
```

**Get these values from SELISE Blocks Cloud Portal** → Project settings

### 3. Install Dependencies

```bash
dotnet restore
```

### 4. Run the Project

```bash
dotnet watch --project src/Server
```

The application starts at `https://localhost:7075`

## Folder Structure

```
src/
├── Client/                          # Blazor WASM — pages and components
│   ├── Components/
│   │   ├── Shared/                  # Reusable UI components
│   │   └── Forms/                   # Form-specific components
│   └── Pages/
│       ├── Auth/LoginPage.razor
│       ├── Dashboard/DashboardPage.razor
│       └── Home/HomePage.razor
├── Server/                          # Single host for UI and API
│   ├── Components/Layout/           # App.razor, MainLayout, Routes, etc.
│   ├── Controllers/                 # [ApiController] REST endpoints
│   └── Extensions/                  # DI registration (AddApplicationServices)
├── Services/                        # Shared business logic — feature-based
│   └── SalesOrders/
│       ├── ISalesOrderService.cs
│       ├── SalesOrderService.cs
│       └── SalesOrder.cs
├── Test/                            # xUnit tests
│   └── Services/                    # Unit tests per feature
└── Worker/                          # Background service
    └── Jobs/                        # One class per background job
```

## Configuration

The application reads configuration from **environment variables** and optional config values. This approach supports all deployment scenarios: local development, Docker, Kubernetes, and CI/CD.

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `MICROSERVICE_API_BASE_URL` | Microservice API base URL (with protocol) | none |
| `X_BLOCKS_KEY` | Blocks API authentication key | none |

**Resolution order:**
1. Environment variables (highest priority)
2. `appsettings.{Environment}.json` fallback (e.g., `appsettings.Development.json`)
3. No hardcoded URL/key defaults are used

### Local Development (`.env` file)

Create a `.env` file in the project root:

```bash
MICROSERVICE_API_BASE_URL=<your_blocks_api_url>
X_BLOCKS_KEY=<your_blocks_api_key>
```

**Get these from SELISE Blocks Cloud Portal** → Project settings

The server now auto-loads `.env` for local runs. Values provided by real environment variables (Docker/Kubernetes/CI) still take precedence.

**Add `.env` to `.gitignore`:**
```
.env
.env.local
*.local
```

### Docker

Set environment variables in your `docker-compose.yml`:

```yaml
services:
  blocks-server:
    image: blocks-construct:latest
    environment:
      MICROSERVICE_API_BASE_URL: ${MICROSERVICE_API_BASE_URL}  # Your microservice API URL
      X_BLOCKS_KEY: ${BLOCKS_KEY}    # Your SELISE Blocks API key
    ports:
      - "5001:8080"
```

Or run directly:

```bash
docker run \
  -e MICROSERVICE_API_BASE_URL=<your_blocks_api_url> \
  -e X_BLOCKS_KEY=<your_blocks_api_key> \
  -p 5001:8080 \
  blocks-construct
```

### Kubernetes

Use ConfigMap for non-sensitive config and Secrets for API keys.

**configmap.yaml:**
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: blocks-config
data:
  MICROSERVICE_API_BASE_URL: "<your_blocks_api_url>"  # Your microservice API URL
```

**secret.yaml:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: blocks-secret
type: Opaque
stringData:
  X_BLOCKS_KEY: "<your_blocks_api_key>"  # Your SELISE Blocks API key
```

**deployment.yaml:**
```yaml
spec:
  containers:
  - name: blocks-server
    image: blocks-construct:latest
    env:
    - name: MICROSERVICE_API_BASE_URL
      valueFrom:
        configMapKeyRef:
          name: blocks-config
          key: MICROSERVICE_API_BASE_URL
    - name: X_BLOCKS_KEY
      valueFrom:
        secretKeyRef:
          name: blocks-secret
          key: X_BLOCKS_KEY
```

### GitHub Actions

Set secrets in GitHub and use in workflows:

```yaml
env:
  MICROSERVICE_API_BASE_URL: ${{ secrets.MICROSERVICE_API_BASE_URL }}
  X_BLOCKS_KEY: ${{ secrets.BLOCKS_KEY }}
```

## Run the Project

```bash
dotnet watch --project src/Server
```

The app runs on `https://localhost:7075` (or the URL shown in terminal).

### Tailwind CSS Watch Mode (Optional)

Watch for CSS changes and rebuild automatically:

```bash
npm run css:watch
```

## Architecture

### Rendering Strategy: Interactive Auto

This app uses **Interactive Auto** with **per-page** rendering:

- **Every page component** in `src/Client/Pages/` declares `@rendermode InteractiveAuto`
- **Child components** inherit the render mode from their parent — no need to repeat it
- **Layout components** (App.razor, MainLayout) stay SSR-only
- **Prerendering** is enabled by default — components render on server first, then become interactive on client

Example page:
```razor
@page "/dashboard"
@rendermode InteractiveAuto

<h1>Dashboard</h1>
```

### Feature-Based Services

Services are organized by domain, not by type:

```
Services/
├── SalesOrders/
│   ├── ISalesOrderService.cs
│   ├── SalesOrderService.cs
│   └── SalesOrder.cs
```

### Dependency Injection

All services registered in [src/Server/Extensions/ServiceExtensions.cs](src/Server/Extensions/ServiceExtensions.cs):

```csharp
public static IServiceCollection AddApplicationServices(this IServiceCollection services, string webRootPath)
{
    services.AddScoped<ISalesOrderService>(_ => new SalesOrderService(webRootPath));
    return services;
}
```

Inject into components:
```razor
@inject ISalesOrderService SalesOrderService
```

## Authentication & Security

### OIDC Login

App uses OIDC via SELISE Blocks for user authentication.

**Key Components:**
- `AppAuthStateProvider` — Manages auth state
- `AuthTokenHandler` — Injects token into API requests
- `RequireClientAuth` — Guards protected pages

### Protected Pages

```razor
@page "/dashboard"
@rendermode InteractiveAuto

<RequireClientAuth>
    <h1>Dashboard</h1>
</RequireClientAuth>
```

## Styling with Tailwind CSS v4

**Tailwind CSS is the only styling method.** No scoped CSS, no inline styles.

### Rules

✅ Use Tailwind utility classes: `<div class="flex items-center gap-4 p-6 bg-white rounded-lg shadow">`
❌ Don't create `.razor.css` files
❌ Don't use inline `style="..."`  
❌ Don't use other CSS frameworks

### Tailwind Source

Edit [src/Server/wwwroot/app.tailwind.css](src/Server/wwwroot/app.tailwind.css):

```css
@import "tailwindcss";

@theme {
  --color-primary: #15969B;
  --color-secondary: #5194B8;
}

@layer components {
  .btn-primary {
    @apply px-4 py-2 bg-primary text-white rounded hover:opacity-90;
  }
}
```

Build CSS:
```bash
dotnet build  # includes MSBuild Tailwind target
```

## Available Interfaces

| Page | URL | Protected |
|------|-----|-----------|
| Home | `/` | ❌ No |
| Login | `/login` | ❌ No |
| Dashboard | `/dashboard` | ✅ Yes |
| Profile | `/profile` | ✅ Yes |
| IAM | `/iam` | ✅ Yes |
| Inventory | `/inventory` | ✅ Yes |

## API Endpoints

### Swagger UI

`https://localhost:7075/swagger` (Development only)

### Sales Orders

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/sales-orders` | List all sales orders |
| `GET` | `/api/sales-orders/{id}` | Get a single sales order |
| `GET` | `/api/sales-orders/by-status/{status}` | Filter by status |

**Example:**
```bash
curl -H "Authorization: Bearer {token}" \
  https://localhost:7075/api/sales-orders
```

## Testing

### Run Tests

```bash
dotnet test
```

**Unit tests:** `src/Test/Services/`
**Component tests (bUnit):** `src/Test/Pages/`

## Building & Deployment

### Build and Run (Local)

```bash
dotnet restore
dotnet build src/Server/Server.csproj
dotnet run --project src/Server/Server.csproj
```

Hot reload:

```bash
dotnet watch --project src/Server
```

### Development Build

```bash
dotnet build
```

### Release Build

```bash
dotnet publish -c Release -o ./publish src/Server
```

### Docker

```bash
docker build -t blocks-construct .
docker run -e MICROSERVICE_API_BASE_URL=<your_blocks_api_url> \
           -e X_BLOCKS_KEY=<your_blocks_api_key> \
           -p 8080:8080 blocks-construct
```

Using `.env` directly:

```bash
docker build -t blocks-construct .
docker run --env-file .env -p 8080:8080 blocks-construct
```

## Adding Features

### New Page

1. Create file: `src/Client/Pages/Feature/FeaturePage.razor`
   ```razor
   @page "/feature"
   @rendermode InteractiveAuto
   <h1>Feature</h1>
   ```
2. Add tests in `src/Test/Pages/`

### New Service

1. Create folder: `src/Services/Feature/`
   - `IFeatureService.cs`
   - `FeatureService.cs`
   - `Feature.cs` (model)

2. Register in [src/Server/Extensions/ServiceExtensions.cs](src/Server/Extensions/ServiceExtensions.cs)

3. Add tests in `src/Test/Services/`

### New API Endpoint

1. Create file: `src/Server/Controllers/FeatureController.cs`
   ```csharp
   [ApiController]
   [Route("api/feature")]
   public class FeatureController(IFeatureService service) : ControllerBase
   {
       [HttpGet]
       public async Task<ActionResult<IEnumerable<Feature>>> GetAll() =>
           Ok(await service.GetAllAsync());
   }
   ```

2. Endpoint auto-exposed in Swagger

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `NETSDK1045` / dotnet watch fails | Install **.NET 10 SDK** and verify with `dotnet --list-sdks`; this repo is pinned via `global.json` |
| Port 7075 in use | Change port in `launchSettings.json` |
| Tailwind not compiling | Run `npm run css:build` |
| Auth token expired | Clear LocalStorage and log in again |
| CORS errors | Check `MICROSERVICE_API_BASE_URL` and `X_BLOCKS_KEY` env vars |

## Best Practices

✅ Use `@rendermode InteractiveAuto` on every page
✅ Keep services feature-based
✅ Use `@inject` for dependencies (not `new`)
✅ Guard `IJSRuntime` in `OnAfterRenderAsync`
✅ Store secrets in environment variables
✅ Write tests

❌ Don't hardcode secrets
❌ Don't create type-based folders
❌ Don't use multiple CSS frameworks
❌ Don't use scoped `.razor.css` files

## License

MIT License — See [LICENSE](LICENSE)

## Support

- **Issues**: [GitHub Issues](https://github.com/SELISEdigitalplatforms/blocks-construct-blazor/issues)
- **Docs**: [SELISE Blocks](https://blocks.selise.io)

---
