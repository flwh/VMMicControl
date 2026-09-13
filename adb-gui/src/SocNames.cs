using System;
using System.Collections.Generic;

namespace AdbGui
{
    /// <summary>
    /// SoC 代号 → 市场通用型号 的对照表。
    /// 用于把 ro.soc.model 里的内部代号（如 SM7325）翻译成用户熟悉的型号（如 骁龙 778G）。
    /// 只收录可确认的对应关系，未收录的代号原样输出，避免猜错。
    /// </summary>
    internal static class SocNames
    {
        private static readonly Dictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // ---- 高通 骁龙 8 系 / 旗舰 ----
            { "SM8850", "骁龙 8 Elite Gen 5" },
            { "SM8750", "骁龙 8 Elite" },
            { "SM8735", "骁龙 8s Gen 4" },
            { "SM8650", "骁龙 8 Gen 3" },
            { "SM8635", "骁龙 8s Gen 3" },
            { "SM8550", "骁龙 8 Gen 2" },
            { "SM8475", "骁龙 8+ Gen 1" },
            { "SM8450", "骁龙 8 Gen 1" },
            { "SM8350", "骁龙 888" },
            { "SM8250-AC", "骁龙 870" },
            { "SM8250", "骁龙 865" },
            { "SM8150-AC", "骁龙 855 Plus" },
            { "SM8150", "骁龙 855" },

            // ---- 高通 骁龙 7 系 ----
            { "SM7750", "骁龙 7 Gen 4" },
            { "SM7675", "骁龙 7+ Gen 3" },
            { "SM7550", "骁龙 7 Gen 3" },
            { "SM7475", "骁龙 7+ Gen 2" },
            { "SM7450", "骁龙 7 Gen 1" },
            { "SM7325", "骁龙 778G" },
            { "SM7250", "骁龙 765G" },
            { "SM7225", "骁龙 750G" },
            { "SM7150", "骁龙 730G" },

            // ---- 高通 骁龙 6/4 系 ----
            { "SM6450", "骁龙 6 Gen 1" },
            { "SM6375", "骁龙 695" },
            { "SM6225", "骁龙 680" },
            { "SM6125", "骁龙 665" },
            { "SM6115", "骁龙 662" },
            { "SM4450", "骁龙 4 Gen 2" },
            { "SM4375", "骁龙 4 Gen 1" },
            { "SM4350", "骁龙 480" },

            // ---- 联发科 天玑 ----
            { "MT6991", "天玑 9400" },
            { "MT6989", "天玑 9300" },
            { "MT6985", "天玑 9200" },
            { "MT6983", "天玑 9000" },
            { "MT6897", "天玑 8300" },
            { "MT6896", "天玑 8200" },
            { "MT6895", "天玑 8100" },
            { "MT6877", "天玑 900" },
            { "MT6873", "天玑 800" },
            { "MT6853", "天玑 720" },
            { "MT6833", "天玑 700" },

            // ---- 联发科 曦力 ----
            { "MT6789", "曦力 G99" },
            { "MT6785", "曦力 G95" },

            // ---- 海思 麒麟 ----
            { "KIRIN9000E", "麒麟 9000E" },
            { "KIRIN9000", "麒麟 9000" },
            { "KIRIN990", "麒麟 990" },
            { "KIRIN985", "麒麟 985" },
            { "KIRIN820", "麒麟 820" },
            { "KIRIN810", "麒麟 810" },

            // ---- 三星 Exynos ----
            { "S5E9925", "Exynos 2200" },
            { "S5E8835", "Exynos 1480" },
            { "EXYNOS2100", "Exynos 2100" },
            { "EXYNOS1380", "Exynos 1380" },
            { "EXYNOS1280", "Exynos 1280" },

            // ---- Google Tensor ----
            { "GS101", "Tensor" },
            { "GS201", "Tensor G2" },
            { "ZUMA", "Tensor G3" },
            { "ZUMAPRO", "Tensor G4" },
        };

        /// <summary>把 SoC 代号翻译成通用型号；未收录时返回 null。</summary>
        internal static string Translate(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;

            string c = Normalize(code);
            string name;
            if (Map.TryGetValue(c, out name)) return name;

            // 带后缀的代号（SM7325-AE）先整体查，再退化到主干（SM7325）
            int dash = c.IndexOf('-');
            if (dash > 0 && Map.TryGetValue(c.Substring(0, dash), out name)) return name;

            return null;
        }

        /// <summary>厂商名中文化（QTI → 高通）；未收录时返回 null。</summary>
        internal static string TranslateMaker(string maker)
        {
            if (string.IsNullOrEmpty(maker)) return null;

            switch (Normalize(maker))
            {
                case "QTI":
                case "QUALCOMM":
                case "QUALCOMMTECHNOLOGIES":
                    return "高通";
                case "MEDIATEK":
                case "MTK":
                    return "联发科";
                case "HISILICON":
                    return "海思";
                case "SAMSUNG":
                    return "三星";
                case "GOOGLE":
                    return "Google";
                case "UNISOC":
                case "SPREADTRUM":
                    return "紫光展锐";
                case "APPLE":
                    return "苹果";
            }
            return null;
        }

        /// <summary>统一大小写并去掉空格、下划线等分隔符，便于查表。</summary>
        private static string Normalize(string s)
        {
            return s.Trim().ToUpperInvariant()
                    .Replace(" ", "")
                    .Replace("_", "")
                    .Replace("\t", "")
                    .Replace(",", "")
                    .Replace(".", "");
        }
    }
}
