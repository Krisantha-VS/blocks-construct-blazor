using System.Net;
using System.Net.Http.Json;
using Client.Services;
using Services.SalesOrders;

namespace Test.Services;

public class SalesOrderApiServiceTests
{
    [Fact]
    public async Task GetAllAsync_CallsCollectionEndpoint()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<SalesOrder>())
            });

        var service = CreateService(handler);

        await service.GetAllAsync();

        Assert.Equal("/api/sales-orders", handler.LastRequestPath);
    }

    [Fact]
    public async Task GetByStatusAsync_CallsStatusEndpoint()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<SalesOrder>())
            });

        var service = CreateService(handler);

        await service.GetByStatusAsync("In Progress");

        Assert.Equal("/api/sales-orders/by-status/In%20Progress", handler.LastRequestPath);
    }

    [Fact]
    public async Task GetByIdAsync_CallsItemEndpoint()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new SalesOrder { Id = "SO-1001" })
            });

        var service = CreateService(handler);

        await service.GetByIdAsync("SO-1001");

        Assert.Equal("/api/sales-orders/SO-1001", handler.LastRequestPath);
    }

    private static SalesOrderApiService CreateService(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };

        return new SalesOrderApiService(httpClient);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public string? LastRequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestPath = request.RequestUri?.PathAndQuery;
            return Task.FromResult(responder(request));
        }
    }
}