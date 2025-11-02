// Services/ISymbolInfoService.cs
using MarketMicrostructureWebService.Models;

namespace MarketMicrostructureWebService.Services
{
    /// <summary>
    /// Сервис для получения и кэширования информации о символах с бирж
    /// </summary>
    public interface ISymbolInfoService
    {
        Task<SymbolInfo?> GetSymbolInfoAsync(string symbol);
        Task<decimal> GetTickSizeAsync(string symbol);
        Task<decimal> GetStepSizeAsync(string symbol);
        Task<bool> RefreshSymbolsAsync();
        Task<IEnumerable<SymbolInfo>> GetAllSymbolsAsync();
        Task<bool> IsSymbolSupportedAsync(string symbol);
    }
}