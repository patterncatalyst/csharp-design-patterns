using System.Globalization;

namespace CarrierIntegration;

/// <summary>
/// A fake, in-memory stand-in for a decades-old flat-file carrier interface,
/// configured with the mainframe host name it would otherwise dial. Airco's
/// native call takes and returns a bare string array, a 1990s fixed-record
/// integration style predating both DTOs and JSON: index 0 is origin, index
/// 1 is destination, index 2 is weight in pounds on the way in; index 0 is
/// the dollar amount and index 1 the transit days on the way out.
/// </summary>
public sealed class AircoLegacyClient(string hostName = "AIRCO-MAINFRAME-01")
{
    public string[] Quote(string[] record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        if (record.Length != 3)
        {
            throw new ArgumentException(
                "Expected a 3-field record: origin, destination, weight in pounds.",
                nameof(record));
        }

        decimal weightPounds = decimal.Parse(record[2], CultureInfo.InvariantCulture);
        decimal amount = 9.99m + (weightPounds * 0.40m);
        int transitDays = record[0] == record[1] ? 1 : 5;

        return
        [
            amount.ToString(CultureInfo.InvariantCulture),
            transitDays.ToString(CultureInfo.InvariantCulture),
        ];
    }
}
