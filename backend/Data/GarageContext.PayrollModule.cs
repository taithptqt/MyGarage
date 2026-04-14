using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Data;

public partial class GarageContext
{
    public virtual DbSet<PayrollPeriod> PayrollPeriods { get; set; }
    public virtual DbSet<PayrollRun> PayrollRuns { get; set; }
    public virtual DbSet<PayrollItem> PayrollItems { get; set; }
    public virtual DbSet<Payslip> Payslips { get; set; }
    public virtual DbSet<StaffSalaryProfile> StaffSalaryProfiles { get; set; }
    public virtual DbSet<StaffContract> StaffContracts { get; set; }
    public virtual DbSet<OvertimeEntry> OvertimeEntries { get; set; }
    public virtual DbSet<AttendanceAdjustment> AttendanceAdjustments { get; set; }
    public virtual DbSet<SalaryAdvance> SalaryAdvances { get; set; }
    public virtual DbSet<EmployeeLoan> EmployeeLoans { get; set; }
    public virtual DbSet<PitProfile> PitProfiles { get; set; }
    public virtual DbSet<EmployeeDependent> EmployeeDependents { get; set; }

    private static void ConfigurePayrollModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PayrollPeriod>(entity =>
        {
            entity.ToTable("payroll_periods");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PeriodCode).HasColumnName("period_code").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.PayrollDate).HasColumnName("payroll_date");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasMany(d => d.PayrollRuns)
                .WithOne(p => p.PayrollPeriod)
                .HasForeignKey(d => d.PayrollPeriodId);

            entity.HasMany(d => d.SalaryAdvances)
                .WithOne(p => p.DeductFromPayrollPeriod)
                .HasForeignKey(d => d.DeductFromPayrollPeriodId);

