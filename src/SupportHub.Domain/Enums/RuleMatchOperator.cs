namespace SupportHub.Domain.Enums;

public enum RuleMatchOperator
{
    Equals,
    Contains,
    StartsWith,
    EndsWith,
    Regex,
    In  // comma-separated list
}
