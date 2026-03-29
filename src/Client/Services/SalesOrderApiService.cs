using System.Net.Http.Json;
using Services.SalesOrders;

namespace Client.Services;

public interface ISalesOrderApiService
{
    Task<IReadOnlyList<SalesOrder>> GetAllAsync();
    Task<SalesOrder?> GetByIdAsync(string id);
    Task<IReadOnlyList<SalesOrder>> GetByStatusAsync(string status);
}

public class SalesOrderApiService(HttpClient http) : ISalesOrderApiService
{
    public async Task<IReadOnlyList<SalesOrder>> GetAllAsync()
    {
        return await http.GetFromJsonAsync<List<SalesOrder>>("/api/sales-orders") ?? [];
    }

    public async Task<SalesOrder?> GetByIdAsync(string id)
    {
        return await http.GetFromJsonAsync<SalesOrder>($"/api/sales-orders/{Uri.EscapeDataString(id)}");
    }

    public async Task<IReadOnlyList<SalesOrder>> GetByStatusAsync(string status)
    {
        return await http.GetFromJsonAsync<List<SalesOrder>>($"/api/sales-orders/by-status/{Uri.EscapeDataString(status)}") ?? [];
    }
}