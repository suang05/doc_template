using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmkDocServer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint10_DataConnectionAndExpr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourcePath",
                table: "FieldMappings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250);

            migrationBuilder.AddColumn<Guid>(
                name: "DataConnectionId",
                table: "FieldMappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceType",
                table: "FieldMappings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "json");

            migrationBuilder.AddColumn<string>(
                name: "SqlQuery",
                table: "FieldMappings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    DatabaseType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConnectionStringEncrypted = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataConnections_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldMappings_DataConnectionId",
                table: "FieldMappings",
                column: "DataConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DataConnections_ProjectId",
                table: "DataConnections",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_FieldMappings_DataConnections_DataConnectionId",
                table: "FieldMappings",
                column: "DataConnectionId",
                principalTable: "DataConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FieldMappings_DataConnections_DataConnectionId",
                table: "FieldMappings");

            migrationBuilder.DropTable(
                name: "DataConnections");

            migrationBuilder.DropIndex(
                name: "IX_FieldMappings_DataConnectionId",
                table: "FieldMappings");

            migrationBuilder.DropColumn(
                name: "DataConnectionId",
                table: "FieldMappings");

            migrationBuilder.DropColumn(
                name: "DataSourceType",
                table: "FieldMappings");

            migrationBuilder.DropColumn(
                name: "SqlQuery",
                table: "FieldMappings");

            migrationBuilder.AlterColumn<string>(
                name: "SourcePath",
                table: "FieldMappings",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);
        }
    }
}
