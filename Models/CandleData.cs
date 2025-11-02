// Models/CandleData.cs
namespace MarketMicrostructureWebService.Models
{
    /// <summary>
    /// Данные свечи для отображения ценового графика
    /// Использует UTC время для синхронизации с биржами
    /// РАСШИРЕН: Добавлена поддержка таймфреймов
    /// </summary>
    public class CandleData
    {
        /// <summary>
        /// Символ инструмента (например, "BTC/USDT")
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// Временная метка свечи (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Цена открытия
        /// </summary>
        public double Open { get; set; }
        
        /// <summary>
        /// Максимальная цена за период
        /// </summary>
        public double High { get; set; }
        
        /// <summary>
        /// Минимальная цена за период
        /// </summary>
        public double Low { get; set; }
        
        /// <summary>
        /// Цена закрытия
        /// </summary>
        public double Close { get; set; }
        
        /// <summary>
        /// Объем торгов
        /// </summary>
        public double Volume { get; set; }
        
        /// <summary>
        /// Таймфрейм свечи (15s, 30s, 1m, 5m, 15m, 1h)
        /// </summary>
        public string Timeframe { get; set; } = "1m";
        
        /// <summary>
        /// Уникальный ключ для идентификации свечи (Символ_Таймфрейм)
        /// </summary>
        public string CandleKey => $"{Symbol}_{Timeframe}";
        
        /// <summary>
        /// Проверка валидности данных свечи
        /// </summary>
        public bool IsValid => 
            !string.IsNullOrEmpty(Symbol) && 
            !string.IsNullOrEmpty(Timeframe) &&
            Timestamp > DateTime.MinValue &&
            Open > 0 && High > 0 && Low > 0 && Close > 0 &&
            High >= Low && High >= Open && High >= Close &&
            Low <= Open && Low <= Close;
    }
}