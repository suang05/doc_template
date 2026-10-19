using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmkDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectIsolationToRemainingTenantEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. data_connections
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "data_connections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_data_connections_ProjectId",
                table: "data_connections",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_data_connections_projects_ProjectId",
                table: "data_connections",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 2. datasets
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "datasets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_datasets_ProjectId",
                table: "datasets",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_datasets_projects_ProjectId",
                table: "datasets",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 3. documents
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "documents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.DropIndex(
                name: "IX_documents_DocumentRef",
                table: "documents");

            migrationBuilder.CreateIndex(
                name: "IX_documents_ProjectId_DocumentRef",
                table: "documents",
                columns: new[] { "ProjectId", "DocumentRef" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_projects_ProjectId",
                table: "documents",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 4. generation_logs
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "generation_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_generation_logs_ProjectId",
                table: "generation_logs",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_generation_logs_projects_ProjectId",
                table: "generation_logs",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 4. generation_logs
            migrationBuilder.DropForeignKey(
                name: "FK_generation_logs_projects_ProjectId",
                table: "generation_logs");

            migrationBuilder.DropIndex(
                name: "IX_generation_logs_ProjectId",
                table: "generation_logs");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "generation_logs");

            // 3. documents
            migrationBuilder.DropForeignKey(
                name: "FK_documents_projects_ProjectId",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_ProjectId_DocumentRef",
                table: "documents");

            migrationBuilder.CreateIndex(
                name: "IX_documents_DocumentRef",
                table: "documents",
                column: "DocumentRef",
                unique: true);

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "documents");

            // 2. datasets
            migrationBuilder.DropForeignKey(
                name: "FK_datasets_projects_ProjectId",
                table: "datasets");

            migrationBuilder.DropIndex(
                name: "IX_datasets_ProjectId",
                table: "datasets");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "datasets");

            // 1. data_connections
            migrationBuilder.DropForeignKey(
                name: "FK_data_connections_projects_ProjectId",
                table: "data_connections");

            migrationBuilder.DropIndex(
                name: "IX_data_connections_ProjectId",
                table: "data_connections");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "data_connections");
        }
    }
}
