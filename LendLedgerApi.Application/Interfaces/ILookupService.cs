using System.Collections.Generic;
using System.Threading.Tasks;
using LendLedgerApi.Domain.Entities;

namespace LendLedgerApi.Application.Interfaces
{
    public interface ILookupService
    {
        Task InitializeAsync();
        IEnumerable<LookupValue> GetValuesByType(string type);
        bool IsValid(string type, string code);
        IEnumerable<LookupValue> GetAllValues();
        Task ReloadAsync();
    }
}
