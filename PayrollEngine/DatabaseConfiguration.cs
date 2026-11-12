using System.Data.Common;

namespace PayrollEngine;

public sealed class DatabaseConfiguration
{
    public required string ServerName { get; init; }
    public required string DatabaseName { get; init; }
    public required bool Encrypt { get; init; }
    public required bool TrustServerCertificate { get; init; }
    public required int ConnectionTimeoutSeconds { get; init; }

    public static DatabaseConfiguration Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("A DatabaseConfig.txt fájl nem található.", filePath);
        }

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rawLine, index) in File.ReadLines(filePath).Select((line, index) => (line, index)))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                throw new InvalidDataException($"Hibás beállítás a DatabaseConfig.txt {index + 1}. sorában.");
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (!settings.TryAdd(key, value))
            {
                throw new InvalidDataException($"A(z) '{key}' beállítás többször szerepel a DatabaseConfig.txt fájlban.");
            }
        }

        string Required(string name)
        {
            if (!settings.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"A(z) '{name}' beállítás hiányzik a DatabaseConfig.txt fájlból.");
            }

            return value;
        }

        var authentication = Required("Authentication");
        if (!string.Equals(authentication, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A demo jelenleg csak Windows-hitelesítést támogat.");
        }

        if (!bool.TryParse(Required("Encrypt"), out var encrypt))
        {
            throw new InvalidDataException("Az Encrypt értéke csak True vagy False lehet.");
        }

        if (!bool.TryParse(Required("TrustServerCertificate"), out var trustServerCertificate))
        {
            throw new InvalidDataException("A TrustServerCertificate értéke csak True vagy False lehet.");
        }

        if (!int.TryParse(Required("ConnectionTimeoutSeconds"), out var connectionTimeoutSeconds)
            || connectionTimeoutSeconds is < 1 or > 60)
        {
            throw new InvalidDataException("A ConnectionTimeoutSeconds értéke 1 és 60 közötti egész szám legyen.");
        }

        return new DatabaseConfiguration
        {
            ServerName = Required("ServerName"),
            DatabaseName = Required("DatabaseName"),
            Encrypt = encrypt,
            TrustServerCertificate = trustServerCertificate,
            ConnectionTimeoutSeconds = connectionTimeoutSeconds
        };
    }

    public string BuildConnectionString()
    {
        var connectionString = new DbConnectionStringBuilder
        {
            ["Server"] = ServerName,
            ["Database"] = DatabaseName,
            ["Integrated Security"] = true,
            ["Encrypt"] = Encrypt,
            ["TrustServerCertificate"] = TrustServerCertificate,
            ["Connect Timeout"] = ConnectionTimeoutSeconds
        };

        return connectionString.ConnectionString;
    }
}
