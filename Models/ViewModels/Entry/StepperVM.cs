namespace Project_HR_1.Models.ViewModels.Entry;

public class EntryStep
{
    public string Id { get; set; } = "";
    public string LabelKey { get; set; } = "";
    public int Num { get; set; }
    public bool Done { get; set; }
    public bool Current { get; set; }
}

public class StepperVM
{
    public List<EntryStep> Steps { get; set; } = new();

    // 2-step flow: company → branch. Launcher is in the in-app shell, not part of the stepper.
    public static StepperVM Build(string currentId)
    {
        var order = new[] { "company", "branch" };
        var currentIdx = Array.IndexOf(order, currentId);
        if (currentIdx < 0) currentIdx = 0;

        return new StepperVM
        {
            Steps = new()
            {
                new() { Id = "company", LabelKey = "company", Num = 1, Done = currentIdx > 0, Current = currentIdx == 0 },
                new() { Id = "branch",  LabelKey = "branch",  Num = 2, Done = currentIdx > 1, Current = currentIdx == 1 },
            }
        };
    }
}
