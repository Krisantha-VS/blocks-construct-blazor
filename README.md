# blocks-construct-blazor

A SELISE Blocks Blazor WASM application with Interactive Auto rendering. Built with .NET 10 (Blazor WASM + Server), Tailwind CSS v4, GraphQL, and OIDC authentication.

## Stack

- **Frontend**: Blazor WASM (.NET 10), Tailwind CSS v4 (only CSS framework — no other CSS libraries or scoped CSS)
- **Backend**: ASP.NET Core 10, GraphQL API, REST API (ApiController), Swagger/OpenAPI

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
├── Server/                          # ASP.NET Core host
│   ├── Layout/                      # App.razor, MainLayout, Routes, etc. (SSR only — no Components/ wrapper)
│   ├── Controllers/                 # [ApiController] REST endpoints
│   └── Extensions/                  # DI registration (AddApplicationServices)
├── Services/                        # Shared business logic — feature-based
│   └── SalesOrders/
│       ├── ISalesOrderService.cs
│       ├── SalesOrderService.cs
│       └── SalesOrder.cs
├── Test/                            # xUnit + bUnit tests
│   ├── Pages/                       # bUnit component tests
│   └── Services/                    # Unit tests per feature
└── Worker/                          # Background service
    └── Jobs/                        # One class per background job
```

## Run the Project

```bash
cd src/Server
dotnet watch
```

The app will be available at `https://localhost:5001` (or the port shown in the terminal).

## Available Interfaces

| Interface | URL |
|-----------|-----|
| Home | `https://localhost:5001/` |
| Login | `https://localhost:5001/login` |
| Dashboard | `https://localhost:5001/dashboard` |
| Swagger | `https://localhost:5001/swagger` *(Development only)* |

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/sales-orders` | List all sales orders |
| `GET` | `/api/sales-orders/{id}` | Get a single sales order |
| `GET` | `/api/sales-orders/by-status/{status}` | Filter by status |

## Key Conventions

### Render Mode — Interactive Auto (Per-Page)

Every `@page` component in `src/Client/Pages/` must declare `@rendermode InteractiveAuto` on line 2. Never set a global render mode on `<Routes />`. Non-page components inherit the render mode from their parent page.

### Component Placement

All interactive UI components live in `src/Client/` — never in `src/Server/`. The Server project contains only SSR shell files (`App.razor`, `MainLayout.razor`, `Routes.razor`, etc.) placed in `src/Server/Layout/`.

### Styling

Tailwind CSS v4 utility classes only — no `.razor.css` scoped files, no inline styles, no other CSS frameworks.

### [PersistentState] for SSR → WASM Handoff

For pages that load data in `OnInitializedAsync`, use `[PersistentState]` on a nullable public property to avoid a double API call when Blazor switches from SSR to WASM.

### Services Layer

Organised by feature (e.g. `Services/SalesOrders/`), not by type. DI registration lives in `Server/Extensions/ServiceExtensions.cs`.

For full details see `.github/copilot-instructions.md`.
