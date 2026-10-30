using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookmarkIndexer.Core.Model.Migrations
{
    /// <inheritdoc />
    public partial class BookmarkContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "Bookmarks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                table: "Bookmarks");
        }
    }
}
