// Services/BinaryCandleStorage.cs
using System.Collections.Concurrent;
using System.IO.Compression;
using MarketMicrostructureWebService.Models;

namespace MarketMicrostructureWebService.Services
{
    /// <summary>
    /// ОПТИМИЗИРОВАННОЕ БИНАРНОЕ ХРАНИЛИЩЕ СВЕЧЕЙ
    /// УЛУЧШЕНА ЛОГИКА СОХРАНЕНИЯ: ОДИН ФАЙЛ В МИНУТУ ВМЕСТО НЕСКОЛЬКИХ В СЕКУНДУ
    /// ДОБАВЛЕНЫ БЛОКИРОВКИ ДЛЯ ИЗБЕЖАНИЯ КОНФЛИКТОВ ДОСТУПА
    /// ИСПРАВЛЕНО: Сохранение и восстановление DateTimeKind для корректной работы временных меток
    /// </summary>
    public class BinaryCandleStorage : ICandleDataStorage
    {
        private readonly ILogger<BinaryCandleStorage> _logger;
        
        #region Константы и настройки
        
        private const string DATA_DIRECTORY = "BinaryCandleData";
        private const byte FILE_FORMAT_VERSION = 3; // УВЕЛИЧЕНА ВЕРСИЯ ДЛЯ ДОБАВЛЕНИЯ DateTimeKind
        private const int MAX_FILES_PER_SYMBOL = 100;
        private const int RETENTION_DAYS = 7; // Уменьшено с 30 до 7 дней для экономии места
        
        // ОПТИМИЗАЦИЯ: Интервал между сохранениями (в миллисекундах)
        private const int SAVE_COOLDOWN_MS = 30000; // 30 секунд между сохранениями для одного символа/таймфрейма
        
        #endregion
        
        #region Статистика и блокировки
        
        private int _totalFilesSaved = 0;
        private int _totalFilesLoaded = 0;
        private long _totalBytesSaved = 0;
        private long _totalBytesLoaded = 0;
        private DateTime _lastCleanupTime = DateTime.MinValue;
        
        // ОПТИМИЗАЦИЯ: Словарь для отслеживания времени последнего сохранения по ключу (symbol_timeframe)
        private readonly ConcurrentDictionary<string, DateTime> _lastSaveTimes = new ConcurrentDictionary<string, DateTime>();
        
        // ОПТИМИЗАЦИЯ: Блокировки для предотвращения одновременной записи в один файл
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        
        #endregion

        public BinaryCandleStorage(ILogger<BinaryCandleStorage> logger)
        {
            _logger = logger;
            InitializeStorageSystem();
            _logger.LogInformation("BinaryCandleStorage инициализирован с оптимизированной логикой сохранения");
        }

        /// <summary>
        /// Инициализация системы хранения
        /// </summary>
        private void InitializeStorageSystem()
        {
            try
            {
                if (!Directory.Exists(DATA_DIRECTORY))
                {
                    Directory.CreateDirectory(DATA_DIRECTORY);
                    _logger.LogInformation("Создана директория для бинарных данных: {Directory}", DATA_DIRECTORY);
                }

                // Запуск периодической очистки старых файлов
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromHours(1)); // Каждый час
                        CleanupOldFiles();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка инициализации системы хранения");
            }
        }

        /// <summary>
        /// Нормализация имени символа для использования в путях файлов
        /// </summary>
        private string NormalizeSymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return "unknown";
                
