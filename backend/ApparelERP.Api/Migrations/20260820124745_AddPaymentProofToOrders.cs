using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApparelERP.Api.Migrations
{
    public partial class AddPaymentProofToOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    setting_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    setting_value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.setting_key);
                });

            migrationBuilder.AddColumn<string>(
                name: "payment_proof_url",
                table: "sales_orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transaction_id",
                table: "sales_orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropColumn(
                name: "payment_proof_url",
                table: "sales_orders");

            migrationBuilder.DropColumn(
                name: "transaction_id",
                table: "sales_orders");
        }
    }
}
