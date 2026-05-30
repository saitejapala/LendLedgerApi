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
    public class PaymentRepository : IPaymentRepository
    {
        private readonly LendLedgerDbContext _dbContext;

        public PaymentRepository(LendLedgerDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Payment?> GetByIdAsync(Guid id, Guid lenderId)
        {
            return await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.Id == id && p.LenderId == lenderId);
        }

        public async Task<IEnumerable<Payment>> GetByBorrowerIdAsync(Guid borrowerId, Guid lenderId)
        {
            return await _dbContext.Payments
                .Where(p => p.BorrowerId == borrowerId && p.LenderId == lenderId)
                .OrderByDescending(p => p.DateReceived)
                .ToListAsync();
        }

        public async Task<IEnumerable<Payment>> GetAllAsync(Guid lenderId)
        {
            return await _dbContext.Payments
                .Where(p => p.LenderId == lenderId)
                .OrderByDescending(p => p.DateReceived)
                .ToListAsync();
        }

        public async Task AddAsync(Payment payment)
        {
            await _dbContext.Payments.AddAsync(payment);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
