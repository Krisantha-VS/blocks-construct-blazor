using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Client.Models.IAM;
using Client.Models.Inventory;

namespace Client.Services;

public interface IInventoryService
{
    Task<PagedResult<InventoryItem>> GetItemsAsync(int page, int pageSize, string? search = null, string? category = null, string? status = null);
    Task<InventoryItem?> GetItemAsync(string id);
    Task<string> CreateItemAsync(InventoryFormModel model);
    Task UpdateItemAsync(string id, InventoryFormModel model);
    Task DeleteItemAsync(string id);
}

public class InventoryService(HttpClient http, RuntimeClientConfig runtimeConfig) : IInventoryService
{
    private const string InventoryFields = "ItemId Category CreatedBy CreatedDate IsDeleted Tags DeletedDate ItemImageFileId ItemImageFileIds ItemLoc ItemName Language LastUpdatedBy LastUpdatedDate OrganizationIds Price Status Stock Supplier EligibleWarranty EligibleReplacement Discount";

    private const string GetInventoryItemsQuery =
        "query InventoryItems($input: DynamicQueryInput) { getInventoryItems(input: $input) { hasNextPage hasPreviousPage totalCount totalPages pageSize pageNo items { " + InventoryFields + " } } }";

    private const string InsertInventoryItemMutation =
        "mutation InsertInventoryItem($input: InventoryItemInsertInput!) { insertInventoryItem(input: $input) { itemId totalImpactedData acknowledged } }";

    private const string UpdateInventoryItemMutation =
        "mutation UpdateInventoryItem($filter: String!, $input: InventoryItemUpdateInput!) { updateInventoryItem(filter: $filter, input: $input) { itemId totalImpactedData acknowledged } }";

    private const string DeleteInventoryItemMutation =
        "mutation DeleteInventoryItem($filter: String!, $input: InventoryItemDeleteInput!) { deleteInventoryItem(filter: $filter, input: $input) { itemId totalImpactedData acknowledged } }";

    private string GatewayPath => $"/uds/v1/{Uri.EscapeDataString(ProjectSlug)}/gateway";
    private string ProjectSlug => !string.IsNullOrWhiteSpace(runtimeConfig.ProjectSlug)
        ? runtimeConfig.ProjectSlug
        : throw new InvalidOperationException("Runtime config is missing ProjectSlug.");

    public async Task<PagedResult<InventoryItem>> GetItemsAsync(int page, int pageSize, string? search = null, string? category = null, string? status = null)
    {
        var data = await ExecuteAsync<GetInventoryItemsData>(new GraphQlRequest(
            GetInventoryItemsQuery,
            new
            {
                input = new
                {
                    filter = BuildListFilter(search, category, status),
                    sort = "{}",
                    pageNo = page + 1,
                    pageSize
                }
            }));

        return new PagedResult<InventoryItem>
        {
            Data = data.GetInventoryItems?.Items ?? [],
            TotalCount = data.GetInventoryItems?.TotalCount ?? 0
        };
    }

    public async Task<InventoryItem?> GetItemAsync(string id)
    {
        var data = await ExecuteAsync<GetInventoryItemsData>(new GraphQlRequest(
            GetInventoryItemsQuery,
            new
            {
                input = new
                {
                    filter = JsonSerializer.Serialize(new Dictionary<string, string> { ["ItemId"] = id }),
                    sort = "{}",
                    pageNo = 1,
                    pageSize = 1
                }
            }));

        return data.GetInventoryItems?.Items.FirstOrDefault();
    }

    public async Task<string> CreateItemAsync(InventoryFormModel model)
    {
        var data = await ExecuteAsync<InsertInventoryItemData>(new GraphQlRequest(
            InsertInventoryItemMutation,
            new
            {
                input = new
                {
                    model.ItemName,
                    model.Category,
                    model.Supplier,
                    model.ItemLoc,
                    model.Price,
                    model.Stock,
                    model.Status,
                    Tags = Array.Empty<string>(),
                    model.EligibleWarranty,
                    model.EligibleReplacement,
                    model.Discount,
                    ItemImageFileId = string.Empty,
                    ItemImageFileIds = Array.Empty<string>()
                }
            }));

        var result = data.InsertInventoryItem
            ?? throw new InvalidOperationException("Inventory create returned no payload.");

        EnsureMutationSucceeded(result, "create");
        return result.ItemId;
    }

