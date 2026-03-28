using System.Text.Json;
using Client.Models.IAM;
using Client.Models.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController(IWebHostEnvironment env) : ControllerBase
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private string DataPath => Path.Combine(env.WebRootPath, "data", "inventory-items.json");

    [HttpGet]
    public async Task<ActionResult<PagedResult<InventoryItem>>> GetItems(
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var items = await ReadItemsAsync(cancellationToken);

        var query = items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(i =>
                Contains(i.ItemName, search)
                || Contains(i.Supplier, search)
                || Contains(i.ItemLoc, search));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(i => string.Equals(i.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = query.Count();
        var data = query
            .OrderByDescending(i => i.LastUpdatedDate)
            .Skip(Math.Max(page, 0) * Math.Max(pageSize, 1))
            .Take(Math.Max(pageSize, 1))
            .ToList();

        return Ok(new PagedResult<InventoryItem>
        {
            TotalCount = totalCount,
            Data = data
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryItem>> GetItem(string id, CancellationToken cancellationToken)
    {
        var items = await ReadItemsAsync(cancellationToken);
        var item = items.FirstOrDefault(i => string.Equals(i.ItemId, id, StringComparison.OrdinalIgnoreCase));
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] InventoryFormModel model, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var item = new InventoryItem
        {
            ItemId = Guid.NewGuid().ToString("N"),
            ItemName = model.ItemName,
            Category = model.Category,
            Supplier = model.Supplier,
            ItemLoc = model.ItemLoc,
            Price = model.Price,
            Stock = model.Stock,
            Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status,
            EligibleWarranty = model.EligibleWarranty,
            EligibleReplacement = model.EligibleReplacement,
            Discount = model.Discount,
            CreatedDate = now,
            LastUpdatedDate = now,
            Tags = []
        };

        var items = await ReadItemsAsync(cancellationToken);
        items.Add(item);
        await WriteItemsAsync(items, cancellationToken);

        return Ok(new { itemId = item.ItemId });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] InventoryFormModel model, CancellationToken cancellationToken)
    {
        var items = await ReadItemsAsync(cancellationToken);
        var item = items.FirstOrDefault(i => string.Equals(i.ItemId, id, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return NotFound();
        }

        item.ItemName = model.ItemName;
        item.Category = model.Category;
        item.Supplier = model.Supplier;
        item.ItemLoc = model.ItemLoc;
        item.Price = model.Price;
        item.Stock = model.Stock;
        item.Status = string.IsNullOrWhiteSpace(model.Status) ? item.Status : model.Status;
        item.EligibleWarranty = model.EligibleWarranty;
        item.EligibleReplacement = model.EligibleReplacement;
        item.Discount = model.Discount;
        item.LastUpdatedDate = DateTime.UtcNow;

        await WriteItemsAsync(items, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var items = await ReadItemsAsync(cancellationToken);
        var removed = items.RemoveAll(i => string.Equals(i.ItemId, id, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            return NotFound();
        }

        await WriteItemsAsync(items, cancellationToken);
        return NoContent();
    }

    private async Task<List<InventoryItem>> ReadItemsAsync(CancellationToken cancellationToken)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            EnsureStorage();
            var json = await System.IO.File.ReadAllTextAsync(DataPath, cancellationToken);
            var data = JsonSerializer.Deserialize<List<InventoryItem>>(json, JsonOptions);
            return data ?? [];
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task WriteItemsAsync(List<InventoryItem> items, CancellationToken cancellationToken)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            EnsureStorage();
            var json = JsonSerializer.Serialize(items, JsonOptions);
            await System.IO.File.WriteAllTextAsync(DataPath, json, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private void EnsureStorage()
    {
        var folder = Path.GetDirectoryName(DataPath)!;
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        if (!System.IO.File.Exists(DataPath))
        {
            System.IO.File.WriteAllText(DataPath, "[]");
        }
    }

    private static bool Contains(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);
}
