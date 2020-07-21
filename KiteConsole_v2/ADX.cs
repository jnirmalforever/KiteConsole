using KiteConnect;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    public class ADX : IIndicator
    {
        private decimal Round(decimal input)
        {
            return Math.Round(input, 2);

        }
        public Position StartLooking(Settings positionSettings, Kite kite, List<Attributes> Attributes)
        {
            if (Attributes.Count > 0)
            {
                foreach (Attributes att in Attributes)
                {
                    int i = Attributes.IndexOf(att);

                    if (i == 0)
                    {

                    }
                    else
                    {
                        decimal findMax = Math.Max((Attributes[i].High - Attributes[i].Low), Math.Abs(Attributes[i].High - Attributes[i - 1].Close));
                        att.TR = Math.Max(Math.Abs(Attributes[i].Low - Attributes[i - 1].Close), findMax);

                        att.PostiveDM = (att.High - Attributes[i - 1].High) < 0 ? 0 : att.High - Attributes[i - 1].High;
                        att.NegativeDM = (Attributes[i - 1].Low - att.Low) < 0 ? 0 : Attributes[i - 1].Low - att.Low;

                        att.PostiveDM = (att.High - Attributes[i - 1].High) > (Attributes[i - 1].Low - att.Low) ? Math.Max(att.High - Attributes[i - 1].High, 0) : 0;
                        att.NegativeDM = (Attributes[i - 1].Low - att.Low) > (att.High - Attributes[i - 1].High) ? Math.Max(Attributes[i - 1].Low - att.Low, 0) : 0;
                    }
                    if (i == Constants.ADXn)
                    {
                        att.TRn = Attributes.OrderBy(x => x.TimeStamp).Skip(i - (Constants.ADXn - 1)).Take(Constants.ADXn).Sum(x => x.TR);
                        att.PostiveDMn = Attributes.OrderBy(x => x.TimeStamp).Skip(i - (Constants.ADXn - 1)).Take(Constants.ADXn).Sum(x => x.PostiveDM);
                        att.NegativeDMn = Attributes.OrderBy(x => x.TimeStamp).Skip(i - (Constants.ADXn - 1)).Take(Constants.ADXn).Sum(x => x.NegativeDM);
                        att.PostiveDIn = (100 * (att.PostiveDMn / att.TRn));
                        att.NegativeDIn = (100 * (att.NegativeDMn / att.TRn));
                    }
                    if (i > Constants.ADXn)
                    {
                        att.TRn = Round(Attributes[i - 1].TRn - (Attributes[i - 1].TRn / Constants.ADXn) + att.TR);
                        att.PostiveDMn = Round(Attributes[i - 1].PostiveDMn - (Attributes[i - 1].PostiveDMn / Constants.ADXn) + att.PostiveDM);
                        att.NegativeDMn = Round(Attributes[i - 1].NegativeDMn - (Attributes[i - 1].NegativeDMn / Constants.ADXn) + att.NegativeDM);
                        att.PostiveDIn = Round((100 * (att.PostiveDMn / att.TRn)));
                        att.NegativeDIn = Round((100 * (att.NegativeDMn / att.TRn)));
                        att.Dx = (100 * (Math.Abs(att.PostiveDIn - att.NegativeDIn) / (att.PostiveDIn + att.NegativeDIn)));
                        att.Diff = Math.Abs(att.PostiveDIn - att.NegativeDIn);

                        //Test
                        if (att.Diff == 15.29M)
                        {

                        }
                        if (att.Close == 2412.20M)
                        {

                        }
                        //if (Attributes[i - 1].Diff != 0 &&
                        //    (Math.Abs(Decimal.Divide(Attributes[i - 1].PostiveDIn - Attributes[i - 1].NegativeDIn, Attributes[i - 1].Diff)) * 100 < 10))
                        //{
                        //    att.IsAdxUnStable = true;
                        //}
                        //if (Attributes[i - 1].IsAdxUnStable) att.IsAdxUnStable = true;

                        if (att.Diff < Attributes[i - 1].Diff)
                        {
                            att.Diff = Attributes[i - 1].Diff;
                        }

                        //if (att.IsAdxUnStable)
                        //{
                        //    if (Attributes[i - 1].TestValue == 0)
                        //        att.TestValue = att.Close;
                        //    else
                        //        att.TestValue = Attributes[i - 1].TestValue;
                        //}

                        if (att.PostiveDIn > att.NegativeDIn)
                        {
                            att.AdxMode = IndicatorMode.Buy;
                        }
                        else
                        {
                            att.AdxMode = IndicatorMode.Sell;
                        }

                        if (att.AdxMode != Attributes[i - 1].AdxMode)
                        {
                            if (Attributes[i - 1].Diff != 0) att.Diff = 0;//Reset
                        }

                        //End Test
                        //if (att.PostiveDIn > att.NegativeDIn)
                        //{
                        //    att.AdxMode = IndicatorMode.Buy;
                        //}
                        //else
                        //{
                        //    att.AdxMode = IndicatorMode.Sell;
                        //}
                    }
                    if (i == Constants.ADXn + Constants.ADXn)
                    {
                        att.Adx = Attributes.OrderBy(x => x.TimeStamp).Skip(i - (Constants.ADXn)).Take(Constants.ADXn).Average(x => x.Dx);
                    }
                    if (i > Constants.ADXn + Constants.ADXn)
                    {
                        att.Adx = Round((Attributes[i - 1].Adx * (Constants.ADXn - 1) + att.Dx) / Constants.ADXn);

                        //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)
                        if (i == Constants.ADXn + Constants.ADXn + Constants.adxEMA)
                        {
                            decimal adxSMA = Decimal.Divide(Attributes.OrderBy(x => x.TimeStamp).Skip(Constants.ADXn + Constants.ADXn).Take(Constants.adxEMA).Sum(x => x.Adx), Constants.adxEMA);
                            att.AdxEMA = adxSMA;
                        }
                        else if (i > Constants.ADXn + Constants.ADXn + Constants.adxEMA)
                        {
                            decimal lm = Decimal.Divide(2, (Constants.adxEMA + 1));
                            att.AdxEMA = (att.Adx - Attributes[i - 1].AdxEMA) * lm + Attributes[i - 1].AdxEMA;
                        }
                    }
                }

                if (positionSettings.IsTestRun)
                {
                    CalculatePL(new Position()
                    {
                        PositionAttributes = Attributes,
                        PositionSettings = positionSettings
                    });
                }

                UpdateDB(positionSettings);

            }
            return new Position()
            {
                PositionAttributes = Attributes,
                PositionSettings = positionSettings
            };

        }
        private static void CalculatePL(Position position)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();

            List<Attributes> Attributes = position.PositionAttributes;
            var pls = Attributes.Count > 28 ? Attributes[29].Close : 0;
            decimal currentPL = 00;
            int totalTrades = 0;
            DateTime TradeStartDateTime = DateTime.Today, TradeEndDateTime = DateTime.Today;
            decimal totalPL = 00;
            foreach (Attributes att in Attributes)
            {
                int i = Attributes.IndexOf(att);
                int count = 1;
                if (i > 28 && Attributes[i].STMode != Attributes[i - 1].STMode)
                {
                    var ple = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    TradeEndDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;
                    if (Attributes[i - 1].STMode == IndicatorMode.Buy)
                    {
                        Attributes[i].PL = Decimal.Subtract(ple, pls);
                    }
                    else
                    {
                        Attributes[i].PL = Decimal.Subtract(pls, ple);
                    }
                    totalPL += Attributes[i].PL;
                    totalTrades += count;

                    position.PositionSettings.Indicator = "ST";
                    if (position.PositionSettings.IsDepth)
                        Program.LogBackTest(position, pls, ple, Attributes[i], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[i - 1].STMode);
                    pls = (Attributes.Count - 1 == i) ? Attributes[i].Open : Attributes[i + 1].Open;
                    TradeStartDateTime = (Attributes.Count - 1 == i) ? Attributes[i].TimeStamp : Attributes[i + 1].TimeStamp;
                }
                if (Attributes[Attributes.Count - 1].STMode == IndicatorMode.Buy)
                    currentPL = Decimal.Subtract(Attributes[Attributes.Count - 1].Close, pls);
                else if (Attributes[Attributes.Count - 1].STMode == IndicatorMode.Sell)
                    currentPL = Decimal.Subtract(pls, Attributes[Attributes.Count - 1].Close);
            }

            if (!position.PositionSettings.IsDepth)
                Program.LogBackTest(position, 0, 0, Attributes[0], conn, totalTrades, totalPL, TradeStartDateTime, TradeEndDateTime, Attributes[0].STMode);

            conn.Close();

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
        public Settings StartLooking(Settings positionSettings, Kite kite)
        {
            throw new System.NotImplementedException();
        }
    }
}