using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.Licensing.Tool.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductId);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "FeatureDefinitions",
                columns: table => new
                {
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    AddedUtc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureDefinitions", x => new { x.ProductId, x.Name });
                    table.ForeignKey(
                        name: "FK_FeatureDefinitions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Licenses",
                columns: table => new
                {
                    Reference = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerEmail = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licenses", x => x.Reference);
                    table.ForeignKey(
                        name: "FK_Licenses_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SigningKeys",
                columns: table => new
                {
                    SigningKeyId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: true),
                    PublicKey = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedPrivateKeyPem = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<string>(type: "TEXT", nullable: false),
                    ReplacedUtc = table.Column<string>(type: "TEXT", nullable: true),
                    ReplacedReason = table.Column<string>(type: "TEXT", nullable: true),
                    InReleaseUtc = table.Column<string>(type: "TEXT", nullable: true),
                    ReissuesSentUtc = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SigningKeys", x => x.SigningKeyId);
                    table.ForeignKey(
                        name: "FK_SigningKeys_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId");
                });

            migrationBuilder.CreateTable(
                name: "IssueEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    IssuedUtc = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    VendorTag = table.Column<string>(type: "TEXT", nullable: true),
                    FeaturesJson = table.Column<string>(type: "TEXT", nullable: false),
                    OrderRef = table.Column<string>(type: "TEXT", nullable: true),
                    KeyIdentifier = table.Column<string>(type: "TEXT", nullable: false),
                    SigningKeyId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueEntries_Licenses_Reference",
                        column: x => x.Reference,
                        principalTable: "Licenses",
                        principalColumn: "Reference",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueEntries_SigningKeys_SigningKeyId",
                        column: x => x.SigningKeyId,
                        principalTable: "SigningKeys",
                        principalColumn: "SigningKeyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssueEntries_OrderRef",
                table: "IssueEntries",
                column: "OrderRef");

            migrationBuilder.CreateIndex(
                name: "IX_IssueEntries_Reference",
                table: "IssueEntries",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_IssueEntries_SigningKeyId",
                table: "IssueEntries",
                column: "SigningKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_ProductId",
                table: "Licenses",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SigningKeys_ProductId",
                table: "SigningKeys",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureDefinitions");

            migrationBuilder.DropTable(
                name: "IssueEntries");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Licenses");

            migrationBuilder.DropTable(
                name: "SigningKeys");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
