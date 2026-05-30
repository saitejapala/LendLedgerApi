using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LendLedgerApi.Domain.Entities;

namespace LendLedgerApi.Domain.Interfaces
{
    public interface INoteRepository
    {
        Task<Note?> GetByIdAsync(Guid id, Guid lenderId);
        Task AddAsync(Note note);
        Task UpdateAsync(Note note);
        Task DeleteAsync(Guid id, Guid lenderId);
        Task SaveChangesAsync();
    }
}
