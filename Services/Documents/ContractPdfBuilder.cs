using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Project_HR_1.Services.Documents;

public class ContractPreviewData
{
    public string EmployeeName { get; set; } = "";
    public string EmployeeCode { get; set; } = "";
    public string? NationalityName { get; set; }
    public string CompanyName { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string? JobTitleName { get; set; }
    public decimal BasicSalary { get; set; }
    public string ContractTypeLabel { get; set; } = "";
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public int? ContractDurationMonths { get; set; }
    public int ProbationDays { get; set; }
}

public interface IContractPdfBuilder
{
    byte[] Build(ContractPreviewData data);
}

public class ContractPdfBuilder : IContractPdfBuilder
{
    public byte[] Build(ContractPreviewData d)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Text("عقد عمل / Employment Contract")
                    .FontSize(18).Bold().AlignCenter();

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(8);

                    void Row(string label, string value)
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(label).SemiBold();
                            row.RelativeItem(2).Text(value);
                        });
                    }

                    Row("الموظف / Employee", d.EmployeeName);
                    Row("الرقم الوظيفي / Employee Code", d.EmployeeCode);
                    if (!string.IsNullOrWhiteSpace(d.NationalityName)) Row("الجنسية / Nationality", d.NationalityName);
                    Row("الشركة / Company", d.CompanyName);
                    Row("الفرع / Branch", d.BranchName);
                    if (!string.IsNullOrWhiteSpace(d.JobTitleName)) Row("الوظيفة / Job Title", d.JobTitleName!);
                    Row("الراتب الأساسي / Basic Salary", d.BasicSalary.ToString("N2"));
                    Row("نوع العقد / Contract Type", d.ContractTypeLabel);
                    if (d.ContractStartDate.HasValue) Row("تاريخ البداية / Start Date", d.ContractStartDate.Value.ToString("yyyy-MM-dd"));
                    if (d.ContractEndDate.HasValue) Row("تاريخ النهاية / End Date", d.ContractEndDate.Value.ToString("yyyy-MM-dd"));
                    if (d.ContractDurationMonths.HasValue) Row("مدة العقد (أشهر) / Duration (months)", d.ContractDurationMonths.Value.ToString());
                    Row("فترة التجربة (أيام) / Probation (days)", d.ProbationDays.ToString());

                    col.Item().PaddingTop(30).Text(
                        "هذه معاينة تلقائية للعقد بناءً على البيانات المُدخلة في شاشة إضافة الموظف، وليست النسخة النهائية الموقعة.\n" +
                        "This is an automatic preview generated from the Add Employee screen data, not the final signed copy.")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }
}
