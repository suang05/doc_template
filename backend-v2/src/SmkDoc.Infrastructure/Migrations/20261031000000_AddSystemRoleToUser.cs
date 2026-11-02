using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmkDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemRoleToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemRole",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Member");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SystemRole",
                table: "users");
        }
    }
}

