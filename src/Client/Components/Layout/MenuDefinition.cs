using Client.Models;

namespace Client.Components.Layout;

public static class MenuDefinition
{
    public static readonly List<MenuItemModel> Items =
    [
        new() { Id = "iam", Label = "IAM", Path = "/identity-management", Icon = "users", IsIntegrated = true },
        new() { Id = "inventory", Label = "Inventory", Path = "/inventory", Icon = "store", IsIntegrated = true },
        new() { Id = "sales", Label = "Sales", Path = "/sales", Icon = "sales", IsIntegrated = true }
    ];
}
