using System;

namespace KiteConsole_v2
{
    [Serializable]
    public class Settings
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
        public string PointsInfo { get; internal set; }
        public bool CurrentMonthExpired { get; internal set; } = false;
        public decimal Margin { get; internal set; }
        public string ParentInstrument { get; internal set; }
        public double IndicatorParmOne { get; internal set; }
        public decimal TotalPL { get; internal set; }
        public bool IsTestRun { get; internal set; } = false;
        public Int32 TradeAmount { get; internal set; }
        public string InstrumentType { get; internal set; }
        public bool IsSTEnabled { get; set; }
        public bool IsRSIEnabled { get; set; }
        public bool IsMACDEnabled { get; set; }
        public decimal STPL { get; internal set; }
        public decimal STTradeCount { get; internal set; }
        public decimal STBasicSetting { get; internal set; }
        public bool IsDepth { get; internal set; } = true;
        public bool IsAdxEnabled { get; internal set; }
        public decimal MacdNoise { get; set; } = 2;
        public bool Rule1 { get; internal set; } = true;
        public bool Rule2 { get; internal set; } = true;
        public bool Rule3 { get; internal set; } = true;
        public bool Rule4 { get; internal set; } = true;
        public bool Rule5 { get; internal set; } = true;
        public bool Rule6 { get; internal set; } = true;
        public bool Rule7 { get; internal set; } = true;
        public bool Rule8 { get; internal set; } = true;
        public bool Rule9 { get; internal set; } = true;
        public bool Rule10 { get; internal set; } = true;
        public bool Rule11 { get; internal set; } = true;
        public bool SellRSITriggered { get; internal set; }
        public bool SellRSITrigger { get; internal set; }
        public bool BuyRSITriggered { get; internal set; }
        public bool BuyRSITrigger { get; internal set; }
        public bool SellMACDTriggered { get; internal set; }
        public bool SellMACDTrigger { get; internal set; }
        public bool BuyMACDTriggered { get; internal set; }
        public bool BuyMACDTrigger { get; internal set; }
        public RunSettings runSettings { get; internal set; }
        public int MFIPeriod { get; set; } = 14;
        public bool SellMFITriggered { get; internal set; }
        public bool SellMFITrigger { get; internal set; }
        public bool BuyMFITriggered { get; internal set; }
        public bool BuyMFITrigger { get; internal set; }
        public bool MFITakeOff { get; internal set; }
        public IndicatorMode WFFMode { get; internal set; }
        public bool BuyOnLeadingSpanA { get; internal set; }
        public bool SellOnLeadingSpanB { get; internal set; }
        public bool MFIOff { get; internal set; }
        public decimal maxLeadingSpan { get; internal set; }
        public decimal minLeadingSpan { get; internal set; }
        public bool BuyonShortRule1 { get; internal set; }
        public int MFIBuyCount { get; internal set; } = 0;
        public int RiskLevel { get; internal set; }
        public DateTime FromDate { get; internal set; }
        public DateTime ToDate { get; internal set; }
    }
}
