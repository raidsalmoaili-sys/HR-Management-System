namespace Project_HR_1.Models.Hr;

public record LookupItem(string Code, string NameAr, string NameEn);

public static class Lookups
{
    public static readonly IReadOnlyList<LookupItem> SaudiBanks = new[]
    {
        new LookupItem("RJHI", "مصرف الراجحي", "Al Rajhi Bank"),
        new LookupItem("NCB",  "البنك الأهلي السعودي", "Saudi National Bank"),
        new LookupItem("RIBL", "بنك الرياض", "Riyad Bank"),
        new LookupItem("SABB", "البنك السعودي البريطاني (ساب)", "SABB"),
        new LookupItem("ANB",  "البنك العربي الوطني", "Arab National Bank"),
        new LookupItem("BSFR", "البنك السعودي الفرنسي", "Banque Saudi Fransi"),
        new LookupItem("ALINMA", "مصرف الإنماء", "Alinma Bank"),
        new LookupItem("SAIB", "البنك السعودي للاستثمار", "Saudi Investment Bank"),
        new LookupItem("BJAZ", "بنك الجزيرة", "Bank Aljazira"),
        new LookupItem("ALBI", "بنك البلاد", "Bank Albilad"),
        new LookupItem("STCPAY", "إس تي سي باي", "STC Pay"),
    };

    public static readonly IReadOnlyList<LookupItem> Nationalities = new[]
    {
        new LookupItem("SA", "سعودي", "Saudi"),
        new LookupItem("EG", "مصري", "Egyptian"),
        new LookupItem("YE", "يمني", "Yemeni"),
        new LookupItem("SD", "سوداني", "Sudanese"),
        new LookupItem("SY", "سوري", "Syrian"),
        new LookupItem("JO", "أردني", "Jordanian"),
        new LookupItem("PK", "باكستاني", "Pakistani"),
        new LookupItem("IN", "هندي", "Indian"),
        new LookupItem("BD", "بنغلاديشي", "Bangladeshi"),
        new LookupItem("PH", "فلبيني", "Filipino"),
        new LookupItem("ID", "إندونيسي", "Indonesian"),
        new LookupItem("LK", "سريلانكي", "Sri Lankan"),
        new LookupItem("NP", "نيبالي", "Nepali"),
        // Added 2026-09-27 for the employee-sheet import (see docs/features/employee-sheet-import.md).
        new LookupItem("MA", "مغربي", "Moroccan"),
        new LookupItem("TN", "تونسي", "Tunisian"),
        new LookupItem("DZ", "جزائري", "Algerian"),
        new LookupItem("LY", "ليبي", "Libyan"),
        new LookupItem("LB", "لبناني", "Lebanese"),
        new LookupItem("PS", "فلسطيني", "Palestinian"),
        new LookupItem("IQ", "عراقي", "Iraqi"),
        new LookupItem("SO", "صومالي", "Somali"),
        new LookupItem("ER", "إريتري", "Eritrean"),
        new LookupItem("ET", "إثيوبي", "Ethiopian"),
        new LookupItem("KE", "كيني", "Kenyan"),
        new LookupItem("UG", "أوغندي", "Ugandan"),
        new LookupItem("NG", "نيجيري", "Nigerian"),
        new LookupItem("NE", "نيجري", "Nigerien"),
        new LookupItem("TD", "تشادي", "Chadian"),
        new LookupItem("MR", "موريتاني", "Mauritanian"),
        new LookupItem("AF", "أفغاني", "Afghan"),
        new LookupItem("TR", "تركي", "Turkish"),
        new LookupItem("CN", "صيني", "Chinese"),
        new LookupItem("KW", "كويتي", "Kuwaiti"),
        new LookupItem("AE", "إماراتي", "Emirati"),
        new LookupItem("BH", "بحريني", "Bahraini"),
        new LookupItem("QA", "قطري", "Qatari"),
        new LookupItem("OM", "عماني", "Omani"),
        new LookupItem("OTH", "أخرى", "Other"),
    };
}
