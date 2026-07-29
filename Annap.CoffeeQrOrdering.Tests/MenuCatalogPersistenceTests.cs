using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Regression: admin MenuItem edits must survive restart/bootstrap (CSV + specialty seed).
/// Uses EF InMemory with vector column ignored — no Docker required.
/// </summary>
public sealed class MenuCatalogPersistenceTests
{
    private sealed class MenuPersistTestDbContext(DbContextOptions<AppDbContext> options)
        : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<MenuItem>().Ignore(m => m.Embedding);
        }
    }

    private static MenuPersistTestDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"menu-persist-{Guid.NewGuid():N}")
            .Options;
        return new MenuPersistTestDbContext(options);
    }

    [Fact]
    public async Task Specialty_bootstrap_does_not_overwrite_admin_edited_name()
    {
        await using var db = CreateDb();
        var category = new MenuCategory { Name = AnnapSpecialtyCoffeeCatalog.CategoryName, SortOrder = 0 };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();

        const string adminName = "Admin Edited Dufatanye Name";
        db.MenuItems.Add(new MenuItem
        {
            CategoryId = category.Id,
            CatalogKey = AnnapSpecialtyCoffeeCatalog.DufatanyeKey,
            Name = adminName,
            Price = 80000m,
            IsAvailable = true
        });
        await db.SaveChangesAsync();

        await AnnapSpecialtyCoffeeBootstrap.EnsureSpecialtyCoffeesAsync(
            db, new DrinkAssetResolver(), NullLogger.Instance);

        var reloaded = await db.MenuItems
            .AsNoTracking()
            .SingleAsync(m => m.CatalogKey == AnnapSpecialtyCoffeeCatalog.DufatanyeKey);

        Assert.Equal(adminName, reloaded.Name);
    }

    [Fact]
    public async Task Specialty_bootstrap_inserts_missing_flagship_only()
    {
        await using var db = CreateDb();

        await AnnapSpecialtyCoffeeBootstrap.EnsureSpecialtyCoffeesAsync(
            db, new DrinkAssetResolver(), NullLogger.Instance);

        var keys = await db.MenuItems.AsNoTracking()
            .Select(m => m.CatalogKey)
            .ToListAsync();

        Assert.Equal(4, keys.Count);
        foreach (var key in AnnapSpecialtyCoffeeCatalog.ProtectedCatalogKeys)
            Assert.Contains(key, keys);
    }

    [Fact]
    public async Task Csv_import_refuses_when_menu_items_already_exist()
    {
        await using var db = CreateDb();
        var cat = new MenuCategory { Name = "Signature", SortOrder = 1 };
        db.MenuCategories.Add(cat);
        await db.SaveChangesAsync();
        db.MenuItems.Add(new MenuItem
        {
            CategoryId = cat.Id,
            CatalogKey = "sig-persistence-guard",
            Name = "Persistence Guard Drink",
            Price = 50000m,
            IsAvailable = true
        });
        await db.SaveChangesAsync();

        var csvPath = Path.Combine(Path.GetTempPath(), $"annap-persist-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(
            csvPath,
            "Category,Tên món,Nguyên liệu,Nguồn gốc / Xuất xứ,Vị\n" +
            "Signature,CSV Should Never Win,milk,VN,sweet\n");

        try
        {
            var imported = await AnnapDrinkCsvImporter.TryImportFromCsvAsync(
                db,
                csvPath,
                (_, _) => null,
                NullLogger.Instance);

            Assert.Equal(0, imported);
            Assert.Equal(1, await db.MenuItems.CountAsync());
            Assert.Equal(
                "Persistence Guard Drink",
                await db.MenuItems.Select(m => m.Name).SingleAsync());
        }
        finally
        {
            if (File.Exists(csvPath))
                File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task Csv_import_runs_only_on_empty_menu()
    {
        await using var db = CreateDb();
        var csvPath = Path.Combine(Path.GetTempPath(), $"annap-empty-{Guid.NewGuid():N}.csv");
        await File.WriteAllTextAsync(
            csvPath,
            "Category,Tên món,Nguyên liệu,Nguồn gốc / Xuất xứ,Vị\n" +
            "Signature,First Install Latte,milk,VN,sweet\n");

        try
        {
            var imported = await AnnapDrinkCsvImporter.TryImportFromCsvAsync(
                db,
                csvPath,
                (_, _) => null,
                NullLogger.Instance);

            Assert.Equal(1, imported);
            Assert.Equal(1, await db.MenuItems.CountAsync());
            Assert.Equal("First Install Latte", await db.MenuItems.Select(m => m.Name).SingleAsync());

            var again = await AnnapDrinkCsvImporter.TryImportFromCsvAsync(
                db,
                csvPath,
                (_, _) => null,
                NullLogger.Instance);
            Assert.Equal(0, again);
            Assert.Equal(1, await db.MenuItems.CountAsync());
        }
        finally
        {
            if (File.Exists(csvPath))
                File.Delete(csvPath);
        }
    }
}
