namespace Project_HR_1.Models.Hr;

public record HrService(
    string Id,
    string NameAr,
    string NameEn,
    string Icon,
    string Color,
    string Category,
    int? Badge = null);
