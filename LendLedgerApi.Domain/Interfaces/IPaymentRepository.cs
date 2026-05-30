using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LendLedgerApi.Domain.Entities;

namespace LendLedgerApi.Domain.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdAsync(Guid id, Guid lenderId);
        Task<IEnumerable<Payment>> GetByBorrowerIdAsync(Guid borrowerId, Guid lenderId);
        Task<IEnumerable<Payment>> GetAllAsync(Guid lenderId);
        Task AddAsync(Payment payment);
        Task SaveChangesAsync();
    }
}
