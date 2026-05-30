using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using LendLedgerApi.Domain.Entities;
using LendLedgerApi.Domain.Interfaces;
using LendLedgerApi.Infrastructure.Data;

namespace LendLedgerApi.Infrastructure.Repositories
{
    public class LenderRepository : ILenderRepository
    {
        private readonly LendLedgerDbContext _dbContext;

        public LenderRepository(LendLedgerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Lender?> GetByIdAsync(Guid id)
        {
            return await _dbContext.Lenders
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<Lender?> GetByEmailAsync(string email)
        {
            var emailLower = email.ToLower();
            return await _dbContext.Lenders
                .FirstOrDefaultAsync(l => l.Email == emailLower);
        }

        public async Task AddAsync(Lender lender)
        {
            await _dbContext.Lenders.AddAsync(lender);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
