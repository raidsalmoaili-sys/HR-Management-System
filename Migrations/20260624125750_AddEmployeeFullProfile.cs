using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Project_HR_1.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeFullProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BasicSalary",
                table: "Employees",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CostCenter",
                table: "Employees",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Employees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyNameAr",
                table: "Employees",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyNameEn",
                table: "Employees",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherNameAr",
                table: "Employees",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherNameEn",
                table: "Employees",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstNameAr",
                table: "Employees",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstNameEn",
                table: "Employees",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrandfatherNameAr",
                table: "Employees",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrandfatherNameEn",
                table: "Employees",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastWorkDate",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SponsorId",
                table: "Employees",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameAr = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    DocType = table.Column<int>(type: "integer", nullable: false),
                    CustomLabel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileData = table.Column<byte[]>(type: "bytea", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeFinancials",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    BeneficiaryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HousingAllowance = table.Column<decimal>(type: "numeric", nullable: false),
                    TransportAllowance = table.Column<decimal>(type: "numeric", nullable: false),
                    OtherAllowance = table.Column<decimal>(type: "numeric", nullable: false),
                    OtherAllowanceDescription = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EndOfServiceAdvance = table.Column<decimal>(type: "numeric", nullable: true),
                    SalaryType = table.Column<int>(type: "integer", nullable: false),
                    DailyRate = table.Column<decimal>(type: "numeric", nullable: true),
                    ContractTotalValue = table.Column<decimal>(type: "numeric", nullable: true),
                    ContractDurationMonths = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeFinancials", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_EmployeeFinancials_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePersonals",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    MaritalStatus = table.Column<int>(type: "integer", nullable: true),
                    ChildrenCount = table.Column<int>(type: "integer", nullable: true),
                    BloodType = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    District = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsEducated = table.Column<bool>(type: "boolean", nullable: false),
                    EducationLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EducationMajor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    EducationInstitution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EducationCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GraduationYear = table.Column<int>(type: "integer", nullable: true),
                    Gpa = table.Column<decimal>(type: "numeric", nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePersonals", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_EmployeePersonals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeWorks",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ActualJobTitleId = table.Column<int>(type: "integer", nullable: true),
                    DirectSupervisorId = table.Column<int>(type: "integer", nullable: true),
                    WorkLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WorkSystem = table.Column<int>(type: "integer", nullable: false),
                    DailyWorkHours = table.Column<decimal>(type: "numeric", nullable: true),
                    ContractType = table.Column<int>(type: "integer", nullable: false),
                    ContractDurationMonths = table.Column<int>(type: "integer", nullable: true),
                    ProbationDays = table.Column<int>(type: "integer", nullable: false),
                    AnnualLeaveBalance = table.Column<int>(type: "integer", nullable: false),
                    LastLeaveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SickLeaveUsedDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeWorks", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_EmployeeWorks_Employees_DirectSupervisorId",
                        column: x => x.DirectSupervisorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmployeeWorks_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeWorks_JobTitles_ActualJobTitleId",
                        column: x => x.ActualJobTitleId,
                        principalTable: "JobTitles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Sponsors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sponsors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sponsors_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_SponsorId",
                table: "Employees",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CompanyId",
                table: "Departments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId",
                table: "EmployeeDocuments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorks_ActualJobTitleId",
                table: "EmployeeWorks",
                column: "ActualJobTitleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorks_DirectSupervisorId",
                table: "EmployeeWorks",
                column: "DirectSupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_CompanyId",
                table: "Sponsors",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Departments_DepartmentId",
                table: "Employees",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Sponsors_SponsorId",
                table: "Employees",
                column: "SponsorId",
                principalTable: "Sponsors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Departments_DepartmentId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Sponsors_SponsorId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "EmployeeDocuments");

            migrationBuilder.DropTable(
                name: "EmployeeFinancials");

            migrationBuilder.DropTable(
                name: "EmployeePersonals");

            migrationBuilder.DropTable(
                name: "EmployeeWorks");

            migrationBuilder.DropTable(
                name: "Sponsors");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_SponsorId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "BasicSalary",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CostCenter",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FamilyNameAr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FamilyNameEn",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FatherNameAr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FatherNameEn",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FirstNameAr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "FirstNameEn",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "GrandfatherNameAr",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "GrandfatherNameEn",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LastWorkDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SponsorId",
                table: "Employees");
        }
    }
}
