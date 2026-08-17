using System.Text.RegularExpressions;

namespace LogAnalyzer.ApplicationIntelligence.KnowledgeSources;

internal static partial class LogDiscoveryPatterns
{
    [GeneratedRegex(
        @"(?im)^\s*(?:Alert Rule|Alert Description)\s*:\s*(?<value>.+?)\s*$")]
    public static partial Regex AlertRule();

    [GeneratedRegex(
        @"(?im)^\s*Source\s*:\s*(?<value>[A-Za-z0-9._-]+)\s*$")]
    public static partial Regex SourceServer();

    [GeneratedRegex(
        @"(?im)^\s*Created\s*:\s*(?<value>.+?)\s*$")]
    public static partial Regex CreatedTimestamp();

    [GeneratedRegex(
        @"\b(?<value>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*){2,})\b")]
    public static partial Regex QualifiedName();

    [GeneratedRegex(
        @"\b(?<value>[A-Za-z_][A-Za-z0-9_]*DbContext)\b")]
    public static partial Regex DbContext();

    [GeneratedRegex(
        @"(?im)\b(?<value>(?:sp|usp|eo|dbo)[A-Za-z0-9_]+)\b")]
    public static partial Regex DatabaseOperation();

    [GeneratedRegex(
        @"(?im)\b(?:EXEC(?:UTE)?\s+)?(?<value>[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]{3,})\s+@")]
    public static partial Regex ParameterizedDatabaseOperation();

    [GeneratedRegex(
        @"(?im)\b(?<value>[A-Za-z0-9_.]+Exception)\b")]
    public static partial Regex ExceptionType();

    [GeneratedRegex(
        @"(?im)\bClientConnectionId\s*:\s*(?<value>[0-9a-fA-F-]{36})")]
    public static partial Regex ClientConnectionId();

    [GeneratedRegex(
        @"(?im)\b(?:CorrelationId|Correlation ID|Correlation-ID)\s*[:=]\s*(?<value>[A-Za-z0-9._:-]+)")]
    public static partial Regex CorrelationId();

    [GeneratedRegex(
        @"(?im)\bError Number\s*:\s*(?<value>-?\d+)")]
    public static partial Regex SqlErrorNumber();

    [GeneratedRegex(
        @"(?im)\bCommandTimeout\s*=\s*'?(?<value>\d+)'?")]
    public static partial Regex CommandTimeout();

    [GeneratedRegex(
        @"(?im)\bFailed executing DbCommand\s*\(""(?<value>[\d,]+)""ms\)")]
    public static partial Regex DbCommandDuration();

    [GeneratedRegex(
        @"(?im)\bExecution Timeout Expired\b")]
    public static partial Regex SqlTimeout();

    [GeneratedRegex(
        @"(?im)\bhttps?://[^\s""'<>()]+")]
    public static partial Regex Url();

    [GeneratedRegex(
        @"(?im)(?<value>/api/[A-Za-z0-9_./{}-]+)")]
    public static partial Regex ApiPath();

    [GeneratedRegex(
        @"(?im)\bHTTP\s*(?:Status)?\s*[:=]?\s*(?<value>[1-5]\d{2})\b")]
    public static partial Regex HttpStatus();

    [GeneratedRegex(
        @"(?im)\bMicrosoft\.EntityFrameworkCore\b")]
    public static partial Regex EntityFrameworkCore();

    [GeneratedRegex(
        @"(?im)\bMicrosoft\.Data\.SqlClient\b")]
    public static partial Regex MicrosoftDataSqlClient();

    [GeneratedRegex(
        @"(?im)\bSystem\.Data\.SqlClient\b")]
    public static partial Regex SystemDataSqlClient();

    [GeneratedRegex(
        @"(?im)\bSignalR\b")]
    public static partial Regex SignalR();

    [GeneratedRegex(
        @"(?im)\bXpertdoc\b")]
    public static partial Regex Xpertdoc();
}