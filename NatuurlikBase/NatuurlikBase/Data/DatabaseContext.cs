﻿using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NatuurlikBase.Models;

namespace NatuurlikBase.Data;

public class DatabaseContext : IdentityDbContext<ApplicationUser>
{
    public DatabaseContext(DbContextOptions<DatabaseContext> options)
        : base(options)
    {
    }

    public DbSet<Country> Country { get; set; }
    public DbSet<Province> Province { get; set; }
    public DbSet<City> City { get; set; }
    public DbSet<Suburb> Suburb { get; set; }
    public DbSet<ApplicationUser> User { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductBrand> Brands { get; set; }
    public DbSet<ProductCategory> Categories { get; set; }
    public DbSet<ReturnReason> ReturnReason { get; set; }
    public DbSet<InventoryItem> InventoryItem { get; set; }
    public DbSet<InventoryType> InventoryType { get; set; }
    public DbSet<WriteOffReason> WriteOffReason { get; set; }
    public DbSet<Courier> Courier { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<QueryReason> QueryReason { get; set; }
    public DbSet<ReviewReason> ReviewReason { get; set; }
    public DbSet<InventoryProcured> InventoryProcured { get; set; }
    public DbSet<InventoryItemTransaction> InventoryItemTransaction { get; set; }
    public DbSet<ProductionTransaction> ProductionTransaction { get; set; }
    public DbSet<WriteOffInventory> InventoryWriteOff { get; set; } 
    public DbSet<WriteOffProduct> ProductWriteOff { get; set; }
    public DbSet<Cart> UserCart { get; set; }
    public DbSet<Order> Order { get; set; }
    public DbSet<OrderLine> OrderLine { get; set; }
    public DbSet<VAT> VAT { get; set; }
    public DbSet<PackageOrderProduct> OrderProduct { get; set; }
    public DbSet<OrderQuery> OrderQuery { get; set; }
    public DbSet<PaymentReminder> PaymentReminder { get; set; }
    public DbSet<OrderReview> OrderReview { get; set; }
    public DbSet<ReturnedProduct> ReturnedProduct { get; set; }
    public DbSet<Video> Video { get; set; }
    public DbSet<Delivery> Delivery { get; set; }
    public DbSet<ProductInventory> ProductConfiguration { get; set; }
    public DbSet<Audit> Audit { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        //Establish relationships for ProductInventory on creating the model.
        base.OnModelCreating(builder);

        builder.Entity<ProductInventory>()
                .HasKey(pi => new { pi.ProductId, pi.InventoryItemId });

        builder.Entity<ProductInventory>()
            .HasOne(pi => pi.Product)
            .WithMany(p => p.ProductInventories)
            .HasForeignKey(pi => pi.ProductId);

        builder.Entity<ProductInventory>()
            .HasOne(pi => pi.Inventory)
            .WithMany(i => i.ProductInventories)
            .HasForeignKey(pi => pi.InventoryItemId);
    }

    public virtual async Task<int> SaveChangesAsync(string userId = null)
    {
        OnBeforeSaveChanges(userId);
        var result = await base.SaveChangesAsync();
        return result;
    }
    private void OnBeforeSaveChanges(string userId)
    {
        ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditLine>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is Audit || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;
            var auditEntry = new AuditLine(entry);
            auditEntry.EntityName = entry.Entity.GetType().Name;
            auditEntry.ActorFullName = userId;
            auditEntries.Add(auditEntry);
            foreach (var property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.AuditType = AuditType.Create;
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        auditEntry.AuditType = AuditType.Delete;
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        break;
                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.ChangedColumns.Add(propertyName);
                            auditEntry.AuditType = AuditType.Update;
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }
        }
        foreach (var auditEntry in auditEntries)
        {
            Audit.Add(auditEntry.ToAudit());
        }
    }


}

