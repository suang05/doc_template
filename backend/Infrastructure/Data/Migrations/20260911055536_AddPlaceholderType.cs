using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmkDocServer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceholderType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlaceholderType",
                table: "FieldMappings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlaceholderType",
                table: "FieldMappings");
        }
    }
}
