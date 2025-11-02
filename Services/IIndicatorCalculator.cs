// Services/IIndicatorCalculator.cs - ОБНОВЛЕН ДЛЯ ПОДДЕРЖКИ BID/ASK VOLUME
using MarketMicrostructureWebService.Models;

namespace MarketMicrostructureWebService.Services
{
    /// <summary>
    /// Интерфейс для калькуляторов индикаторов
    /// Каждый индикатор реализует этот интерфейс
    /// ОБНОВЛЕН: Добавлены методы для работы с закрытием свечей
    /// </summary>
    public interface IIndicatorCalculator
    {
        /// <summary>
        /// Уникальное название индикатора
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Описание индикатора
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Символ инструмента для расчета
        /// </summary>
        string Symbol { get; set; }
        
        /// <summary>
        /// Расчет значения индикатора
        /// </summary>
        /// <param name="orderBook">Снимок стакана</param>
        /// <param name="candle">Данные свечи (опционально)</param>
        /// <returns>Данные индикатора или null если расчет невозможен</returns>
        Task<IndicatorData?> CalculateAsync(OrderBookSnapshot orderBook, CandleData? candle = null);
        
        /// <summary>
        /// Получение истории расчетов индикатора
        /// </summary>
        /// <returns>Список исторических данных</returns>
        Task<List<IndicatorData>> GetHistoryAsync();
        
        /// <summary>
        /// Инициализация индикатора
        /// </summary>
        Task<bool> InitializeAsync();
        
        /// <summary>
        /// Сброс состояния индикатора
        /// </summary>
        Task<bool> ResetAsync();

        /// <summary>
        /// Обработка закрытия свечи (для индикаторов, которые накапливают данные в течение бара)
        /// </summary>
        /// <param name="symbol">Символ инструмента</param>
        /// <param name="timeframe">Таймфрейм</param>
        /// <param name="candle">Данные закрывшейся свечи</param>
        Task ProcessCandleCloseAsync(string symbol, string timeframe, CandleData candle);

        /// <summary>
        /// Получение диагностической информации индикатора
        /// </summary>
        /// <returns>Диагностические данные</returns>
        object GetDiagnostics();
    }
}