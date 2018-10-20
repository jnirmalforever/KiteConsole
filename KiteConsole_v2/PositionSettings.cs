using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KiteConsole_v2
{
    public class PositionSettings
    {
        internal IndicatorMode StableMode;

        public int Id { get; set; }
        public string TradingSymbol { get; set; }
        public string InstrumentToken { get; set; }
        public string Exchange { get; set; }
        public int LotSize { get; set; }
        public int MaxPostion { get; set; }
        public int ProfitSquareOff { get; set; }
        public bool BuyOnlyOption { get; set; }
        public string Indicator { get; set; }
        public bool TradeOnCrossOver { get; set; }
        public decimal LongEMA { get; set; }
        public decimal ShortEMA { get; set; }
        public decimal NewLongEMA { get; set; }
        public decimal NewShortEMA { get; set; }
        public bool TradeOnCrossOverUpdate { get; set; }
        public IndicatorMode PMode { get; set; }
        public IndicatorMode CMode { get; set; }
        public DateTime LastRecorded { get; set; }
        public int StableTime { get; set; }
        public int Target { get; set; }
        public bool IsTargetAchieved { get; set; }
        public bool IsCrossOver { get; set; } = false;
        public DateTime LastCrossOverTime { get; set; }
        public int TotalPosition { get; set; }
        public bool Active { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public IndicatorMode CurrentMode { get; internal set; }
        public int SquareOffTarget { get; internal set; }
        public IndicatorMode pcCMode { get; internal set; }
        public IndicatorMode pcCurrentMode { get; internal set; }
        public string ParentToken { get; internal set; }
        public string Interval { get; internal set; }
        public decimal PL { get; internal set; }
        public DateTime Expiry { get; internal set; }
    }
}
