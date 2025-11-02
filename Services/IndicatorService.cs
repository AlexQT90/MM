// Services/IndicatorService.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
using MarketMicrostructureWebService.Models;

namespace MarketMicrostructureWebService.Services
{
    /// <summary>
    /// Сервис для управления и расчета индикаторов
    /// Централизованная система для всех индикаторов
    /// ОБНОВЛЕН: Добавлена поддержка обработки закрытия свечей для всех индикаторов
    /// ИСПРАВЛЕН: Метод GetDiagnosticsAsync возвращает правильный тип
    /// ИСПРАВЛЕН: Метод GetIndicatorHistoryAsync правильно обрабатывает AdverseSelectionIndex
    /// </summary>
    public class IndicatorService : IIndicatorService
    {
        private readonly Dictionary<string, IIndicatorCalculator> _indicators;
        private readonly Dictionary<string, List<IndicatorData>> _indicatorHistory;
        private readonly object _lock = new object();
        private readonly ILogger<IndicatorService> _logger;
        private readonly BinaryIndicatorStorage _binaryStorage;

        public IndicatorService(
            ILogger<IndicatorService> logger,
            BinaryIndicatorStorage binaryStorage)
        {
            _logger = logger;
            _binaryStorage = binaryStorage;
            _indicators = new Dictionary<string, IIndicatorCalculator>(StringComparer.OrdinalIgnoreCase);
            _indicatorHistory = new Dictionary<string, List<IndicatorData>>(StringComparer.OrdinalIgnoreCase);
            
            _logger.LogInformation("IndicatorService инициализирован с поддержкой всех индикаторов");
        }

        /// <summary>
        /// Получение истории расчетов всех индикаторов
        /// </summary>
        public Task<List<IndicatorData>> GetIndicatorHistoryAsync(string? symbol = null)
        {
            return Task.Run(() =>
            {
                var allHistory = new List<IndicatorData>();
                
                lock (_lock)
                {
                    foreach (var history in _indicatorHistory.Values)
                    {
                        if (string.IsNullOrEmpty(symbol) || history.Any(h => h.Symbol == symbol))
                        {
                            allHistory.AddRange(history);
                        }
                    }
                }

                return allHistory.OrderBy(h => h.Timestamp).ToList();
            });
        }

        /// <summary>
        /// Получение истории для конкретного индикатора, символа и таймфрейма
        /// ИСПРАВЛЕНО: Добавлен метод с тремя параметрами
        /// ВАЖНО: Для AdverseSelectionIndex используем специальный метод GetHistoryAsync(symbol, timeframe)
        /// </summary>
        public Task<List<IndicatorData>> GetIndicatorHistoryAsync(string indicatorName, string symbol, string timeframe)
        {
            return Task.Run(async () =>
            {
                if (_indicators.TryGetValue(indicatorName, out var indicator))
                {
                    // Для AdverseSelectionIndex используем специальный метод
                    if (indicator is AdverseSelectionIndexIndicator adverseIndicator)
                    {
                        // ВАЖНО: Используем перегруженный метод, который возвращает исторические данные
                        return await adverseIndicator.GetHistoryAsync(symbol, timeframe);
                    }
                    
                    // Для BidAskAverageVolume используем специальный метод
                    if (indicator is BidAskAverageVolumeIndicator volumeIndicator)
                    {
                        return await volumeIndicator.GetHistoryAsync(symbol, timeframe);
                    }
                    
                    // Для других индикаторов фильтруем общую историю
                    var allHistory = await GetIndicatorHistoryAsync(symbol);
                    return allHistory
                        .Where(h => h.IndicatorName == indicatorName && h.Timeframe == timeframe)
                        .ToList();
                }

                return new List<IndicatorData>();
            });
        }