            return symbol.Replace("/", "_")
                        .Replace("\\", "_")
                        .Replace(":", "_")
                        .Replace("*", "_")
                        .Replace("?", "_")
                        .Replace("\"", "_")
                        .Replace("<", "_")
                        .Replace(">", "_")
                        .Replace("|", "_");
        }

        /// <summary>
        /// Получение ключа для отслеживания времени сохранения
        /// </summary>
        private string GetSaveKey(string symbol, string timeframe)
        {
            return $"{NormalizeSymbol(symbol)}_{timeframe}";
        }

        /// <summary>
        /// Проверка, можно ли сохранять данные (реализация cooldown)
        /// </summary>
        private bool CanSave(string saveKey)
        {
            if (_lastSaveTimes.TryGetValue(saveKey, out var lastSave))
            {
                return (DateTime.UtcNow - lastSave).TotalMilliseconds >= SAVE_COOLDOWN_MS;
            }
            return true;
        }

        /// <summary>
        /// Сохранение данных свечи в бинарном формате с GZip сжатием
        /// ОПТИМИЗАЦИЯ: Добавлена проверка cooldown для уменьшения количества файлов
        /// ИСПРАВЛЕНО: Сохранение DateTimeKind для корректного восстановления временных меток
        /// </summary>
        public async Task<bool> SaveAsync(string symbol, string timeframe, CandleData candle)
        {
            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(timeframe) || candle == null)
            {
                _logger.LogWarning("Попытка сохранения невалидных данных: Symbol={Symbol}, Timeframe={Timeframe}", symbol, timeframe);
                return false;
            }

            if (!candle.IsValid)
            {
                _logger.LogWarning("Попытка сохранения невалидной свечи: {Symbol} {Timeframe}", symbol, timeframe);
                return false;
            }

            var saveKey = GetSaveKey(symbol, timeframe);
            
            // ОПТИМИЗАЦИЯ: Проверяем cooldown для уменьшения количества файлов
            if (!CanSave(saveKey))
            {
                _logger.LogDebug("Пропущено сохранение (cooldown) для {SaveKey}", saveKey);
                return false;
            }

            try
            {
                var normalizedSymbol = NormalizeSymbol(symbol);
                // ОПТИМИЗАЦИЯ: Используем минуты вместо секунд для уменьшения количества файлов
                var fileName = $"{normalizedSymbol}_{timeframe}_{DateTime.UtcNow:yyyyMMdd_HHmm}.bin.gz";
                var filePath = Path.Combine(DATA_DIRECTORY, fileName);

                // ОПТИМИЗАЦИЯ: Используем блокировку для предотвращения конфликтов доступа
                var fileLock = _fileLocks.GetOrAdd(filePath, new SemaphoreSlim(1, 1));
                await fileLock.WaitAsync();

                try
                {
                    using var fileStream = new FileStream(filePath, FileMode.Create);
                    using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
                    using var writer = new BinaryWriter(gzipStream);

                    // Запись заголовка файла
                    writer.Write(FILE_FORMAT_VERSION);
                    writer.Write(DateTime.UtcNow.Ticks);
                    writer.Write(symbol);
                    writer.Write(timeframe);
                    writer.Write(1);

                    // Запись данных свечи
                    WriteCandleData(writer, candle);

                    Interlocked.Increment(ref _totalFilesSaved);
                    Interlocked.Add(ref _totalBytesSaved, new FileInfo(filePath).Length);

                    // Обновляем время последнего сохранения
                    _lastSaveTimes[saveKey] = DateTime.UtcNow;

                    _logger.LogDebug("Свеча сохранена: {Symbol} {Timeframe} -> {File}", symbol, timeframe, fileName);
                    return true;
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка сохранения свечи {Symbol} {Timeframe}", symbol, timeframe);
                return false;
            }
        }

        /// <summary>
        /// Пакетное сохранение свечей (более эффективное)
        /// ОПТИМИЗАЦИЯ: Улучшенная логика с cooldown и блокировками
        /// ИСПРАВЛЕНО: Сохранение DateTimeKind для корректного восстановления временных меток
        /// </summary>
        public async Task<bool> SaveBatchAsync(string symbol, string timeframe, List<CandleData> candles)
        {
            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(timeframe) || candles == null || !candles.Any())
            {
                return false;
            }

            // Фильтруем только валидные свечи
            var validCandles = candles.Where(c => c.IsValid).ToList();
            if (!validCandles.Any())
            {
                _logger.LogWarning("Нет валидных свечей для сохранения: {Symbol} {Timeframe}", symbol, timeframe);
                return false;
            }

            var saveKey = GetSaveKey(symbol, timeframe);
            
            // ОПТИМИЗАЦИЯ: Проверяем cooldown
            if (!CanSave(saveKey))
            {
                _logger.LogDebug("Пропущено пакетное сохранение (cooldown) для {SaveKey}", saveKey);
                return false;
            }

            try
            {
                var normalizedSymbol = NormalizeSymbol(symbol);
                // ОПТИМИЗАЦИЯ: Используем минуты вместо секунд
                var fileName = $"{normalizedSymbol}_{timeframe}_{DateTime.UtcNow:yyyyMMdd_HHmm}.bin.gz";
                var filePath = Path.Combine(DATA_DIRECTORY, fileName);

                // ОПТИМИЗАЦИЯ: Блокировка файла
                var fileLock = _fileLocks.GetOrAdd(filePath, new SemaphoreSlim(1, 1));
                await fileLock.WaitAsync();

                try
                {
                    using var fileStream = new FileStream(filePath, FileMode.Create);
                    using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
                    using var writer = new BinaryWriter(gzipStream);

                    // Запись заголовка файла
                    writer.Write(FILE_FORMAT_VERSION);
                    writer.Write(DateTime.UtcNow.Ticks);
                    writer.Write(symbol);
                    writer.Write(timeframe);
                    writer.Write(validCandles.Count);

                    // Запись всех свечей
                    foreach (var candle in validCandles)
                    {
                        WriteCandleData(writer, candle);
                    }

                    var fileSize = new FileInfo(filePath).Length;
                    Interlocked.Increment(ref _totalFilesSaved);
                    Interlocked.Add(ref _totalBytesSaved, fileSize);

                    // Обновляем время последнего сохранения
                    _lastSaveTimes[saveKey] = DateTime.UtcNow;

                    _logger.LogInformation("Пакетное сохранение: {Symbol} {Timeframe} -> {Count} свечей, {Size} bytes", 
                        symbol, timeframe, validCandles.Count, fileSize);
                    return true;
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка пакетного сохранения {Symbol} {Timeframe}", symbol, timeframe);
                return false;
            }
        }

        /// <summary>
        /// Запись данных одной свечи в бинарный поток
        /// ИСПРАВЛЕНО: Сохраняем DateTimeKind для корректного восстановления временных меток
        /// </summary>
        private void WriteCandleData(BinaryWriter writer, CandleData candle)
        {
            // Сохраняем ticks и DateTimeKind
            writer.Write(candle.Timestamp.Ticks);
            writer.Write((byte)candle.Timestamp.Kind); // Сохраняем DateTimeKind как байт
            
            writer.Write(candle.Open);
            writer.Write(candle.High);
            writer.Write(candle.Low);
            writer.Write(candle.Close);
            writer.Write(candle.Volume);
            writer.Write(candle.Symbol ?? "");
            writer.Write(candle.Timeframe ?? "");
        }

        /// <summary>
        /// Загрузка исторических данных свечей
        /// ИСПРАВЛЕНО: Корректное восстановление DateTimeKind для временных меток
        /// </summary>
        public async Task<List<CandleData>> LoadAsync(string symbol, string timeframe, DateTime from, DateTime to)
        {
            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(timeframe))
            {
                return new List<CandleData>();
            }

            var result = new List<CandleData>();
            
            try
            {
                var normalizedSymbol = NormalizeSymbol(symbol);
                var searchPattern = $"{normalizedSymbol}_{timeframe}_*.bin.gz";
                
                if (!Directory.Exists(DATA_DIRECTORY))
                {
                    _logger.LogWarning("Директория {Directory} не существует", DATA_DIRECTORY);
                    return result;
                }

                var files = Directory.GetFiles(DATA_DIRECTORY, searchPattern)
                    .OrderByDescending(f => f)
                    .Take(MAX_FILES_PER_SYMBOL)
                    .ToList();

                _logger.LogDebug("Найдено {FileCount} файлов для {Symbol} {Timeframe}", files.Count, symbol, timeframe);

                foreach (var file in files)
                {
                    try
                    {
                        var fileCandles = await LoadFromFileAsync(file, from, to);
                        result.AddRange(fileCandles);
                        
                        Interlocked.Increment(ref _totalFilesLoaded);
                        Interlocked.Add(ref _totalBytesLoaded, new FileInfo(file).Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ошибка загрузки файла {File}", Path.GetFileName(file));
                    }
                }

                // Убираем дубликаты и сортируем
                result = result
                    .Where(c => c.Timestamp >= from && c.Timestamp <= to)
                    .GroupBy(c => c.Timestamp)
                    .Select(g => g.First())
                    .OrderBy(c => c.Timestamp)
                    .ToList();

                _logger.LogInformation("Загружено {CandleCount} свечей для {Symbol} {Timeframe} за период {From} - {To}", 
                    result.Count, symbol, timeframe, from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки данных для {Symbol} {Timeframe}", symbol, timeframe);
            }

            return result;
        }

        /// <summary>
        /// Загрузка данных из одного файла
        /// ИСПРАВЛЕНО: Корректное восстановление DateTimeKind и преобразование в UTC при необходимости
        /// </summary>
        private async Task<List<CandleData>> LoadFromFileAsync(string filePath, DateTime from, DateTime to)
        {
            var candles = new List<CandleData>();

            try
            {
                // ОПТИМИЗАЦИЯ: Блокировка при чтении, если файл заблокирован для записи
                var fileLock = _fileLocks.GetOrAdd(filePath, new SemaphoreSlim(1, 1));
                await fileLock.WaitAsync();

                try
                {
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                    using var reader = new BinaryReader(gzipStream);

                    var version = reader.ReadByte();
                    if (version != FILE_FORMAT_VERSION && version != 2) // Поддержка версий 2 и 3
                    {
                        _logger.LogWarning("Несовместимая версия файла {File}: ожидалась {Expected} или 2, получена {Actual}", 
                            filePath, FILE_FORMAT_VERSION, version);
                        return candles;
                    }

                    var saveTime = new DateTime(reader.ReadInt64());
                    var fileSymbol = reader.ReadString();
                    var fileTimeframe = reader.ReadString();
                    var candleCount = reader.ReadInt32();

                    for (int i = 0; i < candleCount; i++)
                    {
                        try
                        {
                            var candle = ReadCandleData(reader, version);
                            if (candle.IsValid && candle.Timestamp >= from && candle.Timestamp <= to)
                            {
                                candles.Add(candle);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Ошибка чтения свечи #{Index} из файла {File}", i, Path.GetFileName(filePath));
                        }
                    }
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки файла {File}", Path.GetFileName(filePath));
            }

            return candles;
        }

        /// <summary>
        /// Чтение данных одной свечи из бинарного потока
        /// ИСПРАВЛЕНО: Корректное восстановление DateTimeKind и преобразование в UTC при необходимости
        /// </summary>
        private CandleData ReadCandleData(BinaryReader reader, byte fileVersion)
        {
            var ticks = reader.ReadInt64();
            DateTime timestamp;
            
            if (fileVersion >= 3)
            {
                // Версия 3+: читаем сохраненный DateTimeKind
                var dateTimeKind = (DateTimeKind)reader.ReadByte();
                timestamp = new DateTime(ticks, dateTimeKind);
                
                // Если время не в UTC, конвертируем его
                if (timestamp.Kind != DateTimeKind.Utc)
                {
                    timestamp = timestamp.ToUniversalTime();
                    _logger.LogDebug("Конвертировано время из {OriginalKind} в UTC при загрузке свечи", dateTimeKind);
                }
            }
            else
            {
                // Версия 2 и ниже: предполагаем, что время было сохранено в UTC
                timestamp = new DateTime(ticks, DateTimeKind.Utc);
            }

            var open = reader.ReadDouble();
            var high = reader.ReadDouble();
            var low = reader.ReadDouble();
            var close = reader.ReadDouble();
            var volume = reader.ReadDouble();
            var symbol = reader.ReadString();
            var timeframe = reader.ReadString();

            return new CandleData
            {
                Timestamp = timestamp,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                Symbol = symbol,
                Timeframe = timeframe
            };
        }

        /// <summary>
        /// Получение списка доступных таймфреймов для символа
        /// </summary>
        public Task<List<string>> GetAvailableTimeframesAsync(string symbol)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrEmpty(symbol))
                {
                    return new List<string>();
                }

                try
                {
                    var normalizedSymbol = NormalizeSymbol(symbol);
                    var searchPattern = $"{normalizedSymbol}_*.bin.gz";
                    
                    if (!Directory.Exists(DATA_DIRECTORY))
                    {
                        _logger.LogWarning("Директория {Directory} не существует", DATA_DIRECTORY);
                        return new List<string>();
                    }

                    var files = Directory.GetFiles(DATA_DIRECTORY, searchPattern);
                    
                    var timeframes = files
                        .Select(f => 
                        {
                            var fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f));
                            var parts = fileName.Split('_');
                            return parts.Length >= 2 ? parts[1] : null;
                        })
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Distinct()
                        .ToList();

                    _logger.LogDebug("Найдено {Count} таймфреймов для {Symbol}: {Timeframes}", 
                        timeframes.Count, symbol, string.Join(", ", timeframes));

                    return timeframes;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка получения таймфреймов для {Symbol}", symbol);
                    return new List<string>();
                }
            });
        }

        /// <summary>
        /// Очистка старых файлов
        /// ОПТИМИЗАЦИЯ: Более агрессивная очистка для экономии места
        /// </summary>
        private void CleanupOldFiles()
        {
            try
            {
                if (!Directory.Exists(DATA_DIRECTORY))
                    return;

                var cutoffDate = DateTime.UtcNow.AddDays(-RETENTION_DAYS);
                var allFiles = Directory.GetFiles(DATA_DIRECTORY, "*.bin.gz");
                
                // ОПТИМИЗАЦИЯ: Группируем файлы по символам и таймфреймам, оставляем только последние N файлов
                var fileGroups = allFiles
                    .Select(f => new FileInfo(f))
                    .GroupBy(f => 
                    {
                        var name = Path.GetFileNameWithoutExtension(f.Name);
                        var parts = name.Split('_');
                        return parts.Length >= 2 ? $"{parts[0]}_{parts[1]}" : "unknown";
                    })
                    .ToList();

                var filesToDelete = new List<string>();

                foreach (var group in fileGroups)
                {
                    // Оставляем только последние MAX_FILES_PER_SYMBOL файлов для каждой группы
                    var oldFilesInGroup = group
                        .OrderByDescending(f => f.LastWriteTime)
                        .Skip(MAX_FILES_PER_SYMBOL)
                        .Select(f => f.FullName)
                        .ToList();

                    filesToDelete.AddRange(oldFilesInGroup);

                    // Также удаляем файлы старше RETENTION_DAYS
                    var expiredFiles = group
                        .Where(f => f.LastWriteTime < cutoffDate)
                        .Select(f => f.FullName)
                        .ToList();

                    filesToDelete.AddRange(expiredFiles);
                }

                // Удаляем дубликаты
                filesToDelete = filesToDelete.Distinct().ToList();

                foreach (var file in filesToDelete)
                {
                    try
                    {
                        // ОПТИМИЗАЦИЯ: Удаляем блокировку если она существует
                        _fileLocks.TryRemove(file, out _);
                        File.Delete(file);
                        _logger.LogDebug("Удален старый файл: {File}", Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ошибка удаления файла {File}", file);
                    }
                }

                if (filesToDelete.Count > 0)
                {
                    _logger.LogInformation("Очистка завершена: удалено {FileCount} старых файлов", filesToDelete.Count);
                }

                _lastCleanupTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка очистки старых файлов");
            }
        }

        /// <summary>
        /// Получение диагностической информации
        /// </summary>
        public object GetDiagnostics()
        {
            try
            {
                if (!Directory.Exists(DATA_DIRECTORY))
                {
                    return new
                    {
                        ServiceName = "BinaryCandleStorage",
                        DataDirectory = DATA_DIRECTORY,
                        TotalFiles = 0,
                        TotalSizeBytes = 0,
                        TotalSizeMB = 0,
                        FilesSaved = _totalFilesSaved,
                        FilesLoaded = _totalFilesLoaded,
                        BytesSaved = _totalBytesSaved,
                        BytesLoaded = _totalBytesLoaded,
                        LastCleanupTime = _lastCleanupTime,
                        FileFormatVersion = FILE_FORMAT_VERSION,
                        Compression = "GZip",
                        RetentionDays = RETENTION_DAYS,
                        SaveCooldownMs = SAVE_COOLDOWN_MS,
                        ActiveLocks = _fileLocks.Count,
                        TrackedSaveKeys = _lastSaveTimes.Count,
                        Status = "Directory not exists"
                    };
                }

                var allFiles = Directory.GetFiles(DATA_DIRECTORY, "*.bin.gz");
                var totalSize = allFiles.Sum(f => new FileInfo(f).Length);

                return new
                {
                    ServiceName = "BinaryCandleStorage",
                    DataDirectory = DATA_DIRECTORY,
                    TotalFiles = allFiles.Length,
                    TotalSizeBytes = totalSize,
                    TotalSizeMB = totalSize / 1024 / 1024,
                    FilesSaved = _totalFilesSaved,
                    FilesLoaded = _totalFilesLoaded,
                    BytesSaved = _totalBytesSaved,
                    BytesLoaded = _totalBytesLoaded,
                    LastCleanupTime = _lastCleanupTime,
                    FileFormatVersion = FILE_FORMAT_VERSION,
                    Compression = "GZip",
                    RetentionDays = RETENTION_DAYS,
                    SaveCooldownMs = SAVE_COOLDOWN_MS,
                    ActiveLocks = _fileLocks.Count,
                    TrackedSaveKeys = _lastSaveTimes.Count,
                    Status = "Healthy"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка получения диагностической информации");
                return new { Error = ex.Message };
            }
        }
    }
}