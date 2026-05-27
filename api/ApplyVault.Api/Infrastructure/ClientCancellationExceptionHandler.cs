using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;

namespace ApplyVault.Api.Infrastructure;

public sealed class ClientCancellationExceptionHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!IsBenignCancellation(exception))
        {
            return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(true);
    }

    private static bool IsBenignCancellation(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => true,
            SqlException sqlException when IsSqlCancellation(sqlException) => true,
            _ => exception.InnerException is not null && IsBenignCancellation(exception.InnerException)
        };
    }

    private static bool IsSqlCancellation(SqlException exception) =>
        exception.Message.Contains("Operation cancelled", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Operation canceled", StringComparison.OrdinalIgnoreCase) ||
        (
            exception.Message.Contains("severe error", StringComparison.OrdinalIgnoreCase) &&
            exception.Message.Contains("cancel", StringComparison.OrdinalIgnoreCase));
}