        /// <summary>
        /// Получение списка доступных индикаторов
        /// </summary>
        public Task<List<IndicatorDefinition>> GetAvailableIndicatorsAsync()
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    return _indicators.Values.Select(i => new IndicatorDefinition
                    {
                        Name = i.Name,
                        Description = i.Description,
                        Symbol = i.Symbol
                    }).ToList();
                }
            });
        }

        /// <summary>
        /// Регистрация нового индикатора в системе
        /// </summary>
        public async Task<bool> RegisterIndicatorAsync(IIndicatorCalculator indicator)
        {
            if (indicator == null)
            {
                _logger.LogWarning("Попытка регистрации null индикатора");
                return false;
            }

            try
            {
                bool success;
                
                lock (_lock)
                {
                    if (_indicators.ContainsKey(indicator.Name))
                    {
                        _logger.LogWarning("Индикатор {IndicatorName} уже зарегистрирован", indicator.Name);
                        return false;
                    }

                    _indicators[indicator.Name] = indicator;
                    _indicatorHistory[indicator.Name] = new List<IndicatorData>();
                    success = true;
                }

                if (success)
                {
                    // Асинхронная инициализация индикатора
                    var initResult = await indicator.InitializeAsync();
                    if (initResult)
                    {
                        _logger.LogInformation("Индикатор {IndicatorName} успешно зарегистрирован и инициализирован", indicator.Name);
                        return true;
                    }
                    else
                    {
                        // Откат регистрации если инициализация не удалась
                        lock (_lock)
                        {
                            _indicators.Remove(indicator.Name);
                            _indicatorHistory.Remove(indicator.Name);
                        }
                        _logger.LogError("Не удалось инициализировать индикатор {IndicatorName}", indicator.Name);
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка регистрации индикатора {IndicatorName}", indicator.Name);
                
                // Очистка в случае ошибки
                lock (_lock)
                {
                    _indicators.Remove(indicator.Name);
                    _indicatorHistory.Remove(indicator.Name);
                }
                
                return false;
            }
        }

        /// <summary>
        /// Удаление индикатора из системы
        /// </summary>
        public Task<bool> RemoveIndicatorAsync(string indicatorName)
        {
            if (string.IsNullOrEmpty(indicatorName))
            {
                _logger.LogWarning("Попытка удаления индикатора с пустым именем");
                return Task.FromResult(false);
            }

            return Task.Run(() =>
            {
                lock (_lock)
                {
                    if (_indicators.Remove(indicatorName))
                    {
                        _indicatorHistory.Remove(indicatorName);
                        _logger.LogInformation("Индикатор {IndicatorName} удален", indicatorName);
                        return true;
                    }
                    
                    _logger.LogWarning("Индикатор {IndicatorName} не найден", indicatorName);
                    return false;
                }
            });
        }

        /// <summary>
        /// Расчет всех зарегистрированных индикаторов для указанного символа
        /// ИСПРАВЛЕНО: Передаем актуальную свечу для правильного расчета времени бара
        /// </summary>
        public async Task CalculateAllIndicatorsAsync(string symbol, OrderBookSnapshot orderBook, CandleData? candle = null)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                _logger.LogWarning("Попытка расчета индикаторов для пустого символа");
                return;
            }

            if (orderBook == null)
            {
                _logger.LogWarning("Попытка расчета индикаторов с null стаканом для символа {Symbol}", symbol);
                return;
            }

            var tasks = new List<Task>();
            var calculators = new List<IIndicatorCalculator>();
            
            // Собираем калькуляторы
            lock (_lock)
            {
                calculators = _indicators.Values.ToList();
            }

            if (!calculators.Any())
            {
                return;
            }

            // Запускаем параллельные расчеты для каждого индикатора
            foreach (var indicator in calculators)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var result = await indicator.CalculateAsync(orderBook, candle);
                        
                        if (result != null)
                        {
                            lock (_lock)
                            {
                                if (_indicatorHistory.ContainsKey(indicator.Name))
                                {
                                    _indicatorHistory[indicator.Name].Add(result);
                                    
                                    // Ограничение истории для предотвращения утечек памяти
                                    if (_indicatorHistory[indicator.Name].Count > 1000)
                                    {
                                        _indicatorHistory[indicator.Name].RemoveAt(0);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка расчета индикатора {IndicatorName} для {Symbol}", indicator.Name, symbol);
                    }
                }));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при параллельном расчете индикаторов для {Symbol}", symbol);
            }
        }

        /// <summary>
        /// Обработка закрытия свечи для всех индикаторов
        /// ИСПРАВЛЕНО: Добавлен метод для обработки закрытия свечи для всех индикаторов
        /// ВАЖНО: Этот метод гарантирует сохранение исторических данных при закрытии бара
        /// </summary>
        public async Task ProcessCandleCloseForAllIndicatorsAsync(string symbol, string timeframe, CandleData candle)
        {
            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(timeframe) || candle == null)
            {
                return;
            }

            var tasks = new List<Task>();
            var calculators = new List<IIndicatorCalculator>();
            
            lock (_lock)
            {
                calculators = _indicators.Values.ToList();
            }

            foreach (var indicator in calculators)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await indicator.ProcessCandleCloseAsync(symbol, timeframe, candle);
                        _logger.LogDebug("Обработано закрытие свечи для индикатора {IndicatorName} {Symbol} {Timeframe}", 
                            indicator.Name, symbol, timeframe);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка обработки закрытия свечи для {IndicatorName} {Symbol} {Timeframe}", 
                            indicator.Name, symbol, timeframe);
                    }
                }));
            }

            if (tasks.Any())
            {
                try
                {
                    await Task.WhenAll(tasks);
                    _logger.LogDebug("Обработано закрытие свечи для {Symbol} {Timeframe} в {Count} индикаторах", 
                        symbol, timeframe, tasks.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке закрытия свечи для индикаторов {Symbol} {Timeframe}", symbol, timeframe);
                }
            }
        }

        /// <summary>
        /// Проверка здоровья сервиса индикаторов
        /// </summary>
        public Task<bool> IsHealthyAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    lock (_lock)
                    {
                        return _indicators.Any() && _indicatorHistory.Any();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при проверке здоровья IndicatorService");
                    return false;
                }
            });
        }

        /// <summary>
        /// Получение диагностической информации по всем индикаторам
        /// ИСПРАВЛЕНО: Возвращаем Task<object> вместо анонимного типа
        /// </summary>
        public Task<object> GetDiagnosticsAsync()
        {
            return Task.Run<object>(() =>
            {
                var diagnostics = new Dictionary<string, object>();
                
                lock (_lock)
                {
                    foreach (var indicator in _indicators.Values)
                    {
                        try
                        {
                            diagnostics[indicator.Name] = indicator.GetDiagnostics();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Ошибка получения диагностики для индикатора {IndicatorName}", indicator.Name);
                            diagnostics[indicator.Name] = new { Error = ex.Message };
                        }
                    }
                }

                return new
                {
                    ServiceName = "IndicatorService",
                    TotalIndicators = _indicators.Count,
                    TotalHistoryEntries = _indicatorHistory.Sum(h => h.Value.Count),
                    Indicators = diagnostics,
                    BinaryStorage = _binaryStorage.GetDiagnostics()
                };
            });
        }
    }
}