            entity.HasMany(d => d.EmployeeLoans)
                .WithOne(p => p.StartPayrollPeriod)
                .HasForeignKey(d => d.StartPayrollPeriodId);
        });

        modelBuilder.Entity<PayrollRun>(entity =>
        {
            entity.ToTable("payroll_runs", tb => tb.HasTrigger("trg_payroll_runs_no_direct_paid_without_approval"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PayrollPeriodId).HasColumnName("payroll_period_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.RunNo).HasColumnName("run_no");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.CalculatedBy).HasColumnName("calculated_by");
            entity.Property(e => e.CalculatedAt).HasColumnName("calculated_at");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.PayrollPeriod)
                .WithMany(p => p.PayrollRuns)
                .HasForeignKey(d => d.PayrollPeriodId);

            entity.HasOne(d => d.Branch)
                .WithMany(p => p.PayrollRuns)
                .HasForeignKey(d => d.BranchId);

            entity.HasOne(d => d.CalculatedByNavigation)
                .WithMany(p => p.PayrollRunCalculatedByNavigations)
                .HasForeignKey(d => d.CalculatedBy);

            entity.HasOne(d => d.ApprovedByNavigation)
                .WithMany(p => p.PayrollRunApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy);
        });

        modelBuilder.Entity<PayrollItem>(entity =>
        {
            entity.ToTable("payroll_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PayrollRunId).HasColumnName("payroll_run_id");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.BaseSalary).HasColumnName("base_salary").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AttendanceDeduction).HasColumnName("attendance_deduction").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OvertimeAmount).HasColumnName("overtime_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AllowanceAmount).HasColumnName("allowance_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CommissionAmount).HasColumnName("commission_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BonusAmount).HasColumnName("bonus_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PenaltyAmount).HasColumnName("penalty_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AdvanceDeduction).HasColumnName("advance_deduction").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.LoanDeduction).HasColumnName("loan_deduction").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceEmployeeAmount).HasColumnName("insurance_employee_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceCompanyAmount).HasColumnName("insurance_company_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PitAmount).HasColumnName("pit_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OtherDeduction).HasColumnName("other_deduction").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetPay).HasColumnName("net_pay").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.PayrollRun)
                .WithMany(p => p.PayrollItems)
                .HasForeignKey(d => d.PayrollRunId);

            entity.HasOne(d => d.Staff)
                .WithMany(p => p.PayrollItems)
                .HasForeignKey(d => d.StaffId);
        });

        modelBuilder.Entity<Payslip>(entity =>
        {
            entity.ToTable("payslips");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PayrollItemId).HasColumnName("payroll_item_id");
            entity.Property(e => e.SlipNo).HasColumnName("slip_no").HasMaxLength(40).IsUnicode(false);
            entity.Property(e => e.IssuedAt).HasColumnName("issued_at");
            entity.Property(e => e.IssuedBy).HasColumnName("issued_by");
            entity.Property(e => e.FileUrl).HasColumnName("file_url");
            entity.Property(e => e.ViewedAt).HasColumnName("viewed_at");

            entity.HasOne(d => d.PayrollItem)
                .WithOne(p => p.Payslip)
                .HasForeignKey<Payslip>(d => d.PayrollItemId);

            entity.HasOne(d => d.IssuedByNavigation)
                .WithMany(p => p.Payslips)
                .HasForeignKey(d => d.IssuedBy);
        });

        modelBuilder.Entity<StaffSalaryProfile>(entity =>
        {
            entity.ToTable("staff_salary_profiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.BranchId).HasColumnName("branch_id");
            entity.Property(e => e.BaseSalary).HasColumnName("base_salary").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Note).HasColumnName("note").HasMaxLength(500);
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Staff)
                .WithMany(p => p.StaffSalaryProfileStaffs)
                .HasForeignKey(d => d.StaffId);

            entity.HasOne(d => d.Branch)
                .WithMany(p => p.StaffSalaryProfiles)
                .HasForeignKey(d => d.BranchId);

            entity.HasOne(d => d.UpdatedByNavigation)
                .WithMany(p => p.StaffSalaryProfileUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy);
        });

        modelBuilder.Entity<StaffContract>(entity =>
        {
            entity.ToTable("staff_contracts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.ContractType).HasColumnName("contract_type").HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
            entity.Property(e => e.BaseSalary).HasColumnName("base_salary").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InsuranceSalaryBase).HasColumnName("insurance_salary_base").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AllowanceAmount).HasColumnName("allowance_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PayFrequency).HasColumnName("pay_frequency").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.BankAccountName).HasColumnName("bank_account_name").HasMaxLength(255);
            entity.Property(e => e.BankAccountNumber).HasColumnName("bank_account_number").HasMaxLength(60).IsUnicode(false);
            entity.Property(e => e.BankName).HasColumnName("bank_name").HasMaxLength(120).IsUnicode(false);
            entity.Property(e => e.TaxCode).HasColumnName("tax_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Staff)
                .WithMany(p => p.StaffContracts)
                .HasForeignKey(d => d.StaffId);
        });

        modelBuilder.Entity<OvertimeEntry>(entity =>
        {
            entity.ToTable("overtime_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.WorkDate).HasColumnName("work_date");
            entity.Property(e => e.OvertimeType).HasColumnName("overtime_type").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Hours).HasColumnName("hours").HasColumnType("decimal(10, 2)");
            entity.Property(e => e.RateMultiplier).HasColumnName("rate_multiplier").HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Staff)
                .WithMany(p => p.OvertimeEntryStaffs)
                .HasForeignKey(d => d.StaffId);

            entity.HasOne(d => d.ApprovedByNavigation)
                .WithMany(p => p.OvertimeEntryApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy);
        });

        modelBuilder.Entity<AttendanceAdjustment>(entity =>
        {
            entity.ToTable("attendance_adjustments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.WorkDate).HasColumnName("work_date");
            entity.Property(e => e.AdjustmentType).HasColumnName("adjustment_type").HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.Minutes).HasColumnName("minutes");
            entity.Property(e => e.AmountImpact).HasColumnName("amount_impact").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Staff)
                .WithMany(p => p.AttendanceAdjustmentStaffs)
                .HasForeignKey(d => d.StaffId);

            entity.HasOne(d => d.ApprovedByNavigation)
                .WithMany(p => p.AttendanceAdjustmentApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy);
        });

        modelBuilder.Entity<SalaryAdvance>(entity =>
        {
            entity.ToTable("salary_advances");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RequestDate).HasColumnName("request_date");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.DeductFromPayrollPeriodId).HasColumnName("deduct_from_payroll_period_id");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Staff)
                .WithMany(p => p.SalaryAdvanceStaffs)
                .HasForeignKey(d => d.StaffId);

            entity.HasOne(d => d.ApprovedByNavigation)
                .WithMany(p => p.SalaryAdvanceApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy);

            entity.HasOne(d => d.DeductFromPayrollPeriod)
                .WithMany(p => p.SalaryAdvances)
                .HasForeignKey(d => d.DeductFromPayrollPeriodId);
        });

        modelBuilder.Entity<EmployeeLoan>(entity =>
        {
            entity.ToTable("employee_loans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.LoanAmount).HasColumnName("loan_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OutstandingAmount).HasColumnName("outstanding_amount").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MonthlyDeduction).HasColumnName("monthly_deduction").HasColumnType("decimal(18, 2)");
            entity.Property(e => e.StartPayrollPeriodId).HasColumnName("start_payroll_period_id");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Staff)
                .WithMany(p => p.EmployeeLoans)
                .HasForeignKey(d => d.StaffId);

            entity.HasOne(d => d.StartPayrollPeriod)
                .WithMany(p => p.EmployeeLoans)
                .HasForeignKey(d => d.StartPayrollPeriodId);
        });

        modelBuilder.Entity<PitProfile>(entity =>
        {
            entity.ToTable("pit_profiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.TaxCode).HasColumnName("tax_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.ResidentType).HasColumnName("resident_type").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.DependentCount).HasColumnName("dependent_count");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Staff)
                .WithMany(p => p.PitProfiles)
                .HasForeignKey(d => d.StaffId);
        });

        modelBuilder.Entity<EmployeeDependent>(entity =>
        {
            entity.ToTable("employee_dependents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StaffId).HasColumnName("staff_id");
            entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(255);
            entity.Property(e => e.Relationship).HasColumnName("relationship").HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Dob).HasColumnName("dob");
            entity.Property(e => e.TaxRegistrationDate).HasColumnName("tax_registration_date");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Staff)
                .WithMany(p => p.EmployeeDependents)
                .HasForeignKey(d => d.StaffId);
        });

    }
}
