// Services/BinaryIndicatorStorage.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
using System.Collections.Concurrent;
using System.IO.Compression;
using MarketMicrostructureWebService.Models;

namespace MarketMicrostructureWebService.Services
{
    /// <summary>
    /// ОПТИМИЗИРОВАННОЕ БИНАРНОЕ ХРАНИЛИЩЕ ДАННЫХ ИНДИКАТОРОВ
    /// Поддерживает GZip сжатие для экономии места
    /// Потокобезопасные операции с блокировками
    /// Автоматическая ротация и очистка старых файлов
    /// ИСПРАВЛЕНО: Убрана проверка cooldown для гарантированного сохранения
    /// </summary>
    public class BinaryIndicatorStorage
    {
        private readonly ILogger<BinaryIndicatorStorage> _logger;
        
        #region Константы и настройки
        
        private const string DATA_DIRECTORY = "BinaryIndicatorData";
        private const byte FILE_FORMAT_VERSION = 1;
        private const int MAX_FILES_PER_SYMBOL = 50;
        private const int RETENTION_DAYS = 30;
        
        #endregion
        
        #region Статистика и блокировки
        
        private int _totalFilesSaved = 0;
        private int _totalFilesLoaded = 0;
        private long _totalBytesSaved = 0;
        private long _totalBytesLoaded = 0;
        private DateTime _lastCleanupTime = DateTime.MinValue;
        
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        
        #endregion

        public BinaryIndicatorStorage(ILogger<BinaryIndicatorStorage> logger)
        {
            _logger = logger;
            _logger.LogInformation("BinaryIndicatorStorage создан");
        }

        /// <summary>
        /// Инициализация системы хранения
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                if (!Directory.Exists(DATA_DIRECTORY))
                {
                    Directory.CreateDirectory(DATA_DIRECTORY);
                    _logger.LogInformation("Создана директория для бинарных данных индикаторов: {Directory}", DATA_DIRECTORY);
                }

