// Services/TimeframeManager.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ ДЛЯ ВСЕХ ТАЙМФРЕЙМОВ
using System.Collections.Concurrent;
using MarketMicrostructureWebService.Models;
using System.Globalization;

namespace MarketMicrostructureWebService.Services
{
    /// <summary>
    /// МЕНЕДЖЕР ТАЙМФРЕЙМОВ - Обработка свечей для всех таймфреймов
    /// ИСПРАВЛЕНО: Правильное определение времени начала бара для всех таймфреймов
    /// ИСПРАВЛЕНО: Событие закрытия свечи вызывается для КАЖДОГО таймфрейма
    /// </summary>
    public class TimeframeManager
    {
        private readonly ILogger<TimeframeManager> _logger;
        
        #region Поддерживаемые таймфреймы
        
        // Словарь поддерживаемых таймфреймов и их интервалов
        private readonly Dictionary<string, TimeSpan> _supportedTimeframes = new()
        {
            ["15s"] = TimeSpan.FromSeconds(15),
            ["30s"] = TimeSpan.FromSeconds(30),
            ["1m"] = TimeSpan.FromMinutes(1),
            ["5m"] = TimeSpan.FromMinutes(5),
            ["15m"] = TimeSpan.FromMinutes(15),
            ["1h"] = TimeSpan.FromHours(1)
        };
        
        #endregion
        
        #region Структуры данных для хранения свечей
        
        // Хранилище исторических свечей: Symbol_Timeframe -> List<CandleData>
        private readonly ConcurrentDictionary<string, List<CandleData>> _historicalCandles = new();
        
        // Текущие формирующиеся свечи: Symbol_Timeframe -> CandleData
        private readonly ConcurrentDictionary<string, CandleData> _currentCandles = new();
        
        // Время начала текущих свечей: Symbol_Timeframe -> DateTime
        private readonly ConcurrentDictionary<string, DateTime> _currentCandleTimes = new();
        
        #endregion
        
        #region Статистика и мониторинг
        
        private readonly ConcurrentDictionary<string, int> _candleUpdateCounts = new();
        private DateTime _serviceStartTime = DateTime.UtcNow;
        private int _totalCandlesCreated = 0;
        
        #endregion

        // ИСПРАВЛЕНИЕ: Событие вызывается для КАЖДОГО таймфрейма при закрытии свечи
        public event Func<string, CandleData, Task>? OnCandleClose;

        public TimeframeManager(ILogger<TimeframeManager> logger)
        {
            _logger = logger;
            _logger.LogInformation("TimeframeManager создан с поддержкой {TimeframeCount} таймфреймов: {Timeframes}", 
                _supportedTimeframes.Count, string.Join(", ", _supportedTimeframes.Keys));
        }

