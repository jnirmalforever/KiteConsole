using System;

namespace KiteConsole_v2
{
    [Serializable]
    public class RunSettings
    {
        public int MFIEMA1 { get; set; } = 5;
        public string Instrument { get; set; }
        public int STPeriod { get; set; }
        public decimal STMultiplier { get; set; }
        public decimal STBasic { get; set; }
        public int MACDEMA1 { get; set; }
        public int MACDEMA2 { get; set; }
        public int MACDSignalLine { get; set; }
        public int RSIPeriod { get; set; }
        public int RSILong { get; set; }
        public int RSIShort { get; set; }
        public decimal Points { get; set; }
        public decimal CurrentPoints { get; set; }
        public int STMinPeriod { get; set; } = 10;
        public int STMaxPeriod { get; set; } = 30;
        public int STMinMultiplier { get; set; } = 1;
        public int STMaxMultiplier { get; set; } = 5;
        public decimal Trades { get; internal set; }
        public int STIteration { get; internal set; }
    }
}
