namespace Artefacts.Domain.Finance;

/// <summary>
/// ISO 4217 alphabetic currency codes, active and withdrawn (embedded statically so validation does not depend on
/// the host's globalization data). Includes precious-metal and fund codes that appear in valuations.
/// </summary>
public static class Iso4217
{
    private static readonly HashSet<string> Codes = new(StringComparer.Ordinal)
    {
        "AED", "AFN", "ALL", "AMD", "AOA", "ARS", "AUD", "AWG", "AZN", "BAM", "BBD", "BDT", "BGN", "BHD",
        "BIF", "BMD", "BND", "BOB", "BOV", "BRL", "BSD", "BTN", "BWP", "BYN", "BZD", "CAD", "CDF", "CHE",
        "CHF", "CHW", "CLF", "CLP", "CNY", "COP", "COU", "CRC", "CUP", "CVE", "CZK", "DJF", "DKK",
        "DOP", "DZD", "EGP", "ERN", "ETB", "EUR", "FJD", "FKP", "GBP", "GEL", "GHS", "GIP", "GMD", "GNF",
        "GTQ", "GYD", "HKD", "HNL", "HTG", "HUF", "IDR", "ILS", "INR", "IQD", "IRR", "ISK", "JMD",
        "JOD", "JPY", "KES", "KGS", "KHR", "KMF", "KPW", "KRW", "KWD", "KYD", "KZT", "LAK", "LBP", "LKR",
        "LRD", "LSL", "LYD", "MAD", "MDL", "MGA", "MKD", "MMK", "MNT", "MOP", "MRU", "MUR", "MVR", "MWK",
        "MXN", "MXV", "MYR", "MZN", "NAD", "NGN", "NIO", "NOK", "NPR", "NZD", "OMR", "PAB", "PEN", "PGK",
        "PHP", "PKR", "PLN", "PYG", "QAR", "RON", "RSD", "RUB", "RWF", "SAR", "SBD", "SCR", "SDG", "SEK",
        "SGD", "SHP", "SLE", "SOS", "SRD", "SSP", "STN", "SVC", "SYP", "SZL", "THB", "TJS", "TMT",
        "TND", "TOP", "TRY", "TTD", "TWD", "TZS", "UAH", "UGX", "USD", "USN", "UYI", "UYU", "UYW", "UZS",
        "VED", "VES", "VND", "VUV", "WST", "XAF", "XAG", "XAU", "XCD", "XCG", "XDR", "XOF", "XPD", "XPF",
        "XPT", "YER", "ZAR", "ZMW", "ZWG",
    };

    /// <summary>
    /// Withdrawn codes (ISO 4217 list three) that historical acquisitions and valuations were paid in,
    /// e.g. ITL (Italian lira), DEM, FRF.
    /// </summary>
    private static readonly HashSet<string> WithdrawnCodes = new(StringComparer.Ordinal)
    {
        "ADP", "AFA", "ALK", "AOK", "AON", "AOR", "ARA", "ARP", "ARY", "ATS", "AYM", "AZM", "BAD", "BEC", "BEF",
        "BEL", "BGJ", "BGK", "BGL", "BOP", "BRB", "BRC", "BRE", "BRN", "BRR", "BUK", "BYB", "BYR", "CHC", "CSD",
        "CSJ", "CSK", "CYP", "DDM", "DEM", "ECS", "ECV", "EEK", "ESA", "ESB", "ESP", "FIM", "FRF", "GEK", "GHC",
        "GHP", "GNE", "GNS", "GQE", "GRD", "GWE", "GWP", "HRD", "HRK", "IEP", "ILP", "ILR", "ISJ", "ITL", "LAJ",
        "LSM", "LTL", "LTT", "LUC", "LUF", "LUL", "LVL", "LVR", "MGF", "MLF", "MRO", "MTL", "MTP", "MVQ", "MXP",
        "MZE", "MZM", "NIC", "NLG", "PEH", "PEI", "PES", "PLZ", "PTE", "RHD", "ROK", "ROL", "RUR", "SDD", "SDP",
        "SIT", "SKK", "SLL", "SRG", "STD", "SUR", "TJR", "TMM", "TPE", "TRL", "UAK", "UGS", "UGW", "USS", "UYN",
        "UYP", "VEB", "VEF", "VNC", "XEU", "XFO", "XFU", "YDD", "YUD", "YUM", "YUN", "ZAL", "ZMK", "ZRN", "ZRZ",
        "ZWC", "ZWD", "ZWL", "ZWN", "ZWR",
    };

    /// <summary>True for active and withdrawn ISO 4217 codes.</summary>
    public static bool IsKnown(string code) => Codes.Contains(code) || WithdrawnCodes.Contains(code);

    public static bool IsWithdrawn(string code) => WithdrawnCodes.Contains(code);

    /// <summary>Every accepted code: active first, then withdrawn, each alphabetical.</summary>
    public static IReadOnlyCollection<string> All { get; } =
        [.. Codes.Order(StringComparer.Ordinal), .. WithdrawnCodes.Order(StringComparer.Ordinal)];
}
