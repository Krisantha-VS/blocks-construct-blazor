namespace Client.Models.Profile;

public class DeviceSession
{
    public string ItemId { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime IssuedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string IpAddresses { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime UpdateDate { get; set; }
    public DeviceInformation DeviceInformation { get; set; } = new();
}

public class DeviceInformation
{
    public string Browser { get; set; } = string.Empty;
    public string OS { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public class DeviceSessionResponse
{
    public int TotalCount { get; set; }
    public List<DeviceSession> Data { get; set; } = [];
    public string? Errors { get; set; }
}