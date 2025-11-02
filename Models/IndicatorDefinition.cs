namespace MarketMicrostructureWebService.Models
{
    /// <summary>
    /// Определение индикатора для регистрации в системе
    /// Содержит метаинформацию об индикаторе
    /// </summary>
    public class IndicatorDefinition
    {
        /// <summary>
        /// Уникальное название индикатора
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Описание индикатора и его назначения
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Символ инструмента для расчета
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
    }
}