using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    public class MAttributes
    {
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
        public IndicatorMode Mode { get; set; }
        public decimal PL { get; internal set; }
        public decimal EMA1 { get; internal set; }
        public decimal EMA2 { get; internal set; }
        public IndicatorMode MMode { get; internal set; }
    }

    public class Momentum : IIndicator
    {
        public Settings StartLooking(Settings positionSettings, Kite kite)
        {
            positionSettings.CMode = IndicatorMode.None;
            positionSettings.IsCrossOver = false;

            List<Historical> historical;
            if (positionSettings.Interval == null)
            {
                positionSettings.Interval = Constants.INTERVAL_60MINUTE;
            }

            if (positionSettings.Interval == Constants.INTERVAL_30MINUTE || positionSettings.Interval == Constants.INTERVAL_15MINUTE)
            {
                historical = Program.GetHistoricalData(kite, "81153", GetIndianDateTime().AddDays(-180), GetIndianDateTime(), positionSettings.Interval, false);
            }
            else if (string.IsNullOrEmpty(positionSettings.ParentToken))
                historical = Program.GetHistoricalData(kite, positionSettings.InstrumentToken, GetIndianDateTime().AddDays(-300), GetIndianDateTime(), positionSettings.Interval, false);
            else
                historical = Program.GetHistoricalData(kite, positionSettings.ParentToken, GetIndianDateTime().AddDays(-300), GetIndianDateTime(), positionSettings.Interval, false);


            if (historical.Count > 0)
            {
                //decimal longSMA = Decimal.Divide(historical.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                //decimal shortSMA = Decimal.Divide(historical.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                //                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<MAttributes> MAttributes = historical.OrderBy(x => x.TimeStamp).Select(x => new MAttributes
                {
                    Close = x.Close,
                    TimeStamp = x.TimeStamp,
                    High = x.High,
                    Low = x.Low,
                    Open = x.Open
                }).OrderBy(x => x.TimeStamp).ToList();


                foreach (MAttributes att in MAttributes)
                {

                    int i = MAttributes.IndexOf(att);

                    if (i > Constants.MomentumPeriod - 1)
                    {
                        att.Vx = MAttributes[i - Constants.MomentumPeriod].Close;
                        att.Momentum = att.Close - att.Vx;
                    }
                }

                decimal multiplier = Decimal.Divide(2, (4 + 1));
                decimal shortSMA = Decimal.Divide(MAttributes.OrderBy(x => x.TimeStamp).Skip(14)
                                           .Take(4).Sum(x => x.Momentum), 4);

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                foreach (MAttributes att in MAttributes)
                {
                    int i = MAttributes.IndexOf(att);
                    if (i == Constants.MomentumPeriod + 4)
                    {
                        att.EMA1 = (att.Momentum - shortSMA) * multiplier + shortSMA;
                    }
                    else if (i > Constants.MomentumPeriod + 4)
                    {
                        att.EMA1 = (att.Momentum - MAttributes[i - 1].EMA1) * multiplier + MAttributes[i - 1].EMA1;
                    }
                }

                multiplier = Decimal.Divide(2, (14 + 1));
                shortSMA = Decimal.Divide(MAttributes.OrderBy(x => x.TimeStamp).Skip(14)
                                          .Take(14).Sum(x => x.Momentum), 14);

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                foreach (MAttributes att in MAttributes)
                {
                    int i = MAttributes.IndexOf(att);
                    if (i == Constants.MomentumPeriod + 14)
                    {
                        att.EMA2 = (att.Momentum - shortSMA) * multiplier + shortSMA;
                    }
                    else if (i > Constants.MomentumPeriod + 14)
                    {
                        att.EMA2 = (att.Momentum - MAttributes[i - 1].EMA2) * multiplier + MAttributes[i - 1].EMA2;
                    }
                }

                foreach (MAttributes att in MAttributes)
                {
                    int i = MAttributes.IndexOf(att);
                    if (i > Constants.MomentumPeriod + 14)
                    {
                        if (att.EMA1 > att.EMA2)
                        {
                            att.MMode = IndicatorMode.Buy;
                        }
                        else
                            att.MMode = IndicatorMode.Sell;
                    }
                }

                SuperTrend(positionSettings, kite, MAttributes);

                decimal totalPL = 0;
                foreach (MAttributes st in MAttributes)
                {
                    if (st.PL != 0)
                        totalPL += st.PL;
                }
                int count = 0;
                foreach (MAttributes st in MAttributes)
                {
                    if (st.PL != 0)
                        count += 1;
                }
                totalPL = 0;
                foreach (MAttributes st in MAttributes)
                {
                    if (st.TimeStamp >= GetIndianDateTime().AddDays(-60))
                    {

                        if (st.PL != 0)
                            totalPL += st.PL;
                    }
                }

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    positionSettings.CurrentMode = MAttributes[MAttributes.Count - 1].Mode;
                    positionSettings.CMode = MAttributes[MAttributes.Count - 1].Mode;
                }

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    //positionSettings.NewLongEMA = MAttributes[MAttributes.Count - 1].SAR;
                    positionSettings.NewShortEMA = MAttributes[MAttributes.Count - 1].Close;
                }


                if (positionSettings.TradeOnCrossOver == true)
                    positionSettings = CheckCrossOver(positionSettings);

                positionSettings = LogCrossOver(positionSettings); //Log Crossover

                if (positionSettings.TradeOnCrossOverUpdate == true)
                {
                    positionSettings.CMode = IndicatorMode.None;
                    positionSettings.TradeOnCrossOver = true;
                    positionSettings.TradeOnCrossOverUpdate = false;
                }

                UpdateDB(positionSettings);
            }
            return positionSettings;
        }

        public Settings SuperTrend(Settings positionSettings, Kite kite, List<MAttributes> MAttributesInput)
        {
            positionSettings.CMode = IndicatorMode.None;
            positionSettings.IsCrossOver = false;

            //List<Historical> historical;
            //if (positionSettings.Interval == null)
            //{
            //    positionSettings.Interval = Constants.INTERVAL_60MINUTE;
            //}

            //if (positionSettings.Interval == Constants.INTERVAL_30MINUTE || positionSettings.Interval == Constants.INTERVAL_15MINUTE)
            //{
            //    historical = Program.GetHistoricalData(kite, positionSettings.ParentInstrument, GetIndianDateTime().AddDays(-180), GetIndianDateTime(), positionSettings.Interval, false);
            //}
            //else if (string.IsNullOrEmpty(positionSettings.ParentToken))
            //    historical = Program.GetHistoricalData(kite, positionSettings.InstrumentToken, GetIndianDateTime().AddDays(-300), GetIndianDateTime(), positionSettings.Interval, false);
            //else
            //    historical = Program.GetHistoricalData(kite, positionSettings.ParentToken, GetIndianDateTime().AddDays(-300), GetIndianDateTime(), positionSettings.Interval, false);


            if (MAttributesInput.Count > 0)
            {
                //decimal longSMA = Decimal.Divide(historical.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                //decimal shortSMA = Decimal.Divide(historical.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                //                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<MAttributes> MAttributes = MAttributesInput.OrderBy(x => x.TimeStamp).Select(x => new MAttributes
                {
                    Close = x.Close,
                    TimeStamp = x.TimeStamp,
                    High = x.High,
                    Low = x.Low,
                    Open = x.Open,
                    MMode = x.MMode,
                    EMA1 = x.EMA1,
                    EMA2 = x.EMA2
                }).OrderBy(x => x.TimeStamp).ToList();


                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                foreach (MAttributes att in MAttributes)
                {
                    int i = MAttributes.IndexOf(att);
                    if (i == 0)
                    {

                    }
                    else
                    {
                        decimal findMax = Math.Max((MAttributes[i].High - MAttributes[i].Low), Math.Abs(MAttributes[i].High - MAttributes[i - 1].Close));
                        att.TrueRange = Math.Max(Math.Abs(MAttributes[i].Low - MAttributes[i - 1].Close), findMax);
                    }
                    if (i == Constants.SuperTrend)
                    {
                        att.ATR = Decimal.Divide(MAttributes.OrderBy(x => x.TimeStamp).Skip(1).Take(Constants.SuperTrend).Sum(x => x.TrueRange), Constants.SuperTrend);
                    }
                }

                //decimal multiplier = Decimal.Divide(Convert.ToDecimal(Constants.STMultiplier), (Constants.SuperTrend + 1));
                decimal multiplier = Decimal.Divide(Convert.ToDecimal(positionSettings.IndicatorParmOne), (Constants.SuperTrend + 1));

                List<MAttributes> fMAttributes = MAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.SuperTrend).Select(x => new MAttributes
                {
                    Close = x.Close,
                    TimeStamp = x.TimeStamp,
                    High = x.High,
                    Low = x.Low,
                    Open = x.Open,
                    ATR = x.ATR,
                    TrueRange = x.TrueRange,
                    MMode = x.MMode,
                    EMA1 = x.EMA1,
                    EMA2 = x.EMA2
                }).OrderBy(x => x.TimeStamp).ToList();

                //TrueRange EMA (Zerodha Chart)
                foreach (MAttributes att in fMAttributes)
                {
                    int i = fMAttributes.IndexOf(att);
                    if (i > 0)
                    {
                        att.ATR = (att.TrueRange - fMAttributes[i - 1].ATR) * multiplier + fMAttributes[i - 1].ATR;
                    }
                }
                //TrueRange Average (PI Chart)
                //foreach (MAttributes att in fMAttributes)
                //{
                //    int i = fMAttributes.IndexOf(att);
                //    att.ATR = Decimal.Divide(MAttributes.OrderBy(x => x.TimeStamp).Skip(i + 1).Take(Constants.SuperTrend).Sum(x => x.TrueRange), Constants.SuperTrend);
                //}

                //Current ATR = [(Prior ATR x 13) + Current TR] / 14
                //foreach (MAttributes att in fMAttributes)
                //{
                //    int i = fMAttributes.IndexOf(att);
                //    if (i > 0)
                //    {
                //        att.ATR = ((fMAttributes[i - 1].ATR * Constants.SuperTrend - 1) + att.TrueRange) / Constants.SuperTrend;
                //    }
                //}

                //BASIC UPPERBAND = (HIGH + LOW) / 2 + Multiplier * ATR
                //BASIC LOWERBAND = (HIGH + LOW) / 2 - Multiplier * ATR
                foreach (MAttributes att in fMAttributes)
                {
                    //att.basicUpperBand = Decimal.Divide((att.High + att.Low), 2) + Decimal.Multiply(Convert.ToDecimal(Constants.STMultiplier), att.ATR);
                    //att.basicLowerBand = Decimal.Divide((att.High + att.Low), 2) - Decimal.Multiply(Convert.ToDecimal(Constants.STMultiplier), att.ATR);

                    att.basicUpperBand = Decimal.Divide((att.High + att.Low), 2) + Decimal.Multiply(Convert.ToDecimal(positionSettings.IndicatorParmOne), att.ATR);
                    att.basicLowerBand = Decimal.Divide((att.High + att.Low), 2) - Decimal.Multiply(Convert.ToDecimal(positionSettings.IndicatorParmOne), att.ATR);
                }


                //FINAL UPPERBAND = IF((Current BASICUPPERBAND < Previous FINAL UPPERBAND) and(Previous Close > Previous FINAL UPPERBAND)) THEN(Current BASIC UPPERBAND) ELSE Previous FINALUPPERBAND)
                //FINAL LOWERBAND = IF((Current BASIC LOWERBAND > Previous FINAL LOWERBAND) and(Previous Close < Previous FINAL LOWERBAND)) THEN(Current BASIC LOWERBAND) ELSE Previous FINAL LOWERBAND)
                foreach (MAttributes att in fMAttributes)
                {
                    int i = fMAttributes.IndexOf(att);
                    if (i == 0)
                    {
                        att.finalLowerBand = att.basicLowerBand;
                        att.finalUpperBand = att.basicUpperBand;
                        continue;
                    }
                    if ((att.basicUpperBand < fMAttributes[i - 1].finalUpperBand) || (fMAttributes[i - 1].Close > fMAttributes[i - 1].finalUpperBand))
                    {
                        att.finalUpperBand = att.basicUpperBand;
                    }
                    else
                    {
                        att.finalUpperBand = fMAttributes[i - 1].finalUpperBand;
                    }

                    if ((att.basicLowerBand > fMAttributes[i - 1].finalLowerBand) || (fMAttributes[i - 1].Close < fMAttributes[i - 1].finalLowerBand))
                    {
                        att.finalLowerBand = att.basicLowerBand;
                    }
                    else
                    {
                        att.finalLowerBand = fMAttributes[i - 1].finalLowerBand;
                    }
                }

                positionSettings.NewLongEMA = 0;
                positionSettings.NewShortEMA = 0;

                foreach (MAttributes att in fMAttributes)
                {
                    int i = fMAttributes.IndexOf(att);

                    if (i == 0 && att.Close <= att.finalUpperBand)
                    {
                        att.Mode = IndicatorMode.Sell;
                        continue;
                    }
                    else if (i == 0)
                    {
                        att.Mode = IndicatorMode.Buy;
                        continue;
                    }

                    if (fMAttributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        if (att.Close <= att.finalLowerBand)
                        {
                            att.Mode = IndicatorMode.Sell;
                        }
                        else
                        {
                            att.Mode = fMAttributes[i - 1].Mode;
                        }
                    }
                    else if (fMAttributes[i - 1].Mode == IndicatorMode.Sell)
                    {
                        if (att.Close <= att.finalUpperBand)
                        {
                            att.Mode = fMAttributes[i - 1].Mode;
                        }
                        else
                        {
                            att.Mode = IndicatorMode.Buy;
                        }
                    }
                }

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    positionSettings.CurrentMode = fMAttributes[fMAttributes.Count - 2].Mode;
                    positionSettings.CMode = fMAttributes[fMAttributes.Count - 2].Mode;
                }
                else
                {
                    positionSettings.CurrentMode = fMAttributes[fMAttributes.Count - 1].Mode;
                    positionSettings.CMode = fMAttributes[fMAttributes.Count - 1].Mode;
                }

                //if (positionSettings.CMode == IndicatorMode.Buy)
                //{
                //    positionSettings.NewLongEMA = fMAttributes[fMAttributes.Count - 1].finalLowerBand;
                //    positionSettings.NewShortEMA = fMAttributes[fMAttributes.Count - 1].Close;
                //}
                //else
                //{
                //    positionSettings.NewLongEMA = fMAttributes[fMAttributes.Count - 1].finalUpperBand;
                //    positionSettings.NewShortEMA = fMAttributes[fMAttributes.Count - 1].Close;
                //}

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    if (positionSettings.CMode == IndicatorMode.Buy)
                    {
                        positionSettings.NewLongEMA = fMAttributes[fMAttributes.Count - 2].finalLowerBand;
                        positionSettings.NewShortEMA = fMAttributes[fMAttributes.Count - 1].Close;

                    }
                    else
                    {
                        positionSettings.NewLongEMA = fMAttributes[fMAttributes.Count - 2].finalUpperBand;
                        positionSettings.NewShortEMA = fMAttributes[fMAttributes.Count - 1].Close;
                    }
                }

                //Calculate PL
                var pls = fMAttributes[0].Close;
                decimal currentPL = 00;
                foreach (MAttributes st in fMAttributes)
                {
                    int i = fMAttributes.IndexOf(st);

                    if (i > 0 && fMAttributes[i].Mode != fMAttributes[i - 1].Mode)
                    {
                        var ple = (fMAttributes.Count - 1 == i) ? fMAttributes[i].Open : fMAttributes[i + 1].Open;
                        if (fMAttributes[i - 1].Mode == IndicatorMode.Buy)
                        {
                            positionSettings.PL = Decimal.Subtract(ple, pls);
                            fMAttributes[i].PL = Decimal.Subtract(ple, pls);

                        }
                        else
                        {
                            positionSettings.PL = Decimal.Subtract(pls, ple);
                            fMAttributes[i].PL = Decimal.Subtract(pls, ple);
                        }
                        //if (positionSettings.PL == Convert.ToDecimal(173.50) || positionSettings.PL == Convert.ToDecimal(406.30))
                        //{

                        //}
                        pls = (fMAttributes.Count - 1 == i) ? fMAttributes[i].Open : fMAttributes[i + 1].Open;
                        //if (pls == 2250.95M)
                        //{

                        //}
                    }
                    if (fMAttributes[fMAttributes.Count - 1].Mode == IndicatorMode.Buy)
                        currentPL = Decimal.Subtract(fMAttributes[fMAttributes.Count - 1].Close, pls);
                    else if (fMAttributes[fMAttributes.Count - 1].Mode == IndicatorMode.Sell)
                        currentPL = Decimal.Subtract(pls, fMAttributes[fMAttributes.Count - 1].Close);
                }

                decimal totalPL = 0;
                foreach (MAttributes st in fMAttributes)
                {
                    if (st.PL != 0)
                    {
                        totalPL += st.PL;
                        //if (positionSettings.TotalPL < st.PL)
                        //{
                        //    positionSettings.TotalPL = st.PL;
                        //    positionSettings.PointsInfo = st.TimeStamp.ToString() + st.Close.ToString();
                        //}
                    }
                }
                int count = 0;
                foreach (MAttributes st in fMAttributes)
                {
                    if (st.PL != 0)
                        count += 1;
                }

                if (positionSettings.IsTestRun && positionSettings.TotalPL < totalPL)
                {
                    //if (positionSettings.IndicatorParmOne > 1.9)
                    //{
                    positionSettings.TotalPL = totalPL;
                    positionSettings.PointsInfo = positionSettings.IndicatorParmOne.ToString();
                    //}
                }

                totalPL = totalPL - (count * 4);
                totalPL = 0;
                count = 0;
                foreach (MAttributes st in fMAttributes)
                {
                    if (st.TimeStamp >= GetIndianDateTime().AddDays(-60))
                    {

                        if (st.PL != 0)
                            totalPL += st.PL;
                        if (st.PL != 0)
                            count += 1;
                    }
                }
                totalPL = totalPL - (count * 4);
                if (positionSettings.TradeOnCrossOver == true)
                    positionSettings = CheckCrossOver(positionSettings);

                positionSettings = LogCrossOver(positionSettings); //Log Crossover

                if (positionSettings.TradeOnCrossOverUpdate == true)
                {
                    positionSettings.CMode = IndicatorMode.None;
                    positionSettings.TradeOnCrossOver = true;
                    positionSettings.TradeOnCrossOverUpdate = false;
                }

                //Remove false signals //Becz, we are using half hour delay already
                //if (positionSettings.CMode != IndicatorMode.None)
                //{
                //    positionSettings = CheckforStableMode(positionSettings);
                //}

                UpdateDB(positionSettings);
            }
            return positionSettings;
        }

        private static void Log(string exchange, string tradingSymbol, decimal item1, decimal item2, int futureCount, decimal bidPrice, IndicatorMode currentMode, Settings positionSettings)
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

        private Settings LogCrossOver(Settings positionSettings)
        {
            if (positionSettings.CurrentMode != positionSettings.PMode)
            {
                positionSettings.TradeOnCrossOver = false;
                positionSettings.IsCrossOver = true;
                positionSettings.LastCrossOverTime = GetIndianDateTime();
                Log(positionSettings.Exchange + " CrossOverLog", positionSettings.TradingSymbol, positionSettings.NewShortEMA, positionSettings.NewLongEMA, 0, 0, positionSettings.CMode, positionSettings);
            }
            return positionSettings;
        }

        private Settings CheckCrossOver(Settings positionSettings)
        {
            if (positionSettings.CurrentMode != positionSettings.PMode)
            {
                positionSettings.TradeOnCrossOver = false;
                return positionSettings;
            }
            positionSettings.CMode = IndicatorMode.None;
            return positionSettings;
        }
        private Settings CheckforStableMode(Settings positionSettings)
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



        private void UpdateDB(Settings positionSettings)
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
                                                                                           //timeInIndia = Convert.ToDateTime("01-09-2018 01:55:32.000");
            return timeInIndia;
        }

        public Position StartLooking(Settings positionSettings, Kite kite, List<Attributes> Attributes)
        {
            throw new NotImplementedException();
        }
    }
}
