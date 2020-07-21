using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KiteConsole_v2
{
    [Serializable]
    public class Position
    {
        public Settings PositionSettings { get; set; }
        public List<Attributes> PositionAttributes { get; set; }
    }

    public class Attributes
    {
        public decimal HAOpen { get; set; }
        public decimal HAHigh { get; set; }
        public decimal HALow { get; set; }
        public decimal HAClose { get; set; }
        public decimal Close { get; set; }
        public DateTime TimeStamp { get; set; }
        public decimal TrueRange { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal ATR { get; internal set; }
        public decimal Vx { get; internal set; }
        public decimal Momentum { get; internal set; }
        public decimal basicUpperBand { get; internal set; }
        public decimal basicLowerBand { get; internal set; }
        public decimal finalUpperBand { get; internal set; }
        public decimal finalLowerBand { get; internal set; }
        public decimal ATR1 { get; internal set; }
        public IndicatorMode STMode { get; set; }
        public IndicatorMode STMode2 { get; set; }
        public IndicatorMode Mode { get; set; }
        public IndicatorMode AdxMode { get; set; }
        public decimal PL { get; internal set; }
        public decimal MACDEMA1 { get; internal set; }
        public decimal MACDEMA2 { get; internal set; }
        public decimal RSIEMA1 { get; internal set; }
        public decimal RSIEMA2 { get; internal set; }
        public IndicatorMode RSIMode { get; internal set; }
        public decimal AverageGain { get; internal set; }
        public decimal AverageLoss { get; internal set; }
        public decimal Gain { get; internal set; }
        public decimal Loss { get; internal set; }
        public decimal RS { get; internal set; }
        public decimal RSI { get; internal set; }
        public decimal Change { get; internal set; }
        public bool IsNewSFSignal { get; internal set; }

        public bool IsNewSFSignal2 { get; internal set; }
        public bool IsRSIValid { get; internal set; } = false;
        public bool IsMACDValid { get; internal set; } = false;
        public IndicatorMode MMode { get; internal set; }
        public decimal MACD { get; internal set; }
        public decimal SignalLine { get; internal set; }
        public IndicatorMode MACDMode { get; internal set; }
        public bool EarlyBird { get; internal set; } = false;
        public decimal TotalPL { get; internal set; }
        public int TotalTrades { get; internal set; }
        public decimal TR { get; set; }
        public decimal PostiveDM { get; set; }
        public decimal NegativeDM { get; set; }
        public decimal TRn { get; set; }
        public decimal PostiveDMn { get; set; }
        public decimal NegativeDMn { get; set; }
        public decimal PostiveDIn { get; set; }
        public decimal NegativeDIn { get; set; }
        public decimal Dx { get; internal set; }
        public decimal Adx { get; internal set; }
        public bool IsAdxValid { get; internal set; }
        public decimal Diff { get; internal set; }
        public bool IsAdxUnStable { get; internal set; }
        public decimal TestValue { get; internal set; }
        public bool RSISell { get; internal set; }
        public bool RSIBuy { get; internal set; }
        public bool RSIBrought { get; internal set; }
        public object SellWait { get; internal set; }
        public bool WaitSellFlip { get; internal set; }
        public decimal TypicalPrice { get; set; }
        public decimal RawMoneyFlow { get; set; }
        public decimal PositiveMoneyFlow { get; set; }
        public decimal NegativeMoneyFlow { get; set; }
        public decimal MFI { get; set; }
        public uint Volume { get; internal set; }
        public bool PostiveMFI { get; internal set; }
        public decimal MFR { get; internal set; }
        public decimal MFIEMA1 { get; internal set; }
        public decimal ConverstionLine { get; internal set; }
        public decimal BaseLine { get; internal set; }
        public decimal LeadingSpanA { get; internal set; }
        public decimal LeadingSpanBStraight { get; internal set; }
        public decimal LeadingSpanAStraight { get; internal set; }
        public decimal LeadingSpanB { get; internal set; }
        public IndicatorMode CloudMode { get; internal set; }
        public decimal MFIEMA2 { get; internal set; }
        public HACandle HACandle { get; internal set; }
        public IndicatorMode STMode3 { get; internal set; }
        public bool IsNewSFSignal3 { get; internal set; }
        public decimal maxLeadingSpan { get; internal set; }
        public decimal minLeadingSpan { get; internal set; }
        public decimal pmaxLeadingSpan { get; internal set; }
        public decimal pminLeadingSpan { get; internal set; }
        public IndicatorMode STModeDay30Min { get; internal set; }
        public IndicatorMode STModeDay60Min { get; internal set; }
        public IndicatorMode EMAMode { get; internal set; }
        public decimal LongEMA { get; internal set; }
        public decimal ShortEMA { get; internal set; }
        public decimal AdxEMA { get; internal set; }
        public decimal MACDHistogram { get; internal set; }
        public decimal AdxParent { get; internal set; }
        public decimal AdxEMAParent { get; internal set; }
    }
}
