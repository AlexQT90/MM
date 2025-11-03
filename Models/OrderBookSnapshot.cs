// Models/OrderBookSnapshot.cs - ИСПРАВЛЕННАЯ ВЕРСИЯ
using System.Collections.Generic;

namespace MarketMicrostructureWebService.Models
{
    /// <summary>
    /// Улучшенный снимок стакана заявок с исправлениями для стабильной работы.
    /// Добавлены проверки на null и улучшено клонирование.
    /// ИСПРАВЛЕНА: Проблема с вычислением BestBid и BestAsk
    /// </summary>
    public class OrderBookSnapshot
    {
        /// <summary>
        /// Символ инструмента (например, "BTCUSDT").
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Временная метка снимка (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Заявки на покупку (bids): цена -> объем. Сортировка по убыванию цены.
        /// ИСПРАВЛЕНА: Инициализация словаря с правильным компаратором
        /// </summary>
        public SortedDictionary<decimal, decimal> Bids { get; set; } = new SortedDictionary<decimal, decimal>(Comparer<decimal>.Create((x, y) => y.CompareTo(x)));

        /// <summary>
        /// Заявки на продажу (asks): цена -> объем. Сортировка по возрастанию цены.
        /// </summary>
        public SortedDictionary<decimal, decimal> Asks { get; set; } = new SortedDictionary<decimal, decimal>();

        /// <summary>
        /// Последний ID обновления от Binance для синхронизации.
        /// </summary>
        public long LastUpdateId { get; set; }

        /// <summary>
        /// Лучшая цена покупки (best bid).
        /// ИСПРАВЛЕНА: В словаре bids, отсортированном по убыванию, первый элемент - это максимальная цена (лучший bid)
        /// </summary>
        public decimal BestBid 
        { 
            get 
            {
                if (Bids == null || Bids.Count == 0)
                    return 0;
                
                // ВАЖНО: Bids отсортирован по убыванию, поэтому первый элемент - самая высокая цена покупки
                return Bids.Keys.First();
            }
        }

        /// <summary>
        /// Лучшая цена продажи (best ask).
        /// ИСПРАВЛЕНА: В словаре asks, отсортированном по возрастанию, первый элемент - это минимальная цена (лучший ask)
        /// </summary>
        public decimal BestAsk 
        { 
            get 
            {
                if (Asks == null || Asks.Count == 0)
                    return 0;
                
                // ВАЖНО: Asks отсортирован по возрастанию, поэтому первый элемент - самая низкая цена продажи
                return Asks.Keys.First();
            }
        }

        /// <summary>
        /// Спред между best bid и best ask.
        /// ИСПРАВЛЕНА: Добавлена проверка на валидность цен
        /// </summary>
        public decimal Spread 
        { 
            get 
            {
                var bid = BestBid;
                var ask = BestAsk;
                return (bid > 0 && ask > 0 && ask > bid) ? ask - bid : 0;
            }
        }

        /// <summary>
        /// Средняя цена (mid price).
        /// ИСПРАВЛЕНА: Добавлена проверка на валидность цен
        /// </summary>
        public decimal MidPrice 
        { 
            get 
            {
                var bid = BestBid;
                var ask = BestAsk;
                return (bid > 0 && ask > 0) ? (bid + ask) / 2 : 0;
            }
        }

        /// <summary>
        /// Клонирование снимка для безопасной передачи (избегает race conditions).
        /// ИСПРАВЛЕНА: Улучшена безопасность клонирования
        /// </summary>
        /// <returns>Копия снимка.</returns>
        public OrderBookSnapshot Clone()
        {
            var clone = new OrderBookSnapshot
            {
                Symbol = this.Symbol,
                Timestamp = this.Timestamp,
                LastUpdateId = this.LastUpdateId,
                Bids = new SortedDictionary<decimal, decimal>(Comparer<decimal>.Create((x, y) => y.CompareTo(x))),
                Asks = new SortedDictionary<decimal, decimal>()
            };

            // Безопасное копирование bids
            if (this.Bids != null)
            {
                foreach (var kv in this.Bids)
                {
                    if (kv.Key > 0 && kv.Value >= 0) // Проверка валидности данных
                    {
                        clone.Bids[kv.Key] = kv.Value;
                    }
                }
            }

            // Безопасное копирование asks
            if (this.Asks != null)
            {
                foreach (var kv in this.Asks)
                {
                    if (kv.Key > 0 && kv.Value >= 0) // Проверка валидности данных
                    {
                        clone.Asks[kv.Key] = kv.Value;
                    }
                }
            }

            return clone;
        }

	//МЕТОД ДЛЯ ПРОВЕРКИ ЦЕН С TICKSIZE
    	public bool IsPriceValid(decimal price, decimal tickSize)
    	{
        	if (tickSize <= 0) return true;
        
        	// Проверяем, что цена соответствует tickSize
        	decimal remainder = price % tickSize;
        	return remainder == 0;
    	}

    	// ДОБАВИТЬ МЕТОД ДЛЯ ОКРУГЛЕНИЯ ЦЕНЫ ПО TICKSIZE
    	public decimal RoundToTickSize(decimal price, decimal tickSize)
    	{
        	if (tickSize <= 0) return price;
        
        	decimal rounded = Math.Round(price / tickSize) * tickSize;
        	return rounded;
    	}
    }
}