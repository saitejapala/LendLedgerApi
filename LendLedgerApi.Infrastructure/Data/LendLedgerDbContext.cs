using Microsoft.EntityFrameworkCore;
using LendLedgerApi.Domain.Entities;

namespace LendLedgerApi.Infrastructure.Data
{
    public class LendLedgerDbContext : DbContext
    {
        public LendLedgerDbContext(DbContextOptions<LendLedgerDbContext> options) : base(options)
        {
        }

        public DbSet<Lender> Lenders => Set<Lender>();
        public DbSet<Borrower> Borrowers => Set<Borrower>();
        public DbSet<Loan> Loans => Set<Loan>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<LookupValue> LookupValues => Set<LookupValue>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Lender Configuration
            modelBuilder.Entity<Lender>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // Borrower Configuration
            modelBuilder.Entity<Borrower>(entity =>
            {
                entity.HasOne(d => d.Lender)
                    .WithMany(p => p.Borrowers)
                    .HasForeignKey(d => d.LenderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Loan Configuration (One-to-Many with Borrower)
            modelBuilder.Entity<Loan>(entity =>
            {
                entity.HasOne(d => d.Borrower)
                    .WithMany(p => p.Loans)
                    .HasForeignKey(d => d.BorrowerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Lender)
                    .WithMany(p => p.Loans)
                    .HasForeignKey(d => d.LenderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Payment Configuration
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasOne(d => d.Borrower)
                    .WithMany(p => p.Payments)
                    .HasForeignKey(d => d.BorrowerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Lender)
                    .WithMany(p => p.Payments)
                    .HasForeignKey(d => d.LenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.Loan)
                    .WithMany()
                    .HasForeignKey(d => d.LoanId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
            });

            // Note Configuration
            modelBuilder.Entity<Note>(entity =>
            {
                entity.HasOne(d => d.Borrower)
                    .WithMany(p => p.Notes)
                    .HasForeignKey(d => d.BorrowerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Lender)
                    .WithMany(p => p.Notes)
                    .HasForeignKey(d => d.LenderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // LookupValue Configuration & Seeding
            modelBuilder.Entity<LookupValue>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Value).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(250);

                entity.HasData(
                    // Borrower Categories
                    new LookupValue { Id = "category:personal", Type = "Category", Code = "personal", Value = "Personal", Description = "Personal expenses, medical, travel, etc." },
                    new LookupValue { Id = "category:business", Type = "Category", Code = "business", Value = "Business", Description = "Business operation or capital needs" },
                    new LookupValue { Id = "category:emergency", Type = "Category", Code = "emergency", Value = "Emergency", Description = "Immediate financial crisis or medical emergencies" },
                    new LookupValue { Id = "category:education", Type = "Category", Code = "education", Value = "Education", Description = "Tuition, academic books, and college fees" },
                    new LookupValue { Id = "category:equipment", Type = "Category", Code = "equipment", Value = "Equipment", Description = "Tools, machinery, or computing equipment leasing" },

                    // Interest Types
                    new LookupValue { Id = "interest_type:flat", Type = "InterestType", Code = "flat", Value = "Flat", Description = "Flat interest rate calculated on principal amount" },
                    new LookupValue { Id = "interest_type:reducing", Type = "InterestType", Code = "reducing", Value = "Reducing", Description = "Reducing balance interest rate" },

                    // Payment Methods
                    new LookupValue { Id = "payment_method:cash", Type = "PaymentMethod", Code = "cash", Value = "Cash", Description = "Hand-delivered cash payments" },
                    new LookupValue { Id = "payment_method:bank_transfer", Type = "PaymentMethod", Code = "bank_transfer", Value = "Bank Transfer", Description = "Direct wire or ACH bank transfers" },
                    new LookupValue { Id = "payment_method:check", Type = "PaymentMethod", Code = "check", Value = "Check", Description = "Physical paper checks" },
                    new LookupValue { Id = "payment_method:online", Type = "PaymentMethod", Code = "online", Value = "Online", Description = "Digital app transfers (Zelle, Venmo, PayPal, etc.)" },

                    // Loan Statuses
                    new LookupValue { Id = "loan_status:active", Type = "LoanStatus", Code = "active", Value = "Active", Description = "Active loan with balance pending" },
                    new LookupValue { Id = "loan_status:overdue", Type = "LoanStatus", Code = "overdue", Value = "Overdue", Description = "Payment schedule is late or unpaid" },
                    new LookupValue { Id = "loan_status:paid", Type = "LoanStatus", Code = "paid", Value = "Paid", Description = "Fully settled and closed loan" },

                    // Repayment Cycles
                    new LookupValue { Id = "repayment_cycle:weekly", Type = "RepaymentCycle", Code = "weekly", Value = "Weekly", Description = "Installments paid once a week" },
                    new LookupValue { Id = "repayment_cycle:biweekly", Type = "RepaymentCycle", Code = "biweekly", Value = "Bi-Weekly", Description = "Installments paid every two weeks" },
                    new LookupValue { Id = "repayment_cycle:monthly", Type = "RepaymentCycle", Code = "monthly", Value = "Monthly", Description = "Installments paid once a month" }
                );
            });

            // Convert PascalCase names to snake_case for PostgreSQL compatibility
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                var tableName = entity.GetTableName();
                if (tableName != null)
                {
                    entity.SetTableName(tableName.ToSnakeCase());
                }

                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(property.Name.ToSnakeCase());
                }

                foreach (var key in entity.GetKeys())
                {
                    var keyName = key.GetName();
                    if (keyName != null)
                    {
                        key.SetName(keyName.ToSnakeCase());
                    }
                }

                foreach (var fk in entity.GetForeignKeys())
                {
                    var fkName = fk.GetConstraintName();
                    if (fkName != null)
                    {
                        fk.SetConstraintName(fkName.ToSnakeCase());
                    }
                }

                foreach (var index in entity.GetIndexes())
                {
                    var indexName = index.GetDatabaseName();
                    if (indexName != null)
                    {
                        index.SetDatabaseName(indexName.ToSnakeCase());
                    }
                }
            }
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            configurationBuilder.Properties<DateTime>()
                .HaveConversion<DateTimeToUtcConverter>();

            configurationBuilder.Properties<DateTime?>()
                .HaveConversion<NullableDateTimeToUtcConverter>();
        }

        private class DateTimeToUtcConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>
        {
            public DateTimeToUtcConverter() : base(
                v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
                v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc))
            {
            }
        }

        private class NullableDateTimeToUtcConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>
        {
            public NullableDateTimeToUtcConverter() : base(
                v => !v.HasValue ? v : (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)),
                v => !v.HasValue ? v : (v.Value.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)))
            {
            }
        }
    }

    public static class NamingExtensions
    {
        public static string? ToSnakeCase(this string? input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var startUnderscore = input.StartsWith("_");
            var result = System.Text.RegularExpressions.Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
            return startUnderscore ? "_" + result : result;
        }
    }
}
