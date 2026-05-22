using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniMarket.API.Migrations
{
    /// <inheritdoc />
    public partial class Fase16CheckoutMultiLojaNativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LojaEntregaOpcaoId",
                table: "TBL_PEDIDO",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeEntregaSnapshot",
                table: "TBL_PEDIDO",
                type: "varchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PrazoEntregaDias",
                table: "TBL_PEDIDO",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LojaEntregaOpcaoId",
                table: "TBL_PEDIDO");

            migrationBuilder.DropColumn(
                name: "NomeEntregaSnapshot",
                table: "TBL_PEDIDO");

            migrationBuilder.DropColumn(
                name: "PrazoEntregaDias",
                table: "TBL_PEDIDO");
        }
    }
}
