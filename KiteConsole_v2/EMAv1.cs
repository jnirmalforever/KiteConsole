using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KiteConsole_v2
{
    public class EMAv1 : IIndicator
    {
        public PositionSettings StartLooking(PositionSettings positionSettings, Kite kite)
        {
            positionSettings.CMode = IndicatorMode.None;
            positionSettings.IsCrossOver = false;

            List<Historical> historical;
            if (positionSettings.Interval == null)
            {
                positionSettings.Interval = Constants.INTERVAL_60MINUTE;
            }

            if (string.IsNullOrEmpty(positionSettings.ParentToken))
                historical = Program.GetHistoricalData(kite, positionSettings.InstrumentToken, GetIndianDateTime().AddDays(-30), GetIndianDateTime(), positionSettings.Interval, false);
            else
                historical = Program.GetHistoricalData(kite, positionSettings.ParentToken, GetIndianDateTime().AddDays(-30), GetIndianDateTime(), positionSettings.Interval, false);


            if (historical.Count > 0)
            {
                decimal longSMA = Decimal.Divide(historical.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                decimal shortSMA = Decimal.Divide(historical.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<EMAAttributes> emaAttributes = historical.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA)
                                            .Select(x => new EMAAttributes
                                            {
                                                Close = x.Close,
                                                TimeStamp = x.TimeStamp
                                            }).OrderBy(x => x.TimeStamp).ToList();

                decimal lm = Decimal.Divide(2, (Constants.longEMA + 1));
                decimal sm = Decimal.Divide(2, (Constants.shortEMA + 1));

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)


                foreach (EMAAttributes att in emaAttributes)
                {
                    int i = emaAttributes.IndexOf(att);
                    if (i == 0)
                    {
                        att.LongEMA = (att.Close - longSMA) * lm + longSMA;
                        att.ShortEMA = (att.Close - shortSMA) * sm + shortSMA;
                    }
                    else
                    {
                        att.LongEMA = (att.Close - emaAttributes[i - 1].LongEMA) * lm + emaAttributes[i - 1].LongEMA;
                        att.ShortEMA = (att.Close - emaAttributes[i - 1].ShortEMA) * sm + emaAttributes[i - 1].ShortEMA;
                    }
                }

                EMAAttributes pcClose = emaAttributes[emaAttributes.Count - 2];
                EMAAttributes ccClose = emaAttributes[emaAttributes.Count - 1];


                if (pcClose.ShortEMA > pcClose.LongEMA)
                {
                    positionSettings.pcCMode = IndicatorMode.Buy;
                    positionSettings.pcCurrentMode = IndicatorMode.Buy;
                }
                else
                {
                    positionSettings.pcCMode = IndicatorMode.Sell;
                    positionSettings.pcCurrentMode = IndicatorMode.Sell;
                }

                if (ccClose.ShortEMA > ccClose.LongEMA)
                {
                    positionSettings.CMode = IndicatorMode.Buy;
                    positionSettings.CurrentMode = IndicatorMode.Buy;
                }
                else
                {
                    positionSettings.CMode = IndicatorMode.Sell;
                    positionSettings.CurrentMode = IndicatorMode.Sell;
                }

                positionSettings.NewLongEMA = ccClose.LongEMA;
                positionSettings.NewShortEMA = ccClose.ShortEMA;

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
                    currentMode.ToString() + "', " + futureCount + ",'" + GetIndianDateTime().ToString("yyyy-MM-dd HH:mm:ss") + "','" + currentSettings + "'", conn);

                command.CommandType = System.Data.CommandType.Text;
                command.ExecuteNonQuery();
                conn.Close();
            }
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
                positionSettings.IsCrossOver = true;
                positionSettings.LastCrossOverTime = GetIndianDateTime();
                Log(positionSettings.Exchange + " CrossOverLog", positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, 0, 0, positionSettings.CMode, positionSettings);
            }
            return positionSettings;
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
                    //False signal
                    positionSettings.LastRecorded = GetIndianDateTime();
                    positionSettings.CMode = IndicatorMode.None;
                    Log(positionSettings.Exchange + " CrossOverLog1", positionSettings.TradingSymbol, positionSettings.NewShortEMA,
                        positionSettings.NewLongEMA, 0, 0, positionSettings.CMode, positionSettings);

                    return positionSettings;
                }
                else
                {
                    //Stable Mode for first order
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
                    //Wait for Stable Mode for second order onwards
                    positionSettings.CMode = IndicatorMode.None;
                    Log(positionSettings.Exchange + " CrossOverLog3", positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA,
                       0, 0, positionSettings.CMode, positionSettings);
                    //positionSettings.CMode = positionSettings.StableMode;
                }
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
        public static DateTime GetIndianDateTime()
        {
            var indianZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var timeInIndia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indianZone);//.ToString("yyyy-MM-dd HH:mm:ss");
            return timeInIndia;
        }


    }
}
