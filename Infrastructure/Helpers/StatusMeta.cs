namespace AutoCheck.Models;

public static class StatusMeta
{
    // (label, color-class-suffix, css-tone-var)
    public static (string Label, string Color, string Tone) Of(LabStatus s) => s switch
    {
        LabStatus.Done     => ("Зараховано",  "green",  "var(--st-green)"),
        LabStatus.Review   => ("На перевірці", "yellow", "var(--st-yellow)"),
        LabStatus.Rejected => ("Відхилено",   "red",    "var(--st-red)"),
        _                  => ("Не здано",    "grey",   "var(--st-grey)"),
    };
}
