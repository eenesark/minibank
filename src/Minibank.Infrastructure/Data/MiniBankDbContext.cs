using Microsoft.EntityFrameworkCore;
using MiniBank.Entities;

namespace MiniBank.Data;

public class MiniBankDbContext : DbContext
{
    public MiniBankDbContext(DbContextOptions<MiniBankDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AdminUser> Admins => Set<AdminUser>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountTransaction> AccountTransactions => Set<AccountTransaction>();
    public DbSet<CustomerFeature> CustomerFeatures => Set<CustomerFeature>();
    public DbSet<Card> Cards => Set<Card>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AdminUser Yapılandırması
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Username).IsRequired().HasMaxLength(50);
            entity.HasIndex(a => a.Username).IsUnique();
            entity.Property(a => a.FullName).IsRequired().HasMaxLength(100);
            entity.Property(a => a.Email).IsRequired().HasMaxLength(100);
            entity.Property(a => a.PhoneNumber).HasMaxLength(20);
        });

        // Customer Yapılandırması
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(c => c.LastName).IsRequired().HasMaxLength(50);
            entity.Property(c => c.IdentityNumber).IsRequired().HasMaxLength(20);
            entity.HasIndex(c => c.IdentityNumber).IsUnique();
            entity.Property(c => c.Email).IsRequired().HasMaxLength(100);
            entity.Property(c => c.PhoneNumber).HasMaxLength(20);
            entity.Property(c => c.Role).HasMaxLength(20).HasDefaultValue("Customer");
        });

        // Account Yapılandırması
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.AccountNumber).IsRequired().HasMaxLength(20);
            entity.Property(a => a.Iban).IsRequired().HasMaxLength(34);
            entity.HasIndex(a => a.Iban).IsUnique();
            entity.Property(a => a.Balance).HasPrecision(18, 2);
            entity.Property(a => a.Currency).IsRequired().HasMaxLength(3);
            entity.Property(a => a.AccountType).HasMaxLength(20).HasDefaultValue("Demand");
            entity.Property(a => a.InterestRate).HasPrecision(5, 2);
            entity.Property(a => a.InitialDepositAmount).HasPrecision(18, 2);
            entity.Property(a => a.ExpectedInterestAmount).HasPrecision(18, 2);

            entity.HasOne(a => a.Customer)
                  .WithMany(c => c.Accounts)
                  .HasForeignKey(a => a.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // AccountTransaction Yapılandırması
        modelBuilder.Entity<AccountTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Amount).HasPrecision(18, 2);
            entity.Property(t => t.Description).HasMaxLength(200);
            entity.Property(t => t.Category).HasMaxLength(50).HasDefaultValue("Diğer");
            entity.Property(t => t.TargetIban).HasMaxLength(34);

            entity.HasOne(t => t.Account)
                  .WithMany(a => a.Transactions)
                  .HasForeignKey(t => t.AccountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CustomerFeature Yapılandırması
        modelBuilder.Entity<CustomerFeature>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.FeatureKey).IsRequired().HasMaxLength(50);
            entity.Property(f => f.CustomValue).HasMaxLength(100);
            entity.Property(f => f.UpdatedBy).HasMaxLength(50);
            entity.HasIndex(f => new { f.CustomerId, f.FeatureKey }).IsUnique();

            entity.HasOne(f => f.Customer)
                  .WithMany(c => c.Features)
                  .HasForeignKey(f => f.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Card Yapılandırması
        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CardNumber).IsRequired().HasMaxLength(19);
            entity.HasIndex(c => c.CardNumber).IsUnique();
            entity.Property(c => c.CardHolderName).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Cvv).IsRequired().HasMaxLength(4);
            entity.Property(c => c.CardType).HasMaxLength(20).HasDefaultValue("Debit");
            entity.Property(c => c.DailyLimit).HasPrecision(18, 2);
            entity.Property(c => c.DailyAtmWithdrawLimit).HasPrecision(18, 2);

            entity.HasOne(c => c.Customer)
                  .WithMany(u => u.Cards)
                  .HasForeignKey(c => c.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Account)
                  .WithMany(a => a.Cards)
                  .HasForeignKey(c => c.AccountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

