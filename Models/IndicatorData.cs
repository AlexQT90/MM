// Models/IndicatorData.cs - ОБНОВЛЕН ДЛЯ ПОДДЕРЖКИ BID/ASK VOLUME
namespace MarketMicrostructureWebService.Models
{
    /// <summary>
    /// Универсальная структура данных индикатора
    /// Поддерживает различные типы индикаторов через словари значений
    /// РАСШИРЕН: Добавлены специфичные свойства для Bid/Ask Volume индикатора
    /// </summary>
    public class IndicatorData
    {
        /// <summary>
        /// Название индикатора
        /// </summary>
        public string IndicatorName { get; set; } = string.Empty;
        
        /// <summary>
        /// Символ инструмента
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// Временная метка расчета (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Таймфрейм индикатора
        /// </summary>
        public string Timeframe { get; set; } = "1m";
        
        /// <summary>
        /// Значения индикатора (название параметра -> значение)
        /// Например: {"BidAverageVolume", 1250.5}, {"AskAverageVolume", 980.3}
        /// </summary>
        public Dictionary<string, double> Values { get; set; } = new Dictionary<string, double>();
        
        /// <summary>
        /// Метаданные индикатора (настройки, версия, etc.)
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Средний объем Bid за период
        /// </summary>
        public double BidAverageVolume => Values.ContainsKey("BidAverageVolume") ? Values["BidAverageVolume"] : 0;
        
        /// <summary>
        /// Средний объем Ask за период
        /// </summary>
        public double AskAverageVolume => Values.ContainsKey("AskAverageVolume") ? Values["AskAverageVolume"] : 0;
        
        /// <summary>
        /// Количество уровней Bid
        /// </summary>
        public int BidLevelsCount => Values.ContainsKey("BidLevelsCount") ? (int)Values["BidLevelsCount"] : 0;
        
        /// <summary>
        /// Количество уровней Ask
        /// </summary>
        public int AskLevelsCount => Values.ContainsKey("AskLevelsCount") ? (int)Values["AskLevelsCount"] : 0;
        
        /// <summary>
        /// Спред между лучшими ценами
        /// </summary>
        public double Spread => Values.ContainsKey("Spread") ? Values["Spread"] : 0;
        
        /// <summary>
        /// Проверка валидности данных индикатора
        /// </summary>
        public bool IsValid => 
            !string.IsNullOrEmpty(IndicatorName) && 
            !string.IsNullOrEmpty(Symbol) &&
            Timestamp > DateTime.MinValue &&
            Values.Count > 0;
    }
}