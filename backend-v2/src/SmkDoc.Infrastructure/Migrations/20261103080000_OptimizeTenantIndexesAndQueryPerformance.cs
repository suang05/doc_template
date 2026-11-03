using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmkDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeTenantIndexesAndQueryPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. templates: replace legacy global Slug unique index and single-column ProjectId index
            //    with composite tenant-scoped unique index (ProjectId, Slug), and align Name/UpdatedAt metadata.
            migrationBuilder.DropIndex(
                name: "IX_templates_Slug",
                table: "templates");

            migrationBuilder.DropIndex(
                name: "IX_templates_ProjectId",
                table: "templates");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "templates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "templates",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.CreateIndex(
                name: "IX_templates_ProjectId_Slug",
                table: "templates",
                columns: new[] { "ProjectId", "Slug" },
                unique: true);

            // 2. template_versions: promote MappingsSnapshot from text to jsonb.
            migrationBuilder.Sql(
                @"ALTER TABLE template_versions ALTER COLUMN ""MappingsSnapshot"" TYPE jsonb USING ""MappingsSnapshot""::jsonb;");

            // 3. generation_logs: replace unindexed tenant+time scans with composite descending indexes.
            migrationBuilder.DropIndex(
                name: "IX_generation_logs_ProjectId",
                table: "generation_logs");

            migrationBuilder.DropIndex(
                name: "IX_generation_logs_CreatedAt",
                table: "generation_logs");

            migrationBuilder.CreateIndex(
                name: "IX_generation_logs_ProjectId_CreatedAt",
                table: "generation_logs",
                columns: new[] { "ProjectId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_generation_logs_ProjectId_CallerApp_CreatedAt",
                table: "generation_logs",
                columns: new[] { "ProjectId", "CallerApp", "CreatedAt" },
                descending: new[] { false, false, true });

            // 4. Align BaseEntity<TId>.UpdatedAt nullability on companies, projects, and users.
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "companies",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "projects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "companies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_generation_logs_ProjectId_CallerApp_CreatedAt",
                table: "generation_logs");

            migrationBuilder.DropIndex(
                name: "IX_generation_logs_ProjectId_CreatedAt",
                table: "generation_logs");

            migrationBuilder.CreateIndex(
                name: "IX_generation_logs_CreatedAt",
                table: "generation_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_generation_logs_ProjectId",
                table: "generation_logs",
                column: "ProjectId");

            migrationBuilder.AlterColumn<string>(
                name: "MappingsSnapshot",
                table: "template_versions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_templates_ProjectId_Slug",
                table: "templates");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "templates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "templates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_templates_ProjectId",
                table: "templates",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_templates_Slug",
                table: "templates",
                column: "Slug",
                unique: true);
        }
    }
}
