using System.Text.RegularExpressions;
using OtpBridge.Models;

namespace OtpBridge.Services;

public static class OtpExtractor
{
    private static readonly TimeSpan CustomRegexTimeout = TimeSpan.FromMilliseconds(250);

    private const RegexOptions SharedOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly Regex KeywordRegex = new(
        @"(验证码|校验码|动态码|code|otp)(?:[^\p{L}\p{Nd}]|是|为|您的|您|is|your|verification|passcode|password){0,20}?([A-Za-z0-9]{4,8})",
        SharedOptions | RegexOptions.Compiled);

    private static readonly Regex FallbackRegex = new(
        @"(?<![A-Za-z0-9])(?=[A-Za-z0-9]{4,8}(?![A-Za-z0-9]))(?=[A-Za-z0-9]*\d)[A-Za-z0-9]{4,8}(?![A-Za-z0-9])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AlnumCodeRegex = new(
        @"^[A-Za-z0-9]{4,8}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PositiveKeywordRegex = new(
        @"验证码|校验码|动态码|code|otp|verify|verification|passcode",
        SharedOptions | RegexOptions.Compiled);

    private static readonly string[] NegativeContextWords =
    [
        "手机号",
        "手机",
        "电话",
        "号码",
        "订单",
        "单号",
        "快递",
        "金额",
        "余额",
        "价格",
        "付款",
        "支付",
        "尾号",
        "账号",
        "账户",
        "银行卡",
        "卡号",
        "身份证",
        "￥",
        "$",
        "元"
    ];

    public static OtpExtractionResult Extract(string? message, string? customRegex)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return OtpExtractionResult.NotFound("message is empty");
        }

        var custom = TryCustomRegex(message, customRegex);
        if (custom is not null)
        {
            return OtpExtractionResult.Found(custom);
        }

        foreach (Match match in KeywordRegex.Matches(message))
        {
            var candidate = match.Groups[2].Value;
            if (IsValidCode(candidate))
            {
                return OtpExtractionResult.Found(candidate);
            }
        }

        var fallback = FallbackRegex.Matches(message)
            .Cast<Match>()
            .Where(match => IsValidCode(match.Value))
            .Where(match => !LooksObviousNonCode(message, match.Index, match.Length))
            .OrderBy(match => ScoreByKeywordDistance(message, match.Index, match.Value))
            .FirstOrDefault();

        return fallback is null
            ? OtpExtractionResult.NotFound("code not found")
            : OtpExtractionResult.Found(fallback.Value);
    }

    private static string? TryCustomRegex(string message, string? customRegex)
    {
        if (string.IsNullOrWhiteSpace(customRegex))
        {
            return null;
        }

        try
        {
            foreach (Match match in Regex.Matches(message, customRegex, SharedOptions, CustomRegexTimeout))
            {
                var candidate = PickCapture(match);
                if (candidate is not null && IsValidCode(candidate))
                {
                    return candidate;
                }
            }
        }
        catch (ArgumentException)
        {
            return null;
        }

        return null;
    }

    private static string? PickCapture(Match match)
    {
        for (var i = match.Groups.Count - 1; i >= 1; i--)
        {
            var value = match.Groups[i].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return match.Value.Trim();
    }

    private static bool IsValidCode(string value)
    {
        return AlnumCodeRegex.IsMatch(value);
    }

    private static bool LooksObviousNonCode(string message, int index, int length)
    {
        var start = Math.Max(0, index - 12);
        var end = Math.Min(message.Length, index + length + 12);
        var context = message[start..end];

        if (PositiveKeywordRegex.IsMatch(context))
        {
            return false;
        }

        var candidate = message.Substring(index, length);
        if (candidate.All(char.IsDigit) &&
            candidate.Length == 4 &&
            int.TryParse(candidate, out var numeric) &&
            numeric is >= 1900 and <= 2099)
        {
            return true;
        }

        return NegativeContextWords.Any(word => context.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static int ScoreByKeywordDistance(string message, int index, string value)
    {
        var keywordDistance = PositiveKeywordRegex.Matches(message)
            .Cast<Match>()
            .Select(match => Math.Abs(match.Index - index))
            .DefaultIfEmpty(1000)
            .Min();

        var lengthBonus = value.Length switch
        {
            6 => -30,
            5 => -10,
            4 => 10,
            _ => 0
        };

        return keywordDistance + lengthBonus;
    }
}
