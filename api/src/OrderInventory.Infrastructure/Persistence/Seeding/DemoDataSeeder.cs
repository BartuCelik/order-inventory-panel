using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Sales;
using OrderInventory.Domain.Stock;
using OrderInventory.Domain.Users;

namespace OrderInventory.Infrastructure.Persistence.Seeding;

/// <summary>
/// Fills an empty database with an invented furniture workshop. All dates are relative to "now",
/// so the demo always shows a late order, open work orders and a material shortage.
/// </summary>
/// <remarks>
/// The data is written directly (not through the application services) but follows the same rules:
/// stock never goes negative at any point in time, work order components are a snapshot of the
/// product's direct BOM, and the completed work order has matching consumption/output movements.
/// </remarks>
public static class DemoDataSeeder
{
    private const string SeedUser = "seed";

    /// <summary>
    /// Shared by the demo accounts. Public on purpose: the demo invites visitors to sign in as each role.
    /// Only written when Database:SeedDemoData is on, which is never the case for a real deployment.
    /// </summary>
    public const string DemoPassword = "workshop-demo";

    public static async Task SeedDemoDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        await SeedAsync(dbContext, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        await SeedUsersAsync(dbContext, cancellationToken);
    }

    /// <summary>
    /// One demo account per role. Separate from <see cref="SeedAsync"/> because a database seeded
    /// before auth existed (the live demo) already has products but still needs users.
    /// </summary>
    /// <returns><c>false</c> when users already exist and nothing was written.</returns>
    public static async Task<bool> SeedUsersAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return false;
        }

        var hasher = new PasswordHasher<User>();
        User Account(string email, string displayName, UserRole role)
        {
            var user = new User { Email = email, DisplayName = displayName, PasswordHash = "", Role = role };
            user.PasswordHash = hasher.HashPassword(user, DemoPassword);
            return user;
        }

        dbContext.Users.AddRange(
            Account("admin@workshop.example.com", "Mira Kaplan", UserRole.Admin),
            Account("planner@workshop.example.com", "Jonas Reed", UserRole.Planner),
            Account("warehouse@workshop.example.com", "Lena Vogt", UserRole.Warehouse));
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <returns><c>false</c> when the database already contains data and nothing was written.</returns>
    public static async Task<bool> SeedAsync(AppDbContext dbContext, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Products.AnyAsync(cancellationToken))
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await WriteDemoDataAsync(dbContext, utcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The nightly demo reset: empties every table except users and writes the seed again, in one
    /// transaction, so a visitor never sees a half-empty demo. Users stay so their tokens keep working.
    /// </summary>
    /// <remarks>
    /// TRUNCATE skips the append-only ledger trigger (rule 2, owner decision 2026-10-25) and waits for
    /// running requests to finish (it takes an exclusive table lock). The tables are listed without
    /// CASCADE on purpose: a new table that references these makes the reset fail instead of being emptied unnoticed.
    /// </remarks>
    public static async Task ResetAsync(AppDbContext dbContext, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE stock_movements, work_order_components, work_orders, sales_order_lines, sales_orders,
                     customers, bom_lines, products
            RESTART IDENTITY
            """,
            cancellationToken);
        await WriteDemoDataAsync(dbContext, utcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task WriteDemoDataAsync(AppDbContext dbContext, DateTime utcNow, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(utcNow);
        DateTime DaysAgo(int days) => utcNow.AddDays(-days);

        // ---------- Products and BOM ----------
        var products = CreateProducts();
        AddBillsOfMaterials(products);
        dbContext.Products.AddRange(products.Values);

        // ---------- Customers and sales orders ----------
        var harbor = new Customer { Name = "Harbor Street Café", ContactName = "Nora Whitfield", Email = "nora@harborstreet.example.com", Phone = "+1 555 0101" };
        var linden = new Customer { Name = "Linden Boutique Hotel", ContactName = "Samuel Okafor", Email = "purchasing@lindenhotel.example.com", Phone = "+1 555 0102" };
        var northgate = new Customer { Name = "Northgate Interiors", ContactName = "Elif Marsh", Email = "elif@northgate.example.com", Phone = "+1 555 0103" };
        var mapleRye = new Customer { Name = "Maple & Rye Restaurant", ContactName = "Tom Brennan", Email = "tom@maplerye.example.com", Phone = "+1 555 0104" };
        dbContext.Customers.AddRange(harbor, linden, northgate, mapleRye);

        // Late: still open and the due date has passed.
        var harborOrder = CreateSalesOrder("SO-00001", harbor, today.AddDays(-20), today.AddDays(-3),
            (products["FG-CH-OAK"], 8), (products["FG-DT-160"], 2));
        var lindenOrder = CreateSalesOrder("SO-00002", linden, today.AddDays(-10), today.AddDays(14),
            (products["FG-NS-2D"], 12));
        var northgateOrder = CreateSalesOrder("SO-00003", northgate, today.AddDays(-5), today.AddDays(21),
            (products["FG-BS-80"], 4), (products["FG-DT-160"], 1));
        var mapleRyeOrder = CreateSalesOrder("SO-00004", mapleRye, today.AddDays(-2), today.AddDays(30),
            (products["FG-CH-OAK"], 12));
        dbContext.SalesOrders.AddRange(harborOrder, lindenOrder, northgateOrder, mapleRyeOrder);

        // ---------- Work orders ----------
        var legRun = CreateWorkOrder("WO-00001", products["SF-TABLE-LEG"], 6, WorkOrderStatus.Done, today.AddDays(-9));
        legRun.CompletedAt = DaysAgo(7);
        foreach (var component in legRun.Components)
        {
            component.ConsumedQty = component.RequiredQty;
        }

        var topsInProgress = CreateWorkOrder("WO-00002", products["SF-TABLE-TOP"], 2, WorkOrderStatus.InProgress, today.AddDays(-2));
        var harborTables = CreateWorkOrder("WO-00003", products["FG-DT-160"], 2, WorkOrderStatus.Planned, today.AddDays(1),
            harborOrder.Lines.Single(l => l.Product == products["FG-DT-160"]));
        var harborChairs = CreateWorkOrder("WO-00004", products["FG-CH-OAK"], 8, WorkOrderStatus.Planned, today,
            harborOrder.Lines.Single(l => l.Product == products["FG-CH-OAK"]));
        // Needs 4.8 m² oak board; 6 on hand minus 3.2 reserved by WO-00002 leaves a 2.0 shortage.
        var topsPlanned = CreateWorkOrder("WO-00005", products["SF-TABLE-TOP"], 3, WorkOrderStatus.Planned, today.AddDays(3));
        dbContext.WorkOrders.AddRange(legRun, topsInProgress, harborTables, harborChairs, topsPlanned);

        // Work order ids are needed as RefId on the production movements below.
        await dbContext.SaveChangesAsync(cancellationToken);

        // ---------- Stock ledger (chronological) ----------
        var movements = new List<StockMovement>();

        void Add(string code, decimal quantity, StockMovementType type, DateTime at, string? note = null, WorkOrder? workOrder = null) =>
            movements.Add(new StockMovement
            {
                Product = products[code],
                Quantity = quantity,
                Type = type,
                RefType = workOrder is null ? null : StockMovementRefType.WorkOrder,
                RefId = workOrder?.Id,
                Note = note,
                CreatedBy = SeedUser,
                CreatedAt = at,
            });

        foreach (var (code, quantity) in OpeningBalances)
        {
            Add(code, quantity, StockMovementType.Adjustment, DaysAgo(30), "Opening balance");
        }

        Add("RM-OAK-BOARD", 2m, StockMovementType.Receipt, DaysAgo(20), "Timber delivery");
        Add("RM-OAK-SQUARE", 6m, StockMovementType.Receipt, DaysAgo(12), "Timber delivery");
        Add("RM-SCREW-4X40", 500m, StockMovementType.Receipt, DaysAgo(12), "Hardware delivery");
        Add("RM-OIL-HW", 2m, StockMovementType.Receipt, DaysAgo(8));

        foreach (var component in legRun.Components)
        {
            Add(component.ComponentProduct!.Code, -component.ConsumedQty, StockMovementType.ProductionConsumption, legRun.CompletedAt.Value, workOrder: legRun);
        }
        Add("SF-TABLE-LEG", legRun.Quantity, StockMovementType.ProductionOutput, legRun.CompletedAt.Value, workOrder: legRun);

        Add("RM-FABRIC", 10m, StockMovementType.Receipt, DaysAgo(4), "Upholstery supplies");
        Add("RM-OAK-BOARD", -0.5m, StockMovementType.Adjustment, DaysAgo(2), "Cycle count correction");

        dbContext.StockMovements.AddRange(movements);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, Product> CreateProducts()
    {
        Product[] products =
        [
            // Raw materials
            Raw("RM-OAK-BOARD", "Oak board 25 mm", UnitOfMeasure.SquareMeter, 4),
            Raw("RM-OAK-SQUARE", "Oak square stock 70×70 mm", UnitOfMeasure.Meter, 5),
            Raw("RM-PINE-BOARD", "Pine board 18 mm", UnitOfMeasure.SquareMeter, 5),
            Raw("RM-MDF-18", "MDF panel 18 mm", UnitOfMeasure.SquareMeter, 6),
            Raw("RM-PLY-6", "Birch plywood 6 mm", UnitOfMeasure.SquareMeter, 4),
            Raw("RM-SCREW-4X40", "Wood screw 4×40", UnitOfMeasure.Piece, 300),
            Raw("RM-DOWEL-8", "Beech dowel 8×40", UnitOfMeasure.Piece, 200),
            Raw("RM-SLIDE-450", "Drawer slide 450 mm (pair)", UnitOfMeasure.Piece, 8),
            Raw("RM-KNOB", "Brushed steel knob", UnitOfMeasure.Piece, 10),
            Raw("RM-GLUE-PVA", "PVA wood glue", UnitOfMeasure.Kilogram, 2),
            Raw("RM-OIL-HW", "Hardwax oil, natural", UnitOfMeasure.Liter, 2),
            Raw("RM-FOAM-40", "Upholstery foam 40 mm", UnitOfMeasure.SquareMeter, 2),
            Raw("RM-FABRIC", "Upholstery fabric, charcoal", UnitOfMeasure.Meter, 5),
            Raw("RM-FELT", "Felt glide pad", UnitOfMeasure.Piece, 100),

            // Semi-finished
            Make("SF-TABLE-TOP", "Oak table top 160×90", ProductType.SemiFinished),
            Make("SF-TABLE-LEG", "Oak table leg 72 cm", ProductType.SemiFinished),
            Make("SF-CHAIR-FRAME", "Oak chair frame", ProductType.SemiFinished),
            Make("SF-SEAT-CUSHION", "Upholstered seat pad", ProductType.SemiFinished),
            Make("SF-DRAWER-FRONT", "Oak drawer front", ProductType.SemiFinished),
            Make("SF-DRAWER", "Drawer box 450", ProductType.SemiFinished),
            Make("SF-CARCASS-NS", "Nightstand carcass", ProductType.SemiFinished),

            // Finished goods
            Make("FG-DT-160", "Oak dining table 160×90", ProductType.Finished),
            Make("FG-CH-OAK", "Upholstered oak chair", ProductType.Finished),
            Make("FG-NS-2D", "Two-drawer nightstand", ProductType.Finished),
            Make("FG-BS-80", "Pine bookshelf 80 cm", ProductType.Finished),
        ];

        return products.ToDictionary(p => p.Code);

        static Product Raw(string code, string name, UnitOfMeasure unit, decimal minStock) =>
            new() { Code = code, Name = name, Type = ProductType.RawMaterial, Unit = unit, MinStock = minStock };

        static Product Make(string code, string name, ProductType type) =>
            new() { Code = code, Name = name, Type = type, Unit = UnitOfMeasure.Piece };
    }

    private static void AddBillsOfMaterials(Dictionary<string, Product> products)
    {
        Bom("SF-TABLE-TOP", ("RM-OAK-BOARD", 1.6m), ("RM-GLUE-PVA", 0.2m), ("RM-OIL-HW", 0.3m));
        Bom("SF-TABLE-LEG", ("RM-OAK-SQUARE", 0.75m), ("RM-OIL-HW", 0.05m));
        Bom("SF-CHAIR-FRAME", ("RM-OAK-SQUARE", 3.2m), ("RM-DOWEL-8", 12), ("RM-GLUE-PVA", 0.1m), ("RM-OIL-HW", 0.15m));
        Bom("SF-SEAT-CUSHION", ("RM-PLY-6", 0.2m), ("RM-FOAM-40", 0.2m), ("RM-FABRIC", 0.6m));
        Bom("SF-DRAWER-FRONT", ("RM-OAK-BOARD", 0.08m), ("RM-OIL-HW", 0.02m), ("RM-KNOB", 1));
        Bom("SF-DRAWER", ("SF-DRAWER-FRONT", 1), ("RM-PINE-BOARD", 0.35m), ("RM-PLY-6", 0.2m), ("RM-SLIDE-450", 1), ("RM-SCREW-4X40", 8));
        Bom("SF-CARCASS-NS", ("RM-MDF-18", 1.1m), ("RM-DOWEL-8", 8), ("RM-SCREW-4X40", 12), ("RM-GLUE-PVA", 0.1m));

        Bom("FG-DT-160", ("SF-TABLE-TOP", 1), ("SF-TABLE-LEG", 4), ("RM-SCREW-4X40", 16), ("RM-FELT", 4));
        Bom("FG-CH-OAK", ("SF-CHAIR-FRAME", 1), ("SF-SEAT-CUSHION", 1), ("RM-SCREW-4X40", 4), ("RM-FELT", 4));
        Bom("FG-NS-2D", ("SF-CARCASS-NS", 1), ("SF-DRAWER", 2), ("RM-FELT", 4));
        // Single-level BOM: made straight from raw materials.
        Bom("FG-BS-80", ("RM-PINE-BOARD", 2.4m), ("RM-PLY-6", 0.8m), ("RM-SCREW-4X40", 24), ("RM-DOWEL-8", 12), ("RM-FELT", 4));

        void Bom(string parentCode, params (string ComponentCode, decimal QtyPerUnit)[] lines)
        {
            foreach (var (componentCode, qtyPerUnit) in lines)
            {
                products[parentCode].BomLines.Add(new BomLine { ComponentProduct = products[componentCode], QtyPerUnit = qtyPerUnit });
            }
        }
    }

    private static readonly (string Code, decimal Quantity)[] OpeningBalances =
    [
        ("RM-OAK-BOARD", 4.5m),
        ("RM-OAK-SQUARE", 6m),
        ("RM-PINE-BOARD", 8m),
        ("RM-MDF-18", 10m),
        ("RM-PLY-6", 6m),
        ("RM-SCREW-4X40", 400m),
        ("RM-DOWEL-8", 300m),
        ("RM-SLIDE-450", 10m),
        ("RM-KNOB", 15m),
        ("RM-GLUE-PVA", 4m),
        ("RM-OIL-HW", 3m),
        ("RM-FOAM-40", 1.5m), // below MinStock (2) so critical stock has an on-hand example too
        ("RM-FABRIC", 6m),
        ("RM-FELT", 120m),
        ("SF-CHAIR-FRAME", 10m),
        ("SF-SEAT-CUSHION", 4m),
        ("SF-DRAWER", 2m),
        ("SF-CARCASS-NS", 1m),
        ("FG-NS-2D", 3m),
        ("FG-BS-80", 5m),
    ];

    private static SalesOrder CreateSalesOrder(
        string number, Customer customer, DateOnly orderDate, DateOnly dueDate, params (Product Product, decimal Quantity)[] lines)
    {
        var order = new SalesOrder { Number = number, Customer = customer, OrderDate = orderDate, DueDate = dueDate };
        foreach (var (product, quantity) in lines)
        {
            order.Lines.Add(new SalesOrderLine { Product = product, Quantity = quantity });
        }
        return order;
    }

    /// <summary>Snapshots the product's direct BOM lines, as work order creation will (rule 8).</summary>
    private static WorkOrder CreateWorkOrder(
        string number, Product product, decimal quantity, WorkOrderStatus status, DateOnly plannedDate, SalesOrderLine? salesOrderLine = null)
    {
        var workOrder = new WorkOrder
        {
            Number = number,
            Product = product,
            Quantity = quantity,
            Status = status,
            PlannedDate = plannedDate,
            SalesOrderLine = salesOrderLine,
        };

        foreach (var line in product.BomLines)
        {
            workOrder.Components.Add(new WorkOrderComponent
            {
                ComponentProduct = line.ComponentProduct,
                RequiredQty = line.QtyPerUnit * quantity,
            });
        }

        return workOrder;
    }
}
