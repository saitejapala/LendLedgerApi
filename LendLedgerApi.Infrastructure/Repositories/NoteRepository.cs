using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using LendLedgerApi.Domain.Entities;
using LendLedgerApi.Domain.Interfaces;
using LendLedgerApi.Infrastructure.Data;

namespace LendLedgerApi.Infrastructure.Repositories
{
    public class NoteRepository : INoteRepository
    {
        private readonly LendLedgerDbContext _dbContext;

        public NoteRepository(LendLedgerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Note?> GetByIdAsync(Guid id, Guid lenderId)
        {
            return await _dbContext.Notes
                .FirstOrDefaultAsync(n => n.Id == id && n.LenderId == lenderId);
        }

        public async Task AddAsync(Note note)
        {
            await _dbContext.Notes.AddAsync(note);
        }

        public async Task UpdateAsync(Note note)
        {
            _dbContext.Notes.Update(note);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(Guid id, Guid lenderId)
        {
            var note = await GetByIdAsync(id, lenderId);
            if (note != null)
            {
                _dbContext.Notes.Remove(note);
            }
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
