// Services/IMarketDataService.cs
using MarketMicrostructureWebService.Models;

namespace MarketMicrostructureWebService.Services
{
    /// <summary>
    /// Интерфейс для сервиса рыночных данных с поддержкой исторических данных
    /// Поддерживает множественные символы и биржи
    /// Централизованное управление данными для всех индикаторов
    /// ОБНОВЛЕН: Добавлена поддержка таймфреймов
    /// </summary>
    public interface IMarketDataService
    {
        // Основные методы получения данных
        
        /// <summary>
        /// Получение текущего снимка стакана заявок
        /// </summary>
        /// <param name="symbol">Символ инструмента (опционально)</param>
        /// <returns>Снимок стакана заявок</returns>
        Task<OrderBookSnapshot> GetOrderBookDataAsync(string? symbol = null);
        
        /// <summary>
        /// Получение истории свечных данных
        /// ОБНОВЛЕНО: Добавлен параметр timeframe
        /// </summary>
        /// <param name="symbol">Символ инструмента (опционально)</param>
        /// <param name="timeframe">Таймфрейм (15s, 30s, 1m, 5m, 15m, 1h)</param>
        /// <returns>Список свечей</returns>
        Task<List<CandleData>> GetCandleHistoryAsync(string? symbol = null, string? timeframe = null);
        
        /// <summary>
        /// Получение диагностической информации о сервисе
        /// </summary>
        /// <returns>Диагностические данные</returns>
        Task<MarketDiagnostics> GetDiagnosticsAsync();

        // Управление символами
        
        /// <summary>
        /// Получение списка доступных символов
        /// </summary>
        /// <returns>Список символов</returns>
        Task<List<string>> GetAvailableSymbolsAsync();
        
        /// <summary>
        /// Добавление нового символа для отслеживания
        /// </summary>
        /// <param name="symbol">Символ инструмента</param>
        /// <returns>True если успешно добавлен</returns>
        Task<bool> AddSymbolAsync(string symbol);
        
        /// <summary>
        /// Удаление символа из отслеживания
        /// </summary>
        /// <param name="symbol">Символ инструмента</param>
        /// <returns>True если успешно удален</returns>
        Task<bool> RemoveSymbolAsync(string symbol);

        // События для уведомления о новых данных (для индикаторов)
        
        /// <summary>
        /// Событие обновления стакана заявок
        /// </summary>
        event Func<string, OrderBookSnapshot, Task>? OnOrderBookUpdate;
        
        /// <summary>
        /// Событие новой сделки
        /// </summary>
        event Func<string, TradeData, Task>? OnTradeUpdate;
        
        /// <summary>
        /// Событие формирования новой свечи
        /// </summary>
        event Func<string, CandleData, Task>? OnNewCandle;

        // Состояние сервиса
        
        /// <summary>
        /// Проверка здоровья сервиса
        /// </summary>
        /// <returns>True если сервис здоров</returns>
        Task<bool> IsHealthyAsync();
        
        /// <summary>
        /// Запуск сервиса данных
        /// </summary>
        Task StartAsync();
        
        /// <summary>
        /// Остановка сервиса данных
        /// </summary>
        Task StopAsync();
    }
}