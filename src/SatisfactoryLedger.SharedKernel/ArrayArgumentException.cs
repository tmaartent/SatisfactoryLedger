using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SatisfactoryLedger.SharedKernel;

public class ArrayArgumentException(string message, string? paramName) : ArgumentException(message, paramName)
{
    public static void ThrowIfLessThan<T>(
        [NotNull] T[]? argument, 
        int minimumLength, 
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        // 1. Ensure the array itself is not null
        ArgumentNullException.ThrowIfNull(argument, paramName);

        // 2. Ensure it meets the required length threshold
        if (argument.Length < minimumLength)
        {
            throw new ArrayArgumentException(
                $"The array must contain at least {minimumLength} elements. Current count: {argument.Length}.", 
                paramName);
        }
    }
    
    public static void ThrowIfMoreThan<T>(
        [NotNull] T[]? argument, 
        int maximumLength, 
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        // 1. Ensure the array itself is not null
        ArgumentNullException.ThrowIfNull(argument, paramName);

        // 2. Ensure it meets the required length threshold
        if (argument.Length > maximumLength)
        {
            throw new ArrayArgumentException(
                $"The array must contain at most {maximumLength} elements. Current count: {argument.Length}.", 
                paramName);
        }
    }
    
    
}