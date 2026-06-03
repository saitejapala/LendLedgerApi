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
                .Include(b => b.Loans)
                .Include(b => b.Payments)
                .Include(b => b.Notes)
                .FirstOrDefaultAsync(b => b.Id == id && b.LenderId == lenderId);
        }

        public async Task<IEnumerable<Borrower>> GetAllAsync(Guid lenderId, string? status, string? sort, string? search)
        {
            var query = _dbContext.Borrowers
                .Include(b => b.Loans)
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
                if (status == "overdue")
                {
                    query = query.Where(b => b.Loans.Any(l => l.Status == "overdue"));
                }
                else if (status == "active")
                {
                    query = query.Where(b => b.Loans.Any(l => l.Status == "active") && !b.Loans.Any(l => l.Status == "overdue"));
                }
                else if (status == "paid")
                {
                    query = query.Where(b => b.Loans.All(l => l.Status == "paid") && b.Loans.Any());
                }
            }

            // Sorting
            query = sort switch
            {
                "balance-high" => query.OrderByDescending(b => b.Loans.Sum(l => l.RemainingBalance)),
                "due-date" => query.OrderBy(b => b.Loans.Where(l => l.Status != "paid").Min(l => (DateTime?)l.DueDate) ?? DateTime.MaxValue),
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
