using MarketMicrostructureWebService.Models;

namespace MarketMicrostructureWebService.Services
{
    /// <summary>
    /// ИНТЕРФЕЙС ДЛЯ УНИВЕРСАЛЬНОГО ХРАНЕНИЯ ДАННЫХ СВЕЧЕЙ
    /// Поддерживает все таймфреймы и обеспечивает эффективное хранение
    /// Гибкая система для работы с историческими данными свечей
    /// </summary>
    public interface ICandleDataStorage
    {
        /// <summary>
        /// Сохранение данных свечи
        /// </summary>
        Task<bool> SaveAsync(string symbol, string timeframe, CandleData candle);
        
        /// <summary>
        /// Загрузка исторических данных свечей
        /// </summary>
        Task<List<CandleData>> LoadAsync(string symbol, string timeframe, DateTime from, DateTime to);
        
        /// <summary>
        /// Получение списка доступных таймфреймов для символа
        /// </summary>
        Task<List<string>> GetAvailableTimeframesAsync(string symbol);
    }
}