namespace LendLedgerApi.Application.Interfaces
{
    public interface IRedisCacheService
    {
        bool SetString(string key, string value, int expireInSec = 0);
        string? GetString(string key);
        void RemoveKey(string key);
    }
}