        /// <summary>
        /// Обработка новой сделки и обновление всех таймфреймов
        /// ИСПРАВЛЕНО: Правильное определение времени начала бара для всех таймфреймов
        /// </summary>
        public void ProcessTrade(string symbol, decimal price, decimal volume, DateTime timestamp)
        {
            try
            {
                // Убедимся, что временная метка в UTC
                if (timestamp.Kind != DateTimeKind.Utc)
                {
                    timestamp = timestamp.ToUniversalTime();
                    _logger.LogWarning("Получена временная метка не в UTC для символа {Symbol}. Конвертировано в UTC.", symbol);
                }

                // ИСПРАВЛЕНИЕ: Обновляем свечи для ВСЕХ таймфреймов
                foreach (var (timeframe, interval) in _supportedTimeframes)
                {
                    UpdateCandleForTimeframe(symbol, timeframe, interval, price, volume, timestamp);
                }
                
                // Обновляем статистику
                _candleUpdateCounts.AddOrUpdate(symbol, 1, (key, count) => count + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки трейда для символа {Symbol}", symbol);
            }
        }

        /// <summary>
        /// Обновление свечи для конкретного таймфрейма
        /// ИСПРАВЛЕНО: Правильное определение времени начала бара
        /// </summary>
        private void UpdateCandleForTimeframe(string symbol, string timeframe, TimeSpan interval, 
            decimal price, decimal volume, DateTime timestamp)
        {
            var candleKey = $"{symbol}_{timeframe}";
            var candleStartTime = GetCandleStartTime(timestamp, timeframe);
            
            try
            {
                CandleData? completedCandle = null;

                // Проверяем, существует ли текущая свеча и совпадает ли время
                if (!_currentCandles.TryGetValue(candleKey, out var currentCandle) || 
                    currentCandle.Timestamp != candleStartTime)
                {
                    // Сохраняем предыдущую свечу если она существует и валидна
                    if (currentCandle != null && currentCandle.IsValid)
                    {
                        completedCandle = currentCandle;
                        SaveCompletedCandle(candleKey, currentCandle);
                    }

                    // Создаем новую свечу с явным указанием UTC
                    currentCandle = new CandleData
                    {
                        Symbol = symbol,
                        Timeframe = timeframe,
                        Timestamp = candleStartTime,
                        Open = (double)price,
                        High = (double)price,
                        Low = (double)price,
                        Close = (double)price,
                        Volume = (double)volume
                    };
                    
                    _currentCandles[candleKey] = currentCandle;
                    _currentCandleTimes[candleKey] = candleStartTime;
                    Interlocked.Increment(ref _totalCandlesCreated);
                    
                    _logger.LogDebug("Создана новая свеча: {Symbol} {Timeframe} {Timestamp} (UTC)", 
                        symbol, timeframe, candleStartTime.ToString("yyyy-MM-dd HH:mm:ss"));

                    // ИСПРАВЛЕНИЕ: Вызываем событие о закрытии предыдущей свечи для ЭТОГО ТАЙМФРЕЙМА
                    if (completedCandle != null && OnCandleClose != null)
                    {
                        _ = Task.Run(async () => 
                        {
                            try
                            {
                                await OnCandleClose.Invoke(symbol, completedCandle);
                                _logger.LogDebug("Событие закрытия свечи вызвано для {Symbol} {Timeframe}", 
                                    symbol, timeframe);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Ошибка вызова события закрытия свечи для {Symbol} {Timeframe}", 
                                    symbol, timeframe);
                            }
                        });
                    }
                }
                else
                {
                    // Обновляем существующую свечу
                    currentCandle.High = Math.Max(currentCandle.High, (double)price);
                    currentCandle.Low = Math.Min(currentCandle.Low, (double)price);
                    currentCandle.Close = (double)price;
                    currentCandle.Volume += (double)volume;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обновления свечи {CandleKey}", candleKey);
            }
        }

        /// <summary>
        /// Расчет времени начала бара для указанного таймфрейма
        /// ИСПРАВЛЕНО: Правильное округление времени до начала бара
        /// </summary>
        private DateTime GetCandleStartTime(DateTime timestamp, string timeframe)
        {
            try
            {
                // Убедимся, что входное время в UTC
                if (timestamp.Kind != DateTimeKind.Utc)
                {
                    timestamp = timestamp.ToUniversalTime();
                }

                switch (timeframe)
                {
                    case "15s":
                        var seconds15 = timestamp.Second / 15 * 15;
                        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                                          timestamp.Hour, timestamp.Minute, seconds15, 0, DateTimeKind.Utc);
                    
                    case "30s":
                        var seconds30 = timestamp.Second / 30 * 30;
                        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                                          timestamp.Hour, timestamp.Minute, seconds30, 0, DateTimeKind.Utc);
                    
                    case "1m":
                        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                                          timestamp.Hour, timestamp.Minute, 0, 0, DateTimeKind.Utc);
                    
                    case "5m":
                        var minute5 = timestamp.Minute / 5 * 5;
                        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                                          timestamp.Hour, minute5, 0, 0, DateTimeKind.Utc);
                    
                    case "15m":
                        var minute15 = timestamp.Minute / 15 * 15;
                        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                                          timestamp.Hour, minute15, 0, 0, DateTimeKind.Utc);
                    
