using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduGuardProject.Migrations;

[Migration("20260826050000_ReuseReadingPassagesAcrossQuestionSets")]
public partial class ReuseReadingPassagesAcrossQuestionSets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "exam_question_name",
            table: "reading_passages",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "institution_id",
            table: "reading_passages",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE reading_passages AS passage
            SET institution_id = course_class.institution_id,
                exam_question_name = slot.exam_question_name
            FROM exam_slots AS slot
            INNER JOIN classes AS course_class ON course_class.id = slot.class_id
            WHERE passage.exam_slot_id = slot.id;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "exam_question_name",
            table: "reading_passages",
            type: "character varying(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(255)",
            oldMaxLength: 255,
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "institution_id",
            table: "reading_passages",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.DropForeignKey(
            name: "reading_passages_exam_slot_id_fkey",
            table: "reading_passages");

        migrationBuilder.DropIndex(
            name: "idx_reading_passages_exam_slot",
            table: "reading_passages");

        migrationBuilder.DropColumn(
            name: "exam_slot_id",
            table: "reading_passages");

        migrationBuilder.CreateIndex(
            name: "idx_reading_passages_institution_name",
            table: "reading_passages",
            columns: new[] { "institution_id", "exam_question_name" });

        migrationBuilder.AddForeignKey(
            name: "reading_passages_institution_id_fkey",
            table: "reading_passages",
            column: "institution_id",
            principalTable: "institutions",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "reading_passages_institution_id_fkey",
            table: "reading_passages");

        migrationBuilder.DropIndex(
            name: "idx_reading_passages_institution_name",
            table: "reading_passages");

        migrationBuilder.AddColumn<Guid>(
            name: "exam_slot_id",
            table: "reading_passages",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE reading_passages AS passage
            SET exam_slot_id = slot.id
            FROM (
                SELECT DISTINCT ON (course_class.institution_id, exam_slot.exam_question_name)
                    exam_slot.id,
                    course_class.institution_id,
                    exam_slot.exam_question_name
                FROM exam_slots AS exam_slot
                INNER JOIN classes AS course_class ON course_class.id = exam_slot.class_id
                ORDER BY course_class.institution_id, exam_slot.exam_question_name, exam_slot.created_at, exam_slot.id
            ) AS slot
            WHERE passage.institution_id = slot.institution_id
              AND LOWER(passage.exam_question_name) = LOWER(slot.exam_question_name);
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "exam_slot_id",
            table: "reading_passages",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "exam_question_name",
            table: "reading_passages");

        migrationBuilder.DropColumn(
            name: "institution_id",
            table: "reading_passages");

        migrationBuilder.CreateIndex(
            name: "idx_reading_passages_exam_slot",
            table: "reading_passages",
            column: "exam_slot_id");

        migrationBuilder.AddForeignKey(
            name: "reading_passages_exam_slot_id_fkey",
            table: "reading_passages",
            column: "exam_slot_id",
            principalTable: "exam_slots",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
    }
}
