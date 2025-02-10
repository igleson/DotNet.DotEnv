using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Text;

namespace DotEnv.SourceGenerator;

public static class SpanExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToMacroCase(this ReadOnlySpan<char> value)
    {
        var newIndex = 0;
        var insertSeparator = true;
        var isFirstCharacter = true;
        var current = UnicodeCategory.OtherSymbol;
        var newString = new char[value.Length + CalculateSpanSizeForKebabOrSnakeCase(value)];
        foreach (var c in value)
        {
            var previous = current;
            current = char.GetUnicodeCategory(c);
            insertSeparator = (previous != current && current is UnicodeCategory.UppercaseLetter or UnicodeCategory.DecimalDigitNumber) ||
                              insertSeparator;
            if (IsSpecialCharacter(current)) continue;
            if (insertSeparator && !isFirstCharacter)
            {
                newString[newIndex] = '_';
                newIndex++;
            }

            newString[newIndex] = char.ToUpperInvariant(c);
            isFirstCharacter = false;
            insertSeparator = false;
            newIndex++;
        }

        return new string(newString);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateSpanSizeForKebabOrSnakeCase(ReadOnlySpan<char> text)
    {
        var previous = char.GetUnicodeCategory(text[0]);
        var skips = IsSpecialCharacter(previous) ? 1 : 0;
        var divs = 0;
        for (var i = 1; i < text.Length; i++)
        {
            var current = char.GetUnicodeCategory(text[i]);
            skips += IsSpecialCharacter(current) ? 1 : 0;
            divs += previous != current && current is UnicodeCategory.UppercaseLetter or UnicodeCategory.DecimalDigitNumber ? 1 : 0;
            previous = current;
        }

        return divs - skips;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSpecialCharacter(UnicodeCategory category)
    {
        return category is not UnicodeCategory.UppercaseLetter
            and not UnicodeCategory.LowercaseLetter
            and not UnicodeCategory.DecimalDigitNumber;
    }
}