using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using LendLedgerApi.Domain.Entities;
using LendLedgerApi.Domain.Interfaces;
using LendLedgerApi.Infrastructure.Data;

namespace LendLedgerApi.Infrastructure.Repositories
{
    public class BorrowerRepository : IBorrowerRepository
    {
        private readonly LendLedgerDbContext _dbContext;

        public BorrowerRepository(LendLedgerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Borrower?> GetByIdAsync(Guid id, Guid lenderId)
        {
            return await _dbContext.Borrowers
                .Include(b => b.Loan)
                .Include(b => b.Payments)
                .Include(b => b.Notes)
                .FirstOrDefaultAsync(b => b.Id == id && b.LenderId == lenderId);
        }

        public async Task<IEnumerable<Borrower>> GetAllAsync(Guid lenderId, string? status, string? sort, string? search)
        {
            var query = _dbContext.Borrowers
                .Include(b => b.Loan)
                .Where(b => b.LenderId == lenderId)
                .AsQueryable();

            // Search Filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(b => 
                    b.FullName.ToLower().Contains(searchLower) ||
                    b.Phone.ToLower().Contains(searchLower) ||
                    b.Email.ToLower().Contains(searchLower));
            }

            // Status Filter
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                query = query.Where(b => b.Loan != null && b.Loan.Status == status);
            }

            // Sorting
            query = sort switch
            {
                "balance-high" => query.OrderByDescending(b => b.Loan != null ? b.Loan.RemainingBalance : 0),
                "due-date" => query.OrderBy(b => b.Loan != null ? b.Loan.DueDate : DateTime.MaxValue),
                "newest" or _ => query.OrderByDescending(b => b.CreatedAt)
            };

            return await query.ToListAsync();
        }

        public async Task AddAsync(Borrower borrower)
        {
            await _dbContext.Borrowers.AddAsync(borrower);
        }

        public async Task UpdateAsync(Borrower borrower)
        {
            _dbContext.Borrowers.Update(borrower);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(Guid id, Guid lenderId)
        {
            var borrower = await GetByIdAsync(id, lenderId);
            if (borrower != null)
            {
                _dbContext.Borrowers.Remove(borrower);
            }
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
