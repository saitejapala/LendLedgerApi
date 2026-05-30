using System;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using LendLedgerApi.Application.Interfaces;

namespace LendLedgerApi.CacheService
{
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IConfiguration _configuration;
        private readonly int _expireInSec;
        private readonly Lazy<ConnectionMultiplexer> _lazyConnectionMultiplexer;

        private ConnectionMultiplexer Connection => _lazyConnectionMultiplexer.Value;

        public RedisCacheService(IConfiguration configuration)
        {
            _configuration = configuration;
            _expireInSec = Convert.ToInt32(configuration["Cache:ExpireInSec"] ?? "600");
            bool ssl = Convert.ToBoolean(configuration["Cache:Ssl"] ?? "false");
            
            var connectionString = configuration["Cache:ConnectionString"]?.ToString() ?? "localhost";
            var portString = configuration["Cache:Port"]?.ToString() ?? "6379";
            int port = Convert.ToInt32(portString);

            var options = new ConfigurationOptions
            {
                EndPoints = { { connectionString, port } },
                User = configuration["Cache:User"]?.ToString() ?? string.Empty,
                Password = configuration["Cache:Password"]?.ToString() ?? string.Empty,
                Ssl = ssl,
                AbortOnConnectFail = false,
                ConnectRetry = 3
            };

            _lazyConnectionMultiplexer = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options));
        }

        public bool SetString(string key, string value, int expireInSec = 0)
        {
            try
            {
                var db = Connection.GetDatabase();
                TimeSpan expire = TimeSpan.FromSeconds(expireInSec == 0 ? _expireInSec : expireInSec);
                db.StringSet(key, value, expire);
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Redis error in SetString: {e.Message}");
                return false;
            }
        }

        public string? GetString(string key)
        {
            try
            {
                var db = Connection.GetDatabase();
                RedisValue result = db.StringGet(key);
                return result.HasValue ? result.ToString() : null;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Redis error in GetString: {e.Message}");
                return null;
            }
        }

        public void RemoveKey(string key)
        {
            try
            {
                var db = Connection.GetDatabase();
                db.KeyDelete(key);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Redis error in RemoveKey: {e.Message}");
            }
        }
    }
}