                // Запуск периодической очистки старых файлов
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromHours(2));
                        CleanupOldFiles();
                    }
                });

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка инициализации BinaryIndicatorStorage");
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
        /// Сохранение данных индикатора в бинарном формате с GZip сжатием
        /// ИСПРАВЛЕНО: Убрана проверка cooldown для гарантированного сохранения
        /// </summary>
        public async Task<bool> SaveAsync(string symbol, string timeframe, IndicatorData indicator)
        {
            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(timeframe) || indicator == null)
            {
                _logger.LogWarning("Попытка сохранения невалидных данных индикатора: Symbol={Symbol}, Timeframe={Timeframe}", symbol, timeframe);
                return false;
            }

            if (!indicator.IsValid)
            {
                _logger.LogWarning("Попытка сохранения невалидного индикатора: {Symbol} {Timeframe}", symbol, timeframe);
                return false;
            }

            try
            {
                var normalizedSymbol = NormalizeSymbol(symbol);
                var fileName = $"{normalizedSymbol}_{timeframe}_{DateTime.UtcNow:yyyyMMdd_HHmm}.bin.gz";
                var filePath = Path.Combine(DATA_DIRECTORY, fileName);

                // Используем блокировку для предотвращения конфликтов доступа
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
                    writer.Write(1); // Количество записей

                    // Запись данных индикатора
                    WriteIndicatorData(writer, indicator);

                    Interlocked.Increment(ref _totalFilesSaved);
                    Interlocked.Add(ref _totalBytesSaved, new FileInfo(filePath).Length);

                    _logger.LogInformation("Индикатор сохранен в файл: {FilePath}", filePath);
                    return true;
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка сохранения индикатора {Symbol} {Timeframe}", symbol, timeframe);
                return false;
            }
        }

        /// <summary>
        /// Пакетное сохранение индикаторов
        /// ИСПРАВЛЕНО: Убрана проверка cooldown
        /// </summary>
        public async Task<bool> SaveBatchAsync(string symbol, string timeframe, List<IndicatorData> indicators)
        {
            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(timeframe) || indicators == null || !indicators.Any())
            {
                return false;
            }

            // Фильтруем только валидные индикаторы
            var validIndicators = indicators.Where(i => i.IsValid).ToList();
            if (!validIndicators.Any())
            {
                _logger.LogWarning("Нет валидных индикаторов для сохранения: {Symbol} {Timeframe}", symbol, timeframe);
                return false;
            }

            try
            {
                var normalizedSymbol = NormalizeSymbol(symbol);
                var fileName = $"{normalizedSymbol}_{timeframe}_{DateTime.UtcNow:yyyyMMdd_HHmm}.bin.gz";
                var filePath = Path.Combine(DATA_DIRECTORY, fileName);

                // Блокировка файла
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
                    writer.Write(validIndicators.Count);

                    // Запись всех индикаторов
                    foreach (var indicator in validIndicators)
                    {
                        WriteIndicatorData(writer, indicator);
                    }

                    var fileSize = new FileInfo(filePath).Length;
                    Interlocked.Increment(ref _totalFilesSaved);
                    Interlocked.Add(ref _totalBytesSaved, fileSize);

                    _logger.LogInformation("Пакетное сохранение индикаторов: {Symbol} {Timeframe} -> {Count} записей, {Size} bytes", 
                        symbol, timeframe, validIndicators.Count, fileSize);
                    return true;
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка пакетного сохранения индикаторов {Symbol} {Timeframe}", symbol, timeframe);
                return false;
            }
        }

        /// <summary>
        /// Запись данных одного индикатора в бинарный поток
        /// </summary>
        private void WriteIndicatorData(BinaryWriter writer, IndicatorData indicator)
        {
            // Сохраняем временную метку
            writer.Write(indicator.Timestamp.Ticks);
            writer.Write((byte)indicator.Timestamp.Kind);
            
            writer.Write(indicator.IndicatorName ?? "");
            writer.Write(indicator.Symbol ?? "");
            writer.Write(indicator.Timeframe ?? "");

            // Сохраняем значения
            writer.Write(indicator.Values.Count);
            foreach (var kvp in indicator.Values)
            {
                writer.Write(kvp.Key ?? "");
                writer.Write(kvp.Value);
            }

            // Сохраняем метаданные
            writer.Write(indicator.Metadata.Count);
            foreach (var kvp in indicator.Metadata)
            {
                writer.Write(kvp.Key ?? "");
                writer.Write(kvp.Value ?? "");
            }
        }

        /// <summary>
        /// Загрузка исторических данных индикаторов
        /// </summary>
        public async Task<List<IndicatorData>> LoadAsync(string symbol, string timeframe, DateTime from, DateTime to)
        {
            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(timeframe))
            {
                return new List<IndicatorData>();
            }

            var result = new List<IndicatorData>();
            
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

                _logger.LogInformation("Найдено {FileCount} файлов для {Symbol} {Timeframe}", files.Count, symbol, timeframe);

                foreach (var file in files)
                {
                    try
                    {
                        var fileIndicators = await LoadFromFileAsync(file, from, to);
                        result.AddRange(fileIndicators);
                        
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
                    .Where(i => i.Timestamp >= from && i.Timestamp <= to)
                    .GroupBy(i => i.Timestamp)
                    .Select(g => g.First())
                    .OrderBy(i => i.Timestamp)
                    .ToList();

                _logger.LogInformation("Загружено {IndicatorCount} индикаторов для {Symbol} {Timeframe}", result.Count, symbol, timeframe);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки данных индикаторов для {Symbol} {Timeframe}", symbol, timeframe);
            }

            return result;
        }

        /// <summary>
        /// Загрузка данных из одного файла
        /// </summary>
        private async Task<List<IndicatorData>> LoadFromFileAsync(string filePath, DateTime from, DateTime to)
        {
            var indicators = new List<IndicatorData>();

            try
            {
                // Блокировка при чтении
                var fileLock = _fileLocks.GetOrAdd(filePath, new SemaphoreSlim(1, 1));
                await fileLock.WaitAsync();

                try
                {
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                    using var reader = new BinaryReader(gzipStream);

                    var version = reader.ReadByte();
                    if (version != FILE_FORMAT_VERSION)
                    {
                        _logger.LogWarning("Несовместимая версия файла {File}: ожидалась {Expected}, получена {Actual}", 
                            filePath, FILE_FORMAT_VERSION, version);
                        return indicators;
                    }

                    var saveTime = new DateTime(reader.ReadInt64());
                    var fileSymbol = reader.ReadString();
                    var fileTimeframe = reader.ReadString();
                    var indicatorCount = reader.ReadInt32();

                    for (int i = 0; i < indicatorCount; i++)
                    {
                        try
                        {
                            var indicator = ReadIndicatorData(reader);
                            if (indicator.IsValid && indicator.Timestamp >= from && indicator.Timestamp <= to)
                            {
                                indicators.Add(indicator);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Ошибка чтения индикатора #{Index} из файла {File}", i, Path.GetFileName(filePath));
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

            return indicators;
        }

        /// <summary>
        /// Чтение данных одного индикатора из бинарного потока
        /// </summary>
        private IndicatorData ReadIndicatorData(BinaryReader reader)
        {
            var ticks = reader.ReadInt64();
            var dateTimeKind = (DateTimeKind)reader.ReadByte();
            var timestamp = new DateTime(ticks, dateTimeKind);
            
            // Конвертируем в UTC если необходимо
            if (timestamp.Kind != DateTimeKind.Utc)
            {
                timestamp = timestamp.ToUniversalTime();
            }

            var indicatorName = reader.ReadString();
            var symbol = reader.ReadString();
            var timeframe = reader.ReadString();

            var indicator = new IndicatorData
            {
                Timestamp = timestamp,
                IndicatorName = indicatorName,
                Symbol = symbol,
                Timeframe = timeframe,
                Values = new Dictionary<string, double>(),
                Metadata = new Dictionary<string, string>()
            };

            // Чтение значений
            var valuesCount = reader.ReadInt32();
            for (int i = 0; i < valuesCount; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadDouble();
                indicator.Values[key] = value;
            }

            // Чтение метаданных
            var metadataCount = reader.ReadInt32();
            for (int i = 0; i < metadataCount; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadString();
                indicator.Metadata[key] = value;
            }

            return indicator;
        }

        /// <summary>
        /// Очистка старых файлов
        /// </summary>
        private void CleanupOldFiles()
        {
            try
            {
                if (!Directory.Exists(DATA_DIRECTORY))
                    return;

                var cutoffDate = DateTime.UtcNow.AddDays(-RETENTION_DAYS);
                var allFiles = Directory.GetFiles(DATA_DIRECTORY, "*.bin.gz");
                
                // Группируем файлы по символам и таймфреймам
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
                        _fileLocks.TryRemove(file, out _);
                        File.Delete(file);
                        _logger.LogInformation("Удален старый файл индикатора: {File}", Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ошибка удаления файла {File}", file);
                    }
                }

                if (filesToDelete.Count > 0)
                {
                    _logger.LogInformation("Очистка завершена: удалено {FileCount} старых файлов индикаторов", filesToDelete.Count);
                }

                _lastCleanupTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка очистки старых файлов индикаторов");
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
                        ServiceName = "BinaryIndicatorStorage",
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
                        ActiveLocks = _fileLocks.Count,
                        Status = "Directory not exists"
                    };
                }

                var allFiles = Directory.GetFiles(DATA_DIRECTORY, "*.bin.gz");
                var totalSize = allFiles.Sum(f => new FileInfo(f).Length);

                return new
                {
                    ServiceName = "BinaryIndicatorStorage",
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
                    ActiveLocks = _fileLocks.Count,
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