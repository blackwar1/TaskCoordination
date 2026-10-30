namespace TaskCoordinator;
public sealed record DiagnosticResult(string Name,DiagnosticStatus Status,string Details,TimeSpan Duration);