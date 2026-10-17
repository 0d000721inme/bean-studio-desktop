using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MuMuBeans
{
    // Presentation only: keep the engine's confirmed/estimated recovery point unchanged.
    sealed class RecoveryDisplay
    {
        static readonly Regex number = new Regex(@"^恢复点[：:]?\s*(≈)?\s*([0-9]+(?:\.[0-9]+)?)\s*秒(?:（游戏）|（中途补算）)?$", RegexOptions.CultureInvariant);
        public readonly string Value, Caption, Status;
        public readonly bool HasValue, IsApproximate;
        RecoveryDisplay(string value, string status, bool hasValue, bool approximate)
        {
            Value = value; Caption = "游戏恢复点"; Status = status;
            HasValue = hasValue; IsApproximate = approximate;
        }
        public static RecoveryDisplay Parse(string text)
        {
            string raw = (text ?? "").Trim();
            Match match = number.Match(raw); double seconds;
            if (match.Success && Double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && seconds >= 0 && seconds <= 60)
            {
                bool approximate = match.Groups[1].Success || raw.Contains("中途补算");
                return new RecoveryDisplay((approximate ? "≈" : "") + match.Groups[2].Value,
                    approximate ? raw.Contains("中途补算") ? "估算 · 中途补算" : "估算 · 游戏时间" : "已确认 · 游戏秒", true, approximate);
            }
            string status = raw.Contains("不足冷却") ? "本回合不足冷却时间" : raw.Contains("不足") ? "本回合不足 15 秒" :
                raw.Contains("读取中") ? "读取中" : raw.Contains("未确认") ? "游戏时间未确认" : raw.Contains("等待少豆") || raw.Length == 0 ? "等待少豆" : raw;
            return new RecoveryDisplay("—", status, false, false);
        }
    }
}
