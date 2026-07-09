namespace AutoCheck.Models;

public record CommitInfo(string Sha, string Short, string Message, string Author, DateTime Date, string[] Parents);
public record CommitTaskMap(string Sha, int TaskNumber);
