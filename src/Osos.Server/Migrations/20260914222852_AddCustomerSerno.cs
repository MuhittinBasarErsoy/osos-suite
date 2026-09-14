using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Osos.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerSerno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustomerSerno",
                table: "OsosCredentials",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerSerno",
                table: "OsosCredentials");
        }
    }
}
