using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LendLedgerApi.Application.Interfaces;
using LendLedgerApi.Domain.Entities;
using LendLedgerApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LendLedgerApi.Infrastructure.Services
{
    public class LookupService : ILookupService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ConcurrentDictionary<string, List<LookupValue>> _cache = new();
        private bool _isInitialized = false;
        private readonly object _lock = new();

        public LookupService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await LoadCacheAsync();
            _isInitialized = true;
        }

        public IEnumerable<LookupValue> GetValuesByType(string type)
        {
            EnsureInitialized();
            return _cache.TryGetValue(type.ToLowerInvariant(), out var list) ? list : Enumerable.Empty<LookupValue>();
        }

        public bool IsValid(string type, string code)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(code)) return false;

            var list = GetValuesByType(type);
            return list.Any(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<LookupValue> GetAllValues()
        {
            EnsureInitialized();
            return _cache.Values.SelectMany(v => v);
        }

        public async Task ReloadAsync()
        {
            await LoadCacheAsync();
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                lock (_lock)
                {
                    if (!_isInitialized)
                    {
                        LoadCacheAsync().GetAwaiter().GetResult();
                        _isInitialized = true;
                    }
                }
            }
        }

        private async Task LoadCacheAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LendLedgerDbContext>();
            var values = await dbContext.LookupValues
                .Where(v => v.IsActive)
                .ToListAsync();

            _cache.Clear();
            var grouped = values.GroupBy(v => v.Type.ToLowerInvariant());
            foreach (var group in grouped)
            {
                _cache[group.Key] = group.ToList();
            }
        }
    }
}
