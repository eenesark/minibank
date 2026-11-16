namespace MiniBank.Services.Generators;

public interface IBankingCodeGenerator
{
    string GenerateCustomerNumber();
    string GenerateAccountNumber();
    string GenerateIban(string accountNumber);
    string GenerateCardNumber();
    (int Month, int Year) GenerateExpiryDate(int validYears = 4);
    string GenerateCvv();
}

public class BankingCodeGenerator : IBankingCodeGenerator
{
    private static readonly Random _random = new();

    public string GenerateCustomerNumber()
    {
        lock (_random)
        {
            return $"MB{_random.NextInt64(100_000_000, 1_000_000_000)}";
        }
    }

    public string GenerateAccountNumber()
    {
        lock (_random)
        {
            return _random.NextInt64(1000000000, 9999999999).ToString();
        }
    }

    public string GenerateIban(string accountNumber)
    {
        lock (_random)
        {
            var checkDigits = _random.Next(10, 99);
            return $"TR{checkDigits}000620{accountNumber}";
        }
    }

    public string GenerateCardNumber()
    {
        lock (_random)
        {
            // BIN: 4543 62 (MiniBank Visa/Mastercard formatı) + 10 hane
            var part1 = "4543";
            var part2 = $"62{_random.Next(10, 99)}";
            var part3 = _random.Next(1000, 9999).ToString();
            var part4 = _random.Next(1000, 9999).ToString();
            return $"{part1}{part2}{part3}{part4}";
        }
    }

    public (int Month, int Year) GenerateExpiryDate(int validYears = 4)
    {
        var now = DateTime.UtcNow;
        var month = now.Month;
        var year = (now.Year % 100) + validYears; // Örn: 2026 -> 26 + 4 = 30
        return (month, year);
    }

    public string GenerateCvv()
    {
        lock (_random)
        {
            return _random.Next(100, 999).ToString();
        }
    }
}