    public async Task UpdateItemAsync(string id, InventoryFormModel model)
    {
        var data = await ExecuteAsync<UpdateInventoryItemData>(new GraphQlRequest(
            UpdateInventoryItemMutation,
            new
            {
                filter = id,
                input = new
                {
                    model.ItemName,
                    model.Category,
                    model.Supplier,
                    model.ItemLoc,
                    model.Price,
                    model.Stock,
                    model.Status,
                    model.EligibleWarranty,
                    model.EligibleReplacement,
                    model.Discount
                }
            }));

        var result = data.UpdateInventoryItem
            ?? throw new InvalidOperationException("Inventory update returned no payload.");

        EnsureMutationSucceeded(result, "update");
    }

    public async Task DeleteItemAsync(string id)
    {
        var data = await ExecuteAsync<DeleteInventoryItemData>(new GraphQlRequest(
            DeleteInventoryItemMutation,
            new
            {
                filter = id,
                input = new
                {
                    isHardDelete = false
                }
            }));

        var result = data.DeleteInventoryItem
            ?? throw new InvalidOperationException("Inventory delete returned no payload.");

        EnsureMutationSucceeded(result, "delete");
    }

    private async Task<TData> ExecuteAsync<TData>(GraphQlRequest request)
    {
        var response = await http.PostAsJsonAsync(GatewayPath, request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GraphQlResponse<TData>>();
        if (payload is null)
        {
            throw new InvalidOperationException("Inventory gateway returned an empty response.");
        }

        if (payload.Errors is { Count: > 0 })
        {
            throw new InvalidOperationException(payload.Errors[0].Message);
        }

        return payload.Data ?? throw new InvalidOperationException("Inventory gateway returned no data.");
    }

    private static void EnsureMutationSucceeded(MutationResult result, string operation)
    {
        if (!result.Acknowledged)
        {
            throw new InvalidOperationException($"Inventory {operation} was not acknowledged by the gateway.");
        }
    }

    private static string BuildListFilter(string? search, string? category, string? status)
    {
        var conditions = new List<object>();

        if (!string.IsNullOrWhiteSpace(category))
        {
            conditions.Add(new Dictionary<string, object>
            {
                ["Category"] = category
            });
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add(new Dictionary<string, object>
            {
                ["Status"] = status
            });
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add(new Dictionary<string, object>
            {
                ["$or"] = new object[]
                {
                    BuildRegexFilter("ItemName", search),
                    BuildRegexFilter("Supplier", search),
                    BuildRegexFilter("ItemLoc", search)
                }
            });
        }

        return conditions.Count switch
        {
            0 => "{}",
            1 => JsonSerializer.Serialize(conditions[0]),
            _ => JsonSerializer.Serialize(new Dictionary<string, object> { ["$and"] = conditions })
        };
    }

    private static Dictionary<string, object> BuildRegexFilter(string field, string value)
    {
        return new Dictionary<string, object>
        {
            [field] = new Dictionary<string, string>
            {
                ["$regex"] = value,
                ["$options"] = "i"
            }
        };
    }

    private sealed record GraphQlRequest(string Query, object Variables);

    private sealed class GraphQlResponse<TData>
    {
        public TData? Data { get; set; }
        public List<GraphQlError> Errors { get; set; } = [];
    }

    private sealed class GraphQlError
    {
        public string Message { get; set; } = string.Empty;
    }

    private sealed class GetInventoryItemsData
    {
        [JsonPropertyName("getInventoryItems")]
        public InventoryItemsResult? GetInventoryItems { get; set; }
    }

    private sealed class InventoryItemsResult
    {
        public int TotalCount { get; set; }
        public List<InventoryItem> Items { get; set; } = [];
    }

    private sealed class InsertInventoryItemData
    {
        [JsonPropertyName("insertInventoryItem")]
        public MutationResult? InsertInventoryItem { get; set; }
    }

    private sealed class UpdateInventoryItemData
    {
        [JsonPropertyName("updateInventoryItem")]
        public MutationResult? UpdateInventoryItem { get; set; }
    }

    private sealed class DeleteInventoryItemData
    {
        [JsonPropertyName("deleteInventoryItem")]
        public MutationResult? DeleteInventoryItem { get; set; }
    }

    private sealed class MutationResult
    {
        public string ItemId { get; set; } = string.Empty;
        public int TotalImpactedData { get; set; }
        public bool Acknowledged { get; set; }
    }
}
