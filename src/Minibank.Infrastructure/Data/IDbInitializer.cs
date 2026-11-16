namespace MiniBank.Data;

public interface IDbInitializer
{
    /// <summary>
    /// Veritabanı ilk kez oluşturulduğunda veya boş olduğunda başlangıç tohum verilerini yükler
    /// </summary>
    Task SeedAsync();
}

