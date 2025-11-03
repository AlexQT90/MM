// Models/SymbolInfo.cs
namespace MarketMicrostructureWebService.Models
{
    /// <summary>
    /// Информация о символе с Binance включая спецификации контракта
    /// </summary>
    public class SymbolInfo
    {
        public string Symbol { get; set; } = string.Empty;
        public string BaseAsset { get; set; } = string.Empty;
        public string QuoteAsset { get; set; } = string.Empty;
        
        // Спецификации контракта
        public decimal TickSize { get; set; }
        public decimal StepSize { get; set; } // для объемов
        public decimal MinNotional { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        
        // Статус
        public string Status { get; set; } = string.Empty;
        public bool IsSpotTradingAllowed { get; set; }
        public bool IsMarginTradingAllowed { get; set; }
        
        // Дополнительная информация
        public int PricePrecision { get; set; }
        public int QuantityPrecision { get; set; }
        public string Exchange { get; set; } = "Binance";
        
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        public bool IsValid => 
            !string.IsNullOrEmpty(Symbol) && 
            TickSize > 0 && 
            Status == "TRADING";
    }
}