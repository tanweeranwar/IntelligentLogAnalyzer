namespace LogAnalyzer.ApplicationIntelligence.Validation;

public sealed class ApplicationPackageValidationException
    : Exception
{
    public ApplicationPackageValidationException(
        string message)
        : base(message)
    {
    }

    public ApplicationPackageValidationException(
        string message,
        Exception innerException)
        : base(
            message,
            innerException)
    {
    }
}