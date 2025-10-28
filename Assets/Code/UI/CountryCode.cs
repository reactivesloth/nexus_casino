using System;
using UnityEngine;

namespace Code.UI
{
    public static class CountryCode
    {
        public static int GetCodeByLocale(SystemLanguage language)
        {
            return language switch
            {
                SystemLanguage.Unknown => 380,
                SystemLanguage.Dutch => 31,
                SystemLanguage.English => 44,
                SystemLanguage.Russian => 7,
                SystemLanguage.Ukrainian => 380,
                SystemLanguage.Afrikaans => 27,
                SystemLanguage.Arabic => 971,
                SystemLanguage.Belarusian => 375,
                SystemLanguage.Bulgarian => 359,
                SystemLanguage.Catalan => 39,
                SystemLanguage.Chinese => 86,
                SystemLanguage.Czech => 420,
                SystemLanguage.Danish => 45,
                SystemLanguage.Estonian => 372,
                SystemLanguage.Faroese => 278,
                SystemLanguage.Finnish => 358,
                SystemLanguage.French => 33,
                SystemLanguage.German => 49,
                SystemLanguage.Greek => 30,
                SystemLanguage.Hebrew => 972,
                SystemLanguage.Hungarian => 36,
                SystemLanguage.Icelandic => 354,
                SystemLanguage.Indonesian => 62,
                SystemLanguage.Italian => 39,
                SystemLanguage.Japanese => 81,
                SystemLanguage.Korean => 82,
                SystemLanguage.Latvian => 371,
                SystemLanguage.Lithuanian => 370,
                SystemLanguage.Norwegian => 47,
                SystemLanguage.Polish => 48,
                SystemLanguage.Portuguese => 351,
                SystemLanguage.Romanian => 40,
                SystemLanguage.SerboCroatian => 381,
                SystemLanguage.Slovak => 421,
                SystemLanguage.Slovenian => 386,
                SystemLanguage.Spanish => 34,
                SystemLanguage.Swedish => 46,
                SystemLanguage.Thai => 66,
                SystemLanguage.Turkish => 90,
                SystemLanguage.Vietnamese => 84,
                SystemLanguage.ChineseSimplified => 86,
                SystemLanguage.ChineseTraditional => 86,
                SystemLanguage.Hindi => 91,
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
            };
        }
    }
}