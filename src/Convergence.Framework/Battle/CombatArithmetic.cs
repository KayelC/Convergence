namespace Convergence.Battle;

internal static class CombatArithmetic
{
    public static decimal SaturatingAdd(decimal left, decimal right)
    {
        TryAdd(left, right, out decimal result);
        return result;
    }

    public static bool TryAdd(decimal left, decimal right, out decimal result)
    {
        try
        {
            result = checked(left + right);
            return true;
        }
        catch (OverflowException)
        {
            result = left >= 0m ? decimal.MaxValue : decimal.MinValue;
            return false;
        }
    }

    public static decimal SaturatingSubtract(decimal left, decimal right)
    {
        try
        {
            return checked(left - right);
        }
        catch (OverflowException)
        {
            return left >= 0m && right < 0m ? decimal.MaxValue : decimal.MinValue;
        }
    }

    public static decimal SaturatingMultiply(decimal left, decimal right)
    {
        try
        {
            return checked(left * right);
        }
        catch (OverflowException)
        {
            bool positive = (left < 0m) == (right < 0m);
            return positive ? decimal.MaxValue : decimal.MinValue;
        }
    }

    public static decimal SaturatingDivide(decimal dividend, decimal divisor)
    {
        try
        {
            return dividend / divisor;
        }
        catch (OverflowException)
        {
            bool positive = (dividend < 0m) == (divisor < 0m);
            return positive ? decimal.MaxValue : decimal.MinValue;
        }
    }

    public static decimal SaturatingMultiplyDivide(decimal left, decimal right, decimal divisor)
    {
        if (divisor == 0m)
        {
            throw new DivideByZeroException();
        }

        try
        {
            return checked(left * right) / divisor;
        }
        catch (OverflowException)
        {
            double result = ((double)left * (double)right) / (double)divisor;
            return SaturatingFromDouble(result);
        }
    }

    public static decimal SaturatingSum(IEnumerable<decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        decimal result = 0m;
        foreach (decimal value in values)
        {
            result = SaturatingAdd(result, value);
        }

        return result;
    }

    public static int SaturatingAdd(int left, int right)
    {
        long result = (long)left + right;
        return result switch
        {
            > int.MaxValue => int.MaxValue,
            < int.MinValue => int.MinValue,
            _ => (int)result
        };
    }

    public static decimal SaturatingFromDouble(double value)
    {
        if (double.IsNaN(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Combat arithmetic cannot represent NaN.");
        }

        try
        {
            return checked((decimal)value);
        }
        catch (OverflowException)
        {
            return value >= 0d ? decimal.MaxValue : decimal.MinValue;
        }
    }

    public static int SaturatingFloorToInt(decimal value)
    {
        decimal floored = Math.Floor(value);
        if (floored >= int.MaxValue)
        {
            return int.MaxValue;
        }
        if (floored <= int.MinValue)
        {
            return int.MinValue;
        }

        return decimal.ToInt32(floored);
    }
}
