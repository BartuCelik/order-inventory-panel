using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderInventory.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Rule 2 in the database: a row-level trigger rejects every UPDATE and DELETE on stock_movements.
    /// AppDbContext already refuses tracked changes, but bulk ExecuteUpdate/ExecuteDelete and plain SQL
    /// bypass it; the trigger closes that gap for every client.
    /// </summary>
    /// <remarks>
    /// TRUNCATE does not fire row-level triggers, so the Phase 5 nightly demo reset can still empty the
    /// table with <c>TRUNCATE ... RESTART IDENTITY CASCADE</c>. That is deliberate (owner decision 2026-10-25).
    /// No model change, so no snapshot update.
    /// </remarks>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261025070000_AppendOnlyLedgerTrigger")]
    public partial class AppendOnlyLedgerTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION stock_movements_append_only() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'stock_movements is append-only: % is not allowed. Record an Adjustment instead.', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER trg_stock_movements_append_only
                    BEFORE UPDATE OR DELETE ON stock_movements
                    FOR EACH ROW EXECUTE FUNCTION stock_movements_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER trg_stock_movements_append_only ON stock_movements;
                DROP FUNCTION stock_movements_append_only();
                """);
        }
    }
}
