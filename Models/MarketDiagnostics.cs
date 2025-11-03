// Models/MarketDiagnostics.cs - РАСШИРЕННАЯ ВЕРСИЯ
namespace MarketMicrostructureWebService.Models
{
    /// <summary>
    /// Диагностическая информация о состоянии сервиса
    /// Используется для мониторинга здоровья системы
    /// РАСШИРЕНА для поддержки WebSocket диагностики
    /// </summary>
    public class MarketDiagnostics
    {
        /// <summary>
        /// Название сервиса
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// Флаг здоровья сервиса
        /// </summary>
        public bool IsHealthy { get; set; }
        
        /// <summary>
        /// Список активных символов
        /// </summary>
        public List<string> ActiveSymbols { get; set; } = new List<string>();
        
        /// <summary>
        /// Общее количество WebSocket подключений
        /// </summary>
        public int TotalWebSockets { get; set; }
        
        /// <summary>
        /// Временная метка диагностики (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Дополнительные диагностические данные
        /// </summary>
        public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
    }
}