                    case "1h":
                        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                                          timestamp.Hour, 0, 0, 0, DateTimeKind.Utc);
                    
                    default:
                        // По умолчанию 1 минута
                        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                                          timestamp.Hour, timestamp.Minute, 0, 0, DateTimeKind.Utc);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка расчета времени начала бара для таймфрейма {Timeframe}", timeframe);
                return timestamp;
            }
        }

        /// <summary>
        /// Сохранение завершенной свечи в историю
        /// </summary>
        private void SaveCompletedCandle(string candleKey, CandleData completedCandle)
        {
            try
            {
                if (!completedCandle.IsValid)
                {
                    _logger.LogWarning("Попытка сохранения невалидной свечи: {CandleKey}", candleKey);
                    return;
                }

                var history = _historicalCandles.GetOrAdd(candleKey, new List<CandleData>());
                
                lock (history)
                {
                    history.Add(completedCandle);
                    
                    // Ограничиваем размер истории для предотвращения утечек памяти
                    if (history.Count > 1000)
                    {
                        history.RemoveAt(0);
                    }
                }
                
                _logger.LogDebug("Свеча сохранена в историю: {CandleKey} {ClosePrice}", 
                    candleKey, completedCandle.Close);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка сохранения свечи {CandleKey}", candleKey);
            }
        }

        /// <summary>
        /// Получение истории свечей для символа и таймфрейма
        /// </summary>
        public List<CandleData> GetCandleHistory(string symbol, string timeframe)
        {
            var candleKey = $"{symbol}_{timeframe}";
            var result = new List<CandleData>();

            try
            {
                // Добавляем исторические свечи
                if (_historicalCandles.TryGetValue(candleKey, out var history))
                {
                    lock (history)
                    {
                        result.AddRange(history);
                    }
                }

                // Добавляем текущую свечу если она существует
                if (_currentCandles.TryGetValue(candleKey, out var currentCandle) && currentCandle != null)
                {
                    result.Add(currentCandle);
                }

                // Сортируем по времени
                result = result.OrderBy(c => c.Timestamp).ToList();
                
                _logger.LogDebug("Запрошена история свечей: {Symbol} {Timeframe} - {Count} записей", 
                    symbol, timeframe, result.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка получения истории свечей для {CandleKey}", candleKey);
            }

            return result;
        }

        /// <summary>
        /// Получение списка поддерживаемых таймфреймов
        /// </summary>
        public List<string> GetSupportedTimeframes() => _supportedTimeframes.Keys.ToList();

        /// <summary>
        /// Проверка поддержки таймфрейма
        /// </summary>
        public bool IsTimeframeSupported(string timeframe) => _supportedTimeframes.ContainsKey(timeframe);

        /// <summary>
        /// Получение диагностической информации
        /// </summary>
        public object GetDiagnostics()
        {
            var activeSymbols = _currentCandles.Keys
                .Select(k => k.Split('_')[0])
                .Distinct()
                .ToList();

            return new
            {
                ServiceName = "TimeframeManager",
                ActiveSymbols = activeSymbols,
                TotalTimeframes = _supportedTimeframes.Count,
                TotalCandlesCreated = _totalCandlesCreated,
                CurrentCandlesCount = _currentCandles.Count,
                HistoricalCandlesCount = _historicalCandles.Sum(kvp => kvp.Value.Count),
                ServiceUptime = DateTime.UtcNow - _serviceStartTime,
                CandleUpdateStats = _candleUpdateCounts.ToDictionary()
            };
        }

        /// <summary>
        /// Очистка старых данных (для управления памятью)
        /// </summary>
        public void CleanupOldData(TimeSpan olderThan)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - olderThan;
                int removedCount = 0;

                foreach (var kvp in _historicalCandles)
                {
                    lock (kvp.Value)
                    {
                        removedCount += kvp.Value.RemoveAll(c => c.Timestamp < cutoffTime);
                    }
                }

                if (removedCount > 0)
                {
                    _logger.LogInformation("Очищено {RemovedCount} устаревших свечей", removedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка очистки устаревших данных");
            }
        }
    }
}