using System;
using System.Threading.Tasks;
using LendLedgerApi.Domain.Entities;

namespace LendLedgerApi.Domain.Interfaces
{
    public interface ILenderRepository
    {
        Task<Lender?> GetByIdAsync(Guid id);
        Task<Lender?> GetByEmailAsync(string email);
        Task AddAsync(Lender lender);
        Task SaveChangesAsync();
    }
}
