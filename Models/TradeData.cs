// Models/TradeData.cs
namespace MarketMicrostructureWebService.Models
{
    /// <summary>
    /// Данные сделки для обработки торговых операций
    /// Используется для передачи информации о сделках между сервисами
    /// </summary>
    public class TradeData
    {
        /// <summary>
        /// Символ инструмента (например, "BTC/USDT")
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// Цена сделки
        /// </summary>
        public decimal Price { get; set; }
        
        /// <summary>
        /// Объем сделки
        /// </summary>
        public decimal Quantity { get; set; }
        
        /// <summary>
        /// Временная метка сделки (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Флаг указывающий, был ли инициатором покупатель
        /// True если покупатель был мейкером (лимитная заявка)
        /// </summary>
        public bool IsBuyerMaker { get; set; }
        
        /// <summary>
        /// Уникальный идентификатор сделки
        /// </summary>
        public long TradeId { get; set; }
    }
}