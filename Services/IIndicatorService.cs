// Services/IIndicatorService.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
using MarketMicrostructureWebService.Models;

namespace MarketMicrostructureWebService.Services
{
    /// <summary>
    /// Интерфейс для сервиса индикаторов
    /// Централизованное управление всеми индикаторами
    /// Координация расчетов и хранение истории
    /// ОБНОВЛЕН: Добавлены методы для работы с BidAskAverageVolume индикатором
    /// ИСПРАВЛЕНО: Добавлен метод GetIndicatorHistoryAsync с тремя параметрами
    /// </summary>
    public interface IIndicatorService
    {
        // Основные методы
        
        /// <summary>
        /// Получение истории расчетов индикаторов
        /// </summary>
        /// <param name="symbol">Символ инструмента (опционально)</param>
        /// <returns>Список данных индикаторов</returns>
        Task<List<IndicatorData>> GetIndicatorHistoryAsync(string? symbol = null);
        
        /// <summary>
        /// Получение истории для конкретного индикатора, символа и таймфрейма
        /// ИСПРАВЛЕНО: Добавлен метод с тремя параметрами для поддержки BidAskAverageVolume
        /// </summary>
        /// <param name="indicatorName">Название индикатора</param>
        /// <param name="symbol">Символ инструмента</param>
        /// <param name="timeframe">Таймфрейм</param>
        /// <returns>Список данных индикатора</returns>
        Task<List<IndicatorData>> GetIndicatorHistoryAsync(string indicatorName, string symbol, string timeframe);
        
        /// <summary>
        /// Получение списка доступных индикаторов
        /// </summary>
        /// <returns>Список определений индикаторов</returns>
        Task<List<IndicatorDefinition>> GetAvailableIndicatorsAsync();
        
        /// <summary>
        /// Регистрация нового индикатора в системе
        /// </summary>
        /// <param name="indicator">Калькулятор индикатора</param>
        /// <returns>True если успешно зарегистрирован</returns>
        Task<bool> RegisterIndicatorAsync(IIndicatorCalculator indicator);
        
        /// <summary>
        /// Удаление индикатора из системы
        /// </summary>
        /// <param name="indicatorName">Название индикатора</param>
        /// <returns>True если успешно удален</returns>
        Task<bool> RemoveIndicatorAsync(string indicatorName);

        // Расчет индикаторов
        
        /// <summary>
        /// Расчет всех зарегистрированных индикаторов для символа
        /// </summary>
        /// <param name="symbol">Символ инструмента</param>
        /// <param name="orderBook">Снимок стакана</param>
        /// <param name="candle">Данные свечи (опционально)</param>
        Task CalculateAllIndicatorsAsync(string symbol, OrderBookSnapshot orderBook, CandleData? candle = null);
        
        /// <summary>
        /// Обработка закрытия свечи для всех индикаторов
        /// </summary>
        /// <param name="symbol">Символ инструмента</param>
        /// <param name="timeframe">Таймфрейм</param>
        /// <param name="candle">Данные закрывшейся свечи</param>
        Task ProcessCandleCloseForAllIndicatorsAsync(string symbol, string timeframe, CandleData candle);

        // Состояние сервиса
        
        /// <summary>
        /// Проверка здоровья сервиса индикаторов
        /// </summary>
        /// <returns>True если сервис здоров</returns>
        Task<bool> IsHealthyAsync();
        
        /// <summary>
        /// Получение диагностической информации по всем индикаторам
        /// </summary>
        /// <returns>Диагностические данные</returns>
        Task<object> GetDiagnosticsAsync();
    }
}