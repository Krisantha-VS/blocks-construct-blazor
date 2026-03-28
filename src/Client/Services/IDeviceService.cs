using Client.Models.Profile;

namespace Client.Services;

public interface IDeviceService
{
    Task<DeviceSessionResponse> GetSessionsAsync(string userId, int page, int pageSize);
}
