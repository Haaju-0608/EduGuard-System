using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduGuardProject.Migrations
{
    /// <inheritdoc />
    public partial class AddExamNameToExamQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "exam_name",
                table: "exam_questions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE exam_questions AS question
                SET exam_name = slot.exam_name
                FROM exam_slots AS slot
                WHERE question.exam_slot_id = slot.id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "exam_name",
                table: "exam_questions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exam_name",
                table: "exam_questions");
        }
    }
}
