using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sync.Infrastructure.Data.Migrations.Server
{
    /// <inheritdoc />
    public partial class AddSyncRunRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SyncRuns",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SyncRuns");
        }
    }
}
