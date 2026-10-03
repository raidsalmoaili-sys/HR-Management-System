namespace Project_HR_1.Services.I18n;

public interface IHrLocalizer
{
    string Lang { get; }
    bool IsRtl { get; }
    string T(string key);
    string L(string ar, string en);
}
