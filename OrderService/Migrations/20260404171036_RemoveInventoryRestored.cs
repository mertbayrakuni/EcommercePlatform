using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInventoryRestored : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Column existed in the model snapshot but was never added via a migration,
            // so it may or may not be present in the live database. IF EXISTS is safe either way.
            migrationBuilder.Sql(@"ALTER TABLE ""Orders"" DROP COLUMN IF EXISTS ""InventoryRestored"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InventoryRestored",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
