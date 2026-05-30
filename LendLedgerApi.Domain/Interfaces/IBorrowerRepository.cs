using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LendLedgerApi.Domain.Entities;

namespace LendLedgerApi.Domain.Interfaces
{
    public interface IBorrowerRepository
    {
        Task<Borrower?> GetByIdAsync(Guid id, Guid lenderId);
        Task<IEnumerable<Borrower>> GetAllAsync(Guid lenderId, string? status, string? sort, string? search);
        Task AddAsync(Borrower borrower);
        Task UpdateAsync(Borrower borrower);
        Task DeleteAsync(Guid id, Guid lenderId);
        Task SaveChangesAsync();
    }
}
