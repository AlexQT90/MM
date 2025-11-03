// Services/SimpleIndicatorCalculator.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
using MarketMicrostructureWebService.Models;

namespace MarketMicrostructureWebService.Services
{
    /// <summary>
    /// ПРОСТОЙ И РАБОЧИЙ калькулятор индикаторов
    /// ИСПРАВЛЕН: Добавлены недостающие методы интерфейса
    /// </summary>
    public class SimpleIndicatorCalculator : IIndicatorCalculator
    {
        private readonly List<IndicatorData> _history = new List<IndicatorData>();
        private readonly object _historyLock = new object();
        private readonly ILogger<SimpleIndicatorCalculator> _logger;

        public string Name => "SimpleMarketMetrics";
        public string Description => "Базовые метрики рыночной микроструктуры";
        public string Symbol { get; set; } = "BTC/USDT";

        public SimpleIndicatorCalculator(ILogger<SimpleIndicatorCalculator> logger)
        {
            _logger = logger;
            _logger.LogInformation("SimpleIndicatorCalculator создан для символа: {Symbol}", Symbol);
        }

        public async Task<IndicatorData?> CalculateAsync(OrderBookSnapshot orderBook, CandleData? candle = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (orderBook == null || orderBook.Bids.Count == 0 || orderBook.Asks.Count == 0)
                    {
                        return null;
                    }

                    // ПРОСТЫЕ РАСЧЕТЫ для демонстрации
                    decimal spread = orderBook.Spread;
                    decimal midPrice = orderBook.MidPrice;
                    decimal totalBidVolume = orderBook.Bids.Values.Sum();
                    decimal totalAskVolume = orderBook.Asks.Values.Sum();
                    decimal pressure = totalAskVolume > 0 ? totalBidVolume / totalAskVolume : 0;

                    var indicatorData = new IndicatorData
                    {
                        IndicatorName = Name,
                        Symbol = orderBook.Symbol,
                        Timestamp = DateTime.UtcNow,
                        Values = new Dictionary<string, double>
                        {
                            ["Spread"] = (double)spread,
                            ["MidPrice"] = (double)midPrice,
                            ["BidVolume"] = (double)totalBidVolume,
                            ["AskVolume"] = (double)totalAskVolume,
                            ["Pressure"] = (double)pressure,
                            ["BidLevels"] = orderBook.Bids.Count,
                            ["AskLevels"] = orderBook.Asks.Count
                        }
                    };

                    // Сохраняем в историю
                    lock (_historyLock)
                    {
                        _history.Add(indicatorData);
                        if (_history.Count > 1000) _history.RemoveAt(0);
                    }

                    return indicatorData;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка расчета индикатора");
                    return null;
                }
            });
        }

        public async Task<List<IndicatorData>> GetHistoryAsync()
        {
            return await Task.Run(() =>
            {
                lock (_historyLock)
                {
                    return new List<IndicatorData>(_history);
                }
            });
        }

        public async Task<bool> InitializeAsync()
        {
            _logger.LogInformation("Инициализация индикатора {IndicatorName}", Name);
            return await Task.FromResult(true);
        }

        public async Task<bool> ResetAsync()
        {
            lock (_historyLock)
            {
                _history.Clear();
            }
            _logger.LogInformation("Сброс индикатора {IndicatorName}", Name);
            return await Task.FromResult(true);
        }

        /// <summary>
        /// Обработка закрытия свечи (не используется в этом простом индикаторе)
        /// </summary>
        public Task ProcessCandleCloseAsync(string symbol, string timeframe, CandleData candle)
        {
            // Этот индикатор не накапливает данные по свечам
            return Task.CompletedTask;
        }

        /// <summary>
        /// Получение диагностической информации
        /// </summary>
        public object GetDiagnostics()
        {
            lock (_historyLock)
            {
                return new
                {
                    IndicatorName = Name,
                    Symbol = Symbol,
                    HistorySize = _history.Count,
                    LastCalculation = _history.LastOrDefault()?.Timestamp,
                    Status = "Active"
                };
            }
        }
    }
}