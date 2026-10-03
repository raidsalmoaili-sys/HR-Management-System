using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.Audit;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.Hr.Modules;
using Project_HR_1.Models.Hr.Support;

namespace Project_HR_1.Models;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<JobTitle> JobTitles => Set<JobTitle>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<EmployeePersonal> EmployeePersonals => Set<EmployeePersonal>();
    public DbSet<EmployeeFinancial> EmployeeFinancials => Set<EmployeeFinancial>();
    public DbSet<EmployeeWork> EmployeeWorks => Set<EmployeeWork>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();

    public DbSet<HrRole> HrRoles => Set<HrRole>();
    public DbSet<HrRolePermission> HrRolePermissions => Set<HrRolePermission>();
    public DbSet<EmployeeRoleAssignment> EmployeeRoleAssignments => Set<EmployeeRoleAssignment>();

    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();

    // Business services (docs/features/hr-core-modules.md)
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<SalaryAdvance> SalaryAdvances => Set<SalaryAdvance>();
    public DbSet<EmploymentContract> EmploymentContracts => Set<EmploymentContract>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollItem> PayrollItems => Set<PayrollItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Unique indexes
        b.Entity<Company>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Branch>().HasIndex(x => x.Slug).IsUnique();

        b.Entity<Employee>().HasIndex(x => x.EmployeeCode).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.UserId).IsUnique();

        b.Entity<JobTitle>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();

        b.Entity<HrRole>().HasIndex(x => x.Code).IsUnique();
        b.Entity<HrRolePermission>().HasIndex(x => new { x.HrRoleId, x.ServiceId, x.Permission }).IsUnique();

        b.Entity<SupportTicket>().HasIndex(x => x.TicketCode).IsUnique();

        // Hierarchy: Company -> Branch -> Employee
        b.Entity<Branch>()
            .HasOne(x => x.Company)
            .WithMany(c => c.Branches)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Employee>()
            .HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Employee>()
            .HasOne(x => x.Branch)
            .WithMany(br => br.Employees)
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Employee>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<Employee>()
            .HasOne(x => x.JobTitle)
            .WithMany()
            .HasForeignKey(x => x.JobTitleId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<JobTitle>()
            .HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Department / Sponsor lookups
        b.Entity<Department>()
            .HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Sponsor>()
            .HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Employee>()
            .HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<Employee>()
            .HasOne(x => x.Sponsor)
            .WithMany()
            .HasForeignKey(x => x.SponsorId)
            .OnDelete(DeleteBehavior.SetNull);

        // Employee profile sub-tables (1:1, shared PK = EmployeeId)
        b.Entity<EmployeePersonal>().HasKey(x => x.EmployeeId);
        b.Entity<EmployeePersonal>()
            .HasOne(x => x.Employee)
            .WithOne(e => e.Personal)
            .HasForeignKey<EmployeePersonal>(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<EmployeeFinancial>().HasKey(x => x.EmployeeId);
        b.Entity<EmployeeFinancial>()
            .HasOne(x => x.Employee)
            .WithOne(e => e.Financial)
            .HasForeignKey<EmployeeFinancial>(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<EmployeeWork>().HasKey(x => x.EmployeeId);
        b.Entity<EmployeeWork>()
            .HasOne(x => x.Employee)
            .WithOne(e => e.Work)
            .HasForeignKey<EmployeeWork>(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<EmployeeWork>()
            .HasOne(x => x.ActualJobTitle)
            .WithMany()
            .HasForeignKey(x => x.ActualJobTitleId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<EmployeeWork>()
            .HasOne(x => x.DirectSupervisor)
            .WithMany()
            .HasForeignKey(x => x.DirectSupervisorId)
            .OnDelete(DeleteBehavior.SetNull);

        // Employee documents (1:many)
        b.Entity<EmployeeDocument>()
            .HasOne(x => x.Employee)
            .WithMany(e => e.Documents)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // HrRole assignments
        b.Entity<HrRolePermission>()
            .HasOne(x => x.HrRole)
            .WithMany(r => r.Permissions)
            .HasForeignKey(x => x.HrRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<EmployeeRoleAssignment>()
            .HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<EmployeeRoleAssignment>()
            .HasOne(x => x.HrRole)
            .WithMany()
            .HasForeignKey(x => x.HrRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<EmployeeRoleAssignment>()
            .HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<EmployeeRoleAssignment>()
            .HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Audit log relationships
        b.Entity<LoginLog>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<ActivityLog>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<ActivityLog>()
            .HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.SetNull);

        // SupportTicket relationships
        b.Entity<SupportTicket>()
            .HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<SupportTicket>()
            .HasOne(x => x.AssignedToUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // ─── Business services ────────────────────────────────────────
        b.Entity<AttendanceRecord>().HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
        b.Entity<AttendanceRecord>().HasIndex(x => new { x.CompanyId, x.Date });
        b.Entity<AttendanceRecord>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<LeaveRequest>().HasIndex(x => new { x.CompanyId, x.Status });
        b.Entity<LeaveRequest>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<SalaryAdvance>().HasIndex(x => new { x.CompanyId, x.Status });
        b.Entity<SalaryAdvance>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<EmploymentContract>().HasIndex(x => x.ContractNumber).IsUnique();
        b.Entity<EmploymentContract>().HasIndex(x => new { x.CompanyId, x.Status });
        b.Entity<EmploymentContract>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<PayrollRun>().HasIndex(x => new { x.CompanyId, x.Year, x.Month }).IsUnique();
        b.Entity<PayrollRun>()
            .HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PayrollItem>().HasIndex(x => new { x.PayrollRunId, x.EmployeeId }).IsUnique();
        b.Entity<PayrollItem>()
            .HasOne(x => x.PayrollRun).WithMany(r => r.Items).HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PayrollItem>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
