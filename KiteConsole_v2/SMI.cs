using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KiteConsole_v2
{
    public class SMI : IIndicator
    {
        private static readonly int Period = 10;
        private static readonly int SmoothPeriod = 3;

        public PositionSettings StartLooking(PositionSettings positionSettings, Kite kite)
        {
            List<Historical> historical = Program.GetHistoricalData(kite, positionSettings.InstrumentToken, GetIndianDateTime().AddDays(-160), GetIndianDateTime(), Constants.INTERVAL_60MINUTE, false);
            if (historical.Count < 18) { positionSettings.CMode = IndicatorMode.None; return positionSettings; };

            List<SMIAttributes> sMIAttributes = new List<SMIAttributes>();

            sMIAttributes = historical.Select(x => new SMIAttributes
            {
                Open = x.Open,
                High = x.High,
                Low = x.Low,
                Close = x.Close,
                TimeStamp = x.TimeStamp
            }).OrderBy(x => x.TimeStamp).ToList();


            for (int i = 0; i < sMIAttributes.Count - 1; i++)
            {
                int index = Period - 1;
                if (index + i < sMIAttributes.Count)
                {
                    sMIAttributes[index + i].HighMax = sMIAttributes.Skip(i).Take(Period).Max(x => x.High);
                    sMIAttributes[index + i].LowMin = sMIAttributes.Skip(i).Take(Period).Min(x => x.Low);
                    sMIAttributes[index + i].Median = Decimal.Divide((sMIAttributes[index + i].HighMax + sMIAttributes[index + i].LowMin), 2);
                    sMIAttributes[index + i].H = sMIAttributes[index + i].Close - sMIAttributes[index + i].Median;
                    sMIAttributes[index + i].Smooth = Decimal.Divide(2, SmoothPeriod + 1);
                    sMIAttributes[index + i].MaxMinusMin = sMIAttributes[index + i].HighMax - sMIAttributes[index + i].LowMin;
                }
                else
                    break;
            }

            for (int i = 0; i < sMIAttributes.Count - 1; i++)
            {
                int index = Period - 1;
                //if (index + i == 13)
                if (index + i == index)

                {
                    sMIAttributes[index + i + SmoothPeriod - 1].Hs1EMA = sMIAttributes.Skip(index).Take(SmoothPeriod).Average(x => x.H);
                    sMIAttributes[index + i + SmoothPeriod - 1].Dhl1 = sMIAttributes.Skip(index).Take(SmoothPeriod).Average(x => x.MaxMinusMin);

                }
                else if (index + i + SmoothPeriod - 1 < sMIAttributes.Count)
                {
                    sMIAttributes[index + i + SmoothPeriod - 1].Hs1EMA = (sMIAttributes[index + i + SmoothPeriod - 1].H - sMIAttributes[index + SmoothPeriod + i - 2].Hs1EMA) * sMIAttributes[index + SmoothPeriod + i - 1].Smooth + sMIAttributes[index + SmoothPeriod + i - 2].Hs1EMA;
                    sMIAttributes[index + i + SmoothPeriod - 1].Dhl1 = (sMIAttributes[index + i + SmoothPeriod - 1].MaxMinusMin - sMIAttributes[index + SmoothPeriod + i - 2].Dhl1) * sMIAttributes[index + SmoothPeriod + i - 1].Smooth + sMIAttributes[index + SmoothPeriod + i - 2].Dhl1;
                }
            }

            for (int i = 0; i < sMIAttributes.Count - 1; i++)
            {
                int index = Period - 1;
                //if (index + i == 13)
                if (index + i == index)

                {
                    sMIAttributes[index + i + 4].Hs2EMA = sMIAttributes.Skip(index + 2).Take(SmoothPeriod).Average(x => x.Hs1EMA);
                    sMIAttributes[index + i + 4].Dhl2EMA = sMIAttributes.Skip(index + 2).Take(SmoothPeriod).Average(x => x.Dhl1);
                    sMIAttributes[index + i + 4].Dhl2 = Decimal.Divide(sMIAttributes[index + i + 4].Dhl2EMA, 2);

                }
                else if (index + i + 4 < sMIAttributes.Count)
                {
                    sMIAttributes[index + i + 4].Hs2EMA = (sMIAttributes[index + i + 4].Hs1EMA - sMIAttributes[index + i + 3].Hs2EMA) * sMIAttributes[index + i + 3].Smooth + sMIAttributes[index + i + 3].Hs2EMA;
                    sMIAttributes[index + i + 4].Dhl2EMA = (sMIAttributes[index + i + 4].Dhl1 - sMIAttributes[index + i + 3].Dhl2EMA) * sMIAttributes[index + i + 3].Smooth + sMIAttributes[index + i + 3].Dhl2EMA;
                    sMIAttributes[index + i + 4].Dhl2 = Decimal.Divide(sMIAttributes[index + i + 4].Dhl2EMA, 2);
                }
            }

            foreach (SMIAttributes att in sMIAttributes)
            {
                if (att.Dhl2 != 0)
                    att.SMI = Decimal.Divide(att.Hs2EMA, att.Dhl2) * 100;
            }

            //Signal Line Calculation
            for (int i = 0; i < sMIAttributes.Count - 1; i++)
            {
                int index = Period - 1;
                //if (index + i == 13)
                if (index + i == index)

                {
                    sMIAttributes[index + i + Period + 3].SignalLine = sMIAttributes.Skip(index + i + 4).Take(Period).Average(x => x.SMI);

                }
                else if (index + i + Period + 3 < sMIAttributes.Count)
                {
                    sMIAttributes[index + i + Period + 3].SignalLine = (sMIAttributes[index + i + Period + 3].SMI - sMIAttributes[index + i + Period + 3 - 1].SignalLine) * Decimal.Divide(2, Period + 1) + sMIAttributes[index + i + Period + 3 - 1].SignalLine;
                }
            }

            if (sMIAttributes[sMIAttributes.Count - 1].SMI > sMIAttributes[sMIAttributes.Count - 1].SignalLine)
            {
                positionSettings.CMode = IndicatorMode.Buy;
                positionSettings.CurrentMode = IndicatorMode.Buy;
            }
            if (sMIAttributes[sMIAttributes.Count - 1].SMI < sMIAttributes[sMIAttributes.Count - 1].SignalLine)
            {
                positionSettings.CMode = IndicatorMode.Sell;
                positionSettings.CurrentMode = IndicatorMode.Sell;

            }


            positionSettings.NewLongEMA = sMIAttributes[sMIAttributes.Count - 1].SMI;
            positionSettings.NewShortEMA = sMIAttributes[sMIAttributes.Count - 1].SignalLine;

            if (positionSettings.TradeOnCrossOver == true)
                positionSettings = CheckCrossOver(positionSettings);

            positionSettings = LogCrossOver(positionSettings); //Log Crossover

            if (positionSettings.TradeOnCrossOverUpdate == true)
            {
                positionSettings.CMode = IndicatorMode.None;
                positionSettings.TradeOnCrossOver = true;
                positionSettings.TradeOnCrossOverUpdate = false;
            }

            //Remove false signals 
            if (positionSettings.CMode != IndicatorMode.None)
            {
                positionSettings = CheckforStableMode(positionSettings);
            }
            UpdateDB(positionSettings);

            return positionSettings;

        }
        private void UpdateDB(PositionSettings positionSettings)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string commandText = "UPDATE [PostionSettings] SET [LongEMA] =" + positionSettings.NewLongEMA +
                                                                ", [ShortEMA]=" + positionSettings.NewShortEMA +
                                                                ",[TradeOnCrossOverUpdate]='" + positionSettings.TradeOnCrossOverUpdate +
                                                                "',[TradeOnCrossOver]='" + positionSettings.TradeOnCrossOver +
                                                                "',[FMode]='" + positionSettings.CurrentMode +
                                                                "',[StableMode]='" + positionSettings.StableMode +
                                                                "',[LastRecorded]='" + positionSettings.LastRecorded.ToString("yyyy-MM-dd HH:mm:ss") +
                                                                "',LastCrossOverTime='" + positionSettings.LastCrossOverTime.ToString("yyyy-MM-dd HH:mm:ss") +
                                                                "',[LastUpdateTime] = '" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") +
                                                                "' WHERE [TradingSymbol]='" + positionSettings.TradingSymbol + "'";

                SqlCommand command = new SqlCommand(commandText, conn);
                command.CommandType = System.Data.CommandType.Text;
                command.ExecuteNonQuery();
                conn.Close();
            }
        }

        private PositionSettings CheckforStableMode(PositionSettings positionSettings)
        {
            if (positionSettings.StableTime == 0)
                positionSettings.StableTime = 30;

            if (positionSettings.PMode == IndicatorMode.None)
            {
                positionSettings.LastRecorded = GetIndianDateTime().AddMinutes(-positionSettings.StableTime);
                Log(positionSettings.Exchange + " CrossOverLog0", positionSettings.TradingSymbol, positionSettings.NewShortEMA,
                    positionSettings.NewLongEMA, 0, 0, positionSettings.CMode, positionSettings);
                return positionSettings;
            }

            if (positionSettings.CMode != positionSettings.PMode)
            {
                if (GetIndianDateTime() < positionSettings.LastRecorded.AddMinutes(positionSettings.StableTime))
                {
                    positionSettings.LastRecorded = GetIndianDateTime();
                    positionSettings.CMode = IndicatorMode.None;
                    Log(positionSettings.Exchange + " CrossOverLog1", positionSettings.TradingSymbol, positionSettings.NewShortEMA,
                        positionSettings.NewLongEMA, 0, 0, positionSettings.CMode, positionSettings);

                    return positionSettings;
                }
                else
                {
                    positionSettings.StableMode = positionSettings.CMode;
                    positionSettings.LastRecorded = GetIndianDateTime();
                    Log(positionSettings.Exchange + " CrossOverLog2", positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                        0, 0, positionSettings.CMode, positionSettings);
                    return positionSettings;
                }
            }

            if (positionSettings.CMode == positionSettings.PMode)
            {
                if (GetIndianDateTime() < positionSettings.LastRecorded.AddMinutes(positionSettings.StableTime))
                {
                    //positionSettings.CMode = IndicatorMode.None;
                    positionSettings.CMode = positionSettings.StableMode;
                }
            }
            return positionSettings;
        }
        private PositionSettings LogCrossOver(PositionSettings positionSettings)
        {
            if ((positionSettings.ShortEMA > positionSettings.LongEMA) && (positionSettings.NewShortEMA > positionSettings.NewLongEMA))
            {
            }
            else if ((positionSettings.ShortEMA < positionSettings.LongEMA) && (positionSettings.NewShortEMA < positionSettings.NewLongEMA))
            {
            }
            else
            {
                Log(positionSettings.Exchange + " CrossOverLog", positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, 0, 0, positionSettings.CMode, positionSettings);

                positionSettings.IsCrossOver = true;
                positionSettings.LastCrossOverTime = GetIndianDateTime();
            }
            return positionSettings;
        }
        private PositionSettings CheckCrossOver(PositionSettings positionSettings)
        {
            if ((positionSettings.ShortEMA > positionSettings.LongEMA) && (positionSettings.NewShortEMA > positionSettings.NewLongEMA))
            {
                positionSettings.CMode = IndicatorMode.None;
            }
            else if ((positionSettings.ShortEMA < positionSettings.LongEMA) && (positionSettings.NewShortEMA < positionSettings.NewLongEMA))
            {
                positionSettings.CMode = IndicatorMode.None;
            }
            else
            {
                positionSettings.TradeOnCrossOver = false;
                positionSettings.CMode = IndicatorMode.None;
            }
            return positionSettings;
        }
       
        private static void Log(string exchange, string tradingSymbol, decimal item1, decimal item2, int futureCount, decimal bidPrice, IndicatorMode currentMode, PositionSettings positionSettings)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string currentSettings = JsonConvert.SerializeObject(positionSettings);
                SqlCommand command = new SqlCommand("INSERT INTO [LogSettings]([Exchange],[TradingSymbol],[Item1],[Item2],[BidPrice],[Mode]" +
                    ",[Count],CreateTime,Json) SELECT '" + exchange + "', '" + tradingSymbol + "', " + item1 + ", " + item2 + ", " + bidPrice + ",'" +
                    currentMode.ToString() + "', " + futureCount + ",'" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") + "','" + currentSettings + "'", conn)
                {
                    CommandType = System.Data.CommandType.Text
                };
                command.ExecuteNonQuery();
                conn.Close();
            }
        }
        public static DateTime GetIndianDateTime()
        {
            var indianZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var timeInIndia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indianZone);//.ToString("yyyy-MM-dd HH:mm:ss");
            return timeInIndia;
        }
    }

    public class SMIAttributes
    {
        internal decimal SignalLine;

        public DateTime TimeStamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal HighMax { get; set; }
        public decimal LowMin { get; set; }
        public decimal Median { get; set; }
        public decimal H { get; set; }
        public decimal Smooth { get; set; }
        public decimal Hs1EMA { get; set; }
        public decimal Hs2EMA { get; set; }
        public decimal MaxMinusMin { get; set; }
        public decimal Dhl1 { get; set; }
        public decimal Dhl2 { get; set; }
        public decimal SMI { get; set; }
        public decimal SMIEMA { get; set; }
        public decimal Dhl2EMA { get; internal set; }
    }
}
