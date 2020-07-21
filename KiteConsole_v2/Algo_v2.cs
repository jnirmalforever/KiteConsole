using KiteConnect;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{

    public class Algov2
    {
        private Position PullIndicatorData(Settings settings, Kite kite)
        {
            IterateOnIndicator indicator = new IterateOnIndicator();
            return indicator.IndicatorCheck(settings, kite);
        }

        public void Main(Kite kite, Settings settings)
        {
            Position position60Min = new Position();
            Position positionDay = new Position();
            Position position30Min = new Position();

            //settings.ParentInstrument = "81153";

            settings.ParentInstrument = "1214721"; //BI
            ////settings.ParentInstrument = "738561";//Reliance

            //settings.ParentInstrument = "341249";//HDFCBANK
            //settings.ParentInstrument = "256265";

            position60Min.PositionSettings = Misc.DeepClone(settings);
            positionDay.PositionSettings = Misc.DeepClone(settings);
            position30Min.PositionSettings = Misc.DeepClone(settings);



            //positionDay.PositionSettings.Interval = "day";
            //positionDay.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-540);
            //positionDay.PositionSettings.ToDate = Program.GetIndianDateTime();

            //positionDay.PositionSettings.Interval = "day";
            //positionDay.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-1095);
            //positionDay.PositionSettings.ToDate = Program.GetIndianDateTime().AddDays(0);

            //positionDay.PositionSettings.Interval = "day";
            //positionDay.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-1460);
            //positionDay.PositionSettings.ToDate = Program.GetIndianDateTime().AddDays(0);

            //positionDay.PositionSettings.Interval = "30minute";
            //positionDay.PositionSettings.Interval = "hour";
            positionDay.PositionSettings.Interval = "day";
            positionDay.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-2555);
            positionDay.PositionSettings.ToDate = Program.GetIndianDateTime().AddDays(0);

            position60Min.PositionSettings.Interval = "2hour";
            position60Min.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-2555);
            position60Min.PositionSettings.ToDate = Program.GetIndianDateTime().AddDays(0);


            //positionDay.PositionSettings.Interval = "2hour";
            //positionDay.PositionSettings.FromDate = Program.GetIndianDateTime().AddDays(-1095);
            //positionDay.PositionSettings.ToDate = Program.GetIndianDateTime().AddDays(0);

            IterateOnIndicator indicator = new IterateOnIndicator();
            //position30Min = indicator.IndicatorCheck(position30Min.PositionSettings, kite);
            position60Min = indicator.IndicatorCheck(position60Min.PositionSettings, kite);
            positionDay = indicator.IndicatorCheck(positionDay.PositionSettings, kite);


            foreach (Attributes att2Hr in position60Min.PositionAttributes)
            {
                int i = position60Min.PositionAttributes.IndexOf(att2Hr);

                foreach (Attributes att30 in positionDay.PositionAttributes)
                {
                    if (att30.TimeStamp >= att2Hr.TimeStamp && (i < position60Min.PositionAttributes.Count - 1) && att30.TimeStamp < position60Min.PositionAttributes[i + 1].TimeStamp)
                    {
                        att30.AdxParent = att2Hr.Adx;
                        att30.AdxEMAParent = att2Hr.AdxEMA;
                    }
                    else if (position60Min.PositionAttributes.Count - 1 == i && att30.TimeStamp >= att2Hr.TimeStamp)
                    {
                        att30.AdxParent = att2Hr.Adx;
                        att30.AdxEMAParent = att2Hr.AdxEMA;
                    }
                }
            }

            //AnalyzeTrade1(positionDay, position60Min);
            //AnalyzeTradeEMA(positionDay);
            //AnalyzeTradeAdx(positionDay);
            AnalyzeTradeSTAll(positionDay);
            //AnalyzeTradeSTAllv2(positionDay);

        }
        private void AnalyzeTradeSTAllv2(Position positionDay)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;


            bool adxActivated = false, adxNegative = false;
            decimal startPrice1 = 0, endPrice1 = 0, totalPL1 = 0;
            DateTime startDateTime;

            bool stoplosshit = false;
            if (positionDay.PositionAttributes.Count > 0)
            {
                decimal longSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                decimal shortSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<EMAAttributes> emaAttributes = positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA)
                                            .Select(x => new EMAAttributes
                                            {
                                                Close = x.Close,
                                                TimeStamp = x.TimeStamp
                                            }).OrderBy(x => x.TimeStamp).ToList();

                decimal lm = Decimal.Divide(2, (Constants.longEMA + 1));
                decimal sm = Decimal.Divide(2, (Constants.shortEMA + 1));

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)


                foreach (Attributes att in positionDay.PositionAttributes)
                {
                    int i = positionDay.PositionAttributes.IndexOf(att);
                    if (i < 50)
                        continue;

                    if (i == 0)
                    {
                        att.LongEMA = (att.Close - longSMA) * lm + longSMA;
                        att.ShortEMA = (att.Close - shortSMA) * sm + shortSMA;
                    }
                    else
                    {
                        att.LongEMA = (att.Close - positionDay.PositionAttributes[i - 1].LongEMA) * lm + positionDay.PositionAttributes[i - 1].LongEMA;
                        att.ShortEMA = (att.Close - positionDay.PositionAttributes[i - 1].ShortEMA) * sm + positionDay.PositionAttributes[i - 1].ShortEMA;
                    }
                    if (att.ShortEMA > att.LongEMA)
                    {
                        att.EMAMode = IndicatorMode.Buy;
                    }
                    else
                    {
                        att.EMAMode = IndicatorMode.Sell;
                    }
                }

                EMAAttributes pcClose = emaAttributes[emaAttributes.Count - 2];
                EMAAttributes ccClose = emaAttributes[emaAttributes.Count - 1];
            }

            bool IsTradeStart = true;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);

                if (!stoplosshit && attributes.STMode == IndicatorMode.Buy)
                {
                    if (attributes.Adx > attributes.AdxEMA && attributes.PostiveDIn > attributes.NegativeDIn
                        && attributes.AdxParent > attributes.AdxEMAParent)// && attributes.MACDMode == IndicatorMode.Buy)// && attributes.Adx > 25)
                    {
                        //if (attributes.STMode3 == IndicatorMode.Sell && attributes.Adx < attributes.PostiveDIn && attributes.Adx < attributes.NegativeDIn)
                        //{
                        //    positionDay.PositionAttributes[i].Mode = positionDay.PositionAttributes[i - 1].Mode;
                        //    continue;
                        //}

                        IsTradeStart = true;
                        attributes.Mode = IndicatorMode.Buy;
                    }
                    else if (IsTradeStart && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        attributes.Mode = IndicatorMode.Buy;
                    }
                    else
                        attributes.Mode = IndicatorMode.SquareOff;
                }
                else if (attributes.STMode == IndicatorMode.Sell && stoplosshit)
                {
                    stoplosshit = false;
                    attributes.Mode = IndicatorMode.SquareOff;
                    IsTradeStart = false;
                }
                else if ((attributes.STMode == IndicatorMode.Sell && attributes.Adx > attributes.AdxEMA
                    && attributes.PostiveDIn < attributes.NegativeDIn && attributes.AdxParent > attributes.AdxEMAParent)// && attributes.Adx > 13)
                    )//|| (attributes.STMode == IndicatorMode.Sell && attributes.STMode3 == IndicatorMode.Sell))
                {
                    if (attributes.Adx < positionDay.PositionAttributes[i - 1].Adx && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        attributes.Mode = IndicatorMode.SquareOff;
                        continue;
                    }

                    attributes.Mode = IndicatorMode.Sell;
                    IsTradeStart = true;
                }
                else if (i > 0 && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Sell && attributes.STMode == IndicatorMode.Buy)
                {
                    attributes.Mode = IndicatorMode.SquareOff;
                    IsTradeStart = false;
                }
                else if (i > 0)
                {
                    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                }
                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                }


                if (attributes.Adx < 20 && i > 1)
                {
                    var s = positionDay.PositionAttributes[i - 1].Mode;

                    //attributes.Mode = IndicatorMode.SquareOff;
                    //stoplosshit = true;x  x   
                }
                else
                {

                }
                //if (attributes.Close < startPrice1)
                //{
                //    attributes.Mode = IndicatorMode.SquareOff;
                //}



                endPrice1 = attributes.Close;
                if (endPrice1 - startPrice1 < -50 && startPrice1 != 0 && attributes.Mode == IndicatorMode.Buy)
                {
                    //attributes.Mode = IndicatorMode.SquareOff;
                    //stoplosshit = true;
                }
                //else if (attributes.EMAMode == IndicatorMode.Sell)
                //{
                //    //if (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy && positionDay.PositionAttributes[i - 1].STMode == IndicatorMode.Buy)
                //    //{
                //    //    attributes.Mode = IndicatorMode.Buy;
                //    //    continue;
                //    //}
                //    attributes.Mode = IndicatorMode.SquareOff;
                //    adxActivated = false; stoplosshit = false;
                //}
                //else
                //{
                //    attributes.Mode = IndicatorMode.SquareOff;
                //    adxActivated = false;
                //}

                //if (attributes.Adx < attributes.PostiveDIn && attributes.Adx < attributes.NegativeDIn)
                //{
                //    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                //}

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        if (attributes.Close < attributes.Open)
                        {
                            attributes.Mode = IndicatorMode.SquareOff;
                        }
                        else
                        {

                        }
                    }
                }

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice1 = attributes.Close;
                        if (startPrice1 > 0)
                        {
                            attributes.PL = endPrice1 - startPrice1;
                            totalPL1 += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        startPrice1 = attributes.Close;
                        startDateTime = attributes.TimeStamp;
                    }
                }
            }

            decimal startPrice = 0, endPrice = 0, totalPL = 0;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if ((attributes.Mode == IndicatorMode.SquareOff || attributes.Mode == IndicatorMode.Sell)
                        && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        endPrice = attributes.Close;
                        if (startPrice > 0)
                        {
                            attributes.PL = endPrice - startPrice;
                            totalPL += attributes.PL;
                        }
                    }



                    if ((attributes.Mode == IndicatorMode.Buy || attributes.Mode == IndicatorMode.SquareOff)
                       && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Sell)
                    {
                        endPrice = attributes.Close;
                        if (startPrice > 0)
                        {
                            attributes.PL = startPrice - endPrice;
                            totalPL += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy && (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.SquareOff ||
                    positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Sell))
                        startPrice = attributes.Close;

                    if (attributes.Mode == IndicatorMode.Sell && (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.SquareOff ||
                        positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy))
                        startPrice = attributes.Close;

                    Program.LogBackTest_Algo(positionDay, attributes.Close, attributes.Close, attributes, conn, trades, totalPL, attributes.TimeStamp, attributes.TimeStamp, attributes.Mode);
                    trades++;
                }
            }
        }


        private void AnalyzeTradeSTOnly(Position positionDay)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;


            bool adxActivated = false, adxNegative = false;
            decimal startPrice1 = 0, endPrice1 = 0, totalPL1 = 0;
            DateTime startDateTime;

            bool stoplosshit = false;
            if (positionDay.PositionAttributes.Count > 0)
            {
                decimal longSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                decimal shortSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<EMAAttributes> emaAttributes = positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA)
                                            .Select(x => new EMAAttributes
                                            {
                                                Close = x.Close,
                                                TimeStamp = x.TimeStamp
                                            }).OrderBy(x => x.TimeStamp).ToList();

                decimal lm = Decimal.Divide(2, (Constants.longEMA + 1));
                decimal sm = Decimal.Divide(2, (Constants.shortEMA + 1));

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)


                foreach (Attributes att in positionDay.PositionAttributes)
                {
                    int i = positionDay.PositionAttributes.IndexOf(att);
                    if (i < 50)
                        continue;

                    if (i == 0)
                    {
                        att.LongEMA = (att.Close - longSMA) * lm + longSMA;
                        att.ShortEMA = (att.Close - shortSMA) * sm + shortSMA;
                    }
                    else
                    {
                        att.LongEMA = (att.Close - positionDay.PositionAttributes[i - 1].LongEMA) * lm + positionDay.PositionAttributes[i - 1].LongEMA;
                        att.ShortEMA = (att.Close - positionDay.PositionAttributes[i - 1].ShortEMA) * sm + positionDay.PositionAttributes[i - 1].ShortEMA;
                    }
                    if (att.ShortEMA > att.LongEMA)
                    {
                        att.EMAMode = IndicatorMode.Buy;
                    }
                    else
                    {
                        att.EMAMode = IndicatorMode.Sell;
                    }
                }

                EMAAttributes pcClose = emaAttributes[emaAttributes.Count - 2];
                EMAAttributes ccClose = emaAttributes[emaAttributes.Count - 1];
            }

            bool IsTradeStart = true;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);

                if (!stoplosshit && attributes.STMode == IndicatorMode.Buy)
                {
                    if (attributes.STMode3 == IndicatorMode.Buy)// && attributes.MACDMode == IndicatorMode.Buy)// && attributes.Adx > 25)
                    {
                        //if (attributes.STMode3 == IndicatorMode.Sell && attributes.Adx < attributes.PostiveDIn && attributes.Adx < attributes.NegativeDIn)
                        //{
                        //    positionDay.PositionAttributes[i].Mode = positionDay.PositionAttributes[i - 1].Mode;
                        //    continue;
                        //}

                        IsTradeStart = true;
                        attributes.Mode = IndicatorMode.Buy;
                    }
                    else if (IsTradeStart && positionDay.PositionAttributes[i].STMode3 == IndicatorMode.Sell)
                    {
                        attributes.Mode = IndicatorMode.SquareOff;
                    }
                    else
                        attributes.Mode = IndicatorMode.SquareOff;
                }
                else if (attributes.STMode == IndicatorMode.Sell && stoplosshit)
                {
                    stoplosshit = false;
                    attributes.Mode = IndicatorMode.SquareOff;
                    IsTradeStart = false;
                }
                else if ((attributes.STMode == IndicatorMode.Sell && positionDay.PositionAttributes[i].STMode3 == IndicatorMode.Sell)
                    )//|| (attributes.STMode == IndicatorMode.Sell && attributes.STMode3 == IndicatorMode.Sell))
                {
                    //if (attributes.Adx < positionDay.PositionAttributes[i - 1].Adx && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy)
                    //{
                    //    attributes.Mode = IndicatorMode.SquareOff;
                    //    continue;
                    //}

                    attributes.Mode = IndicatorMode.Sell;
                    IsTradeStart = true;
                }
                else if (attributes.STMode == IndicatorMode.Sell && positionDay.PositionAttributes[i].STMode3 == IndicatorMode.Sell)
                    attributes.Mode = IndicatorMode.SquareOff;

                else if (i > 0)
                {
                    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                }
                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                }


                if (attributes.Adx < 20 && i > 1)
                {
                    var s = positionDay.PositionAttributes[i - 1].Mode;

                    //attributes.Mode = IndicatorMode.SquareOff;
                    //stoplosshit = true;x  x   
                }
                else
                {

                }
                //if (attributes.Close < startPrice1)
                //{
                //    attributes.Mode = IndicatorMode.SquareOff;
                //}



                endPrice1 = attributes.Close;
                if (endPrice1 - startPrice1 < -50 && startPrice1 != 0 && attributes.Mode == IndicatorMode.Buy)
                {
                    //attributes.Mode = IndicatorMode.SquareOff;
                    //stoplosshit = true;
                }
                //else if (attributes.EMAMode == IndicatorMode.Sell)
                //{
                //    //if (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy && positionDay.PositionAttributes[i - 1].STMode == IndicatorMode.Buy)
                //    //{
                //    //    attributes.Mode = IndicatorMode.Buy;
                //    //    continue;
                //    //}
                //    attributes.Mode = IndicatorMode.SquareOff;
                //    adxActivated = false; stoplosshit = false;
                //}
                //else
                //{
                //    attributes.Mode = IndicatorMode.SquareOff;
                //    adxActivated = false;
                //}

                //if (attributes.Adx < attributes.PostiveDIn && attributes.Adx < attributes.NegativeDIn)
                //{
                //    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                //}

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        if (attributes.Close < attributes.Open)
                        {
                            attributes.Mode = IndicatorMode.SquareOff;
                        }
                        else
                        {

                        }
                    }
                }

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice1 = attributes.Close;
                        if (startPrice1 > 0)
                        {
                            attributes.PL = endPrice1 - startPrice1;
                            totalPL1 += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        startPrice1 = attributes.Close;
                        startDateTime = attributes.TimeStamp;
                    }
                }
            }

            decimal startPrice = 0, endPrice = 0, totalPL = 0;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if ((attributes.Mode == IndicatorMode.SquareOff || attributes.Mode == IndicatorMode.Sell)
                        && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        endPrice = attributes.Close;
                        if (startPrice > 0)
                        {
                            attributes.PL = endPrice - startPrice;
                            totalPL += attributes.PL;
                        }
                    }



                    if ((attributes.Mode == IndicatorMode.Buy || attributes.Mode == IndicatorMode.SquareOff)
                       && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Sell)
                    {
                        endPrice = attributes.Close;
                        if (startPrice > 0)
                        {
                            attributes.PL = startPrice - endPrice;
                            totalPL += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy && (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.SquareOff ||
                    positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Sell))
                        startPrice = attributes.Close;

                    if (attributes.Mode == IndicatorMode.Sell && (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.SquareOff ||
                        positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy))
                        startPrice = attributes.Close;

                    Program.LogBackTest_Algo(positionDay, attributes.Close, attributes.Close, attributes, conn, trades, totalPL, attributes.TimeStamp, attributes.TimeStamp, attributes.Mode);
                    trades++;
                }
            }
        }

        private void AnalyzeTradeSTAll(Position positionDay)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;


            bool adxActivated = false, adxNegative = false;
            decimal startPrice1 = 0, endPrice1 = 0, totalPL1 = 0;
            DateTime startDateTime;

            bool stoplosshit = false;
            if (positionDay.PositionAttributes.Count > 0)
            {
                decimal longSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                decimal shortSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<EMAAttributes> emaAttributes = positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA)
                                            .Select(x => new EMAAttributes
                                            {
                                                Close = x.Close,
                                                TimeStamp = x.TimeStamp
                                            }).OrderBy(x => x.TimeStamp).ToList();

                decimal lm = Decimal.Divide(2, (Constants.longEMA + 1));
                decimal sm = Decimal.Divide(2, (Constants.shortEMA + 1));

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)


                foreach (Attributes att in positionDay.PositionAttributes)
                {
                    int i = positionDay.PositionAttributes.IndexOf(att);
                    if (i < 50)
                        continue;

                    if (i == 0)
                    {
                        att.LongEMA = (att.Close - longSMA) * lm + longSMA;
                        att.ShortEMA = (att.Close - shortSMA) * sm + shortSMA;
                    }
                    else
                    {
                        att.LongEMA = (att.Close - positionDay.PositionAttributes[i - 1].LongEMA) * lm + positionDay.PositionAttributes[i - 1].LongEMA;
                        att.ShortEMA = (att.Close - positionDay.PositionAttributes[i - 1].ShortEMA) * sm + positionDay.PositionAttributes[i - 1].ShortEMA;
                    }
                    if (att.ShortEMA > att.LongEMA)
                    {
                        att.EMAMode = IndicatorMode.Buy;
                    }
                    else
                    {
                        att.EMAMode = IndicatorMode.Sell;
                    }
                }

                EMAAttributes pcClose = emaAttributes[emaAttributes.Count - 2];
                EMAAttributes ccClose = emaAttributes[emaAttributes.Count - 1];
            }

            bool IsTradeStart = true;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);

                if (!stoplosshit && attributes.STMode == IndicatorMode.Buy)
                {
                    if (attributes.Adx > attributes.AdxEMA && attributes.PostiveDIn > attributes.NegativeDIn)
                        // && attributes.AdxParent > attributes.AdxEMAParent)
                        // && attributes.MACDMode == IndicatorMode.Buy)// && attributes.Adx > 25)
                    {
                        //if (attributes.STMode3 == IndicatorMode.Sell && attributes.Adx < attributes.PostiveDIn && attributes.Adx < attributes.NegativeDIn)
                        //{
                        //    positionDay.PositionAttributes[i].Mode = positionDay.PositionAttributes[i - 1].Mode;
                        //    continue;
                        //}

                        IsTradeStart = true;
                        attributes.Mode = IndicatorMode.Buy;
                    }
                    else if (IsTradeStart && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        attributes.Mode = IndicatorMode.Buy;
                    }
                    else
                        attributes.Mode = IndicatorMode.SquareOff;
                }
                else if (attributes.STMode == IndicatorMode.Sell && stoplosshit)
                {
                    stoplosshit = false;
                    attributes.Mode = IndicatorMode.SquareOff;
                    IsTradeStart = false;
                }
                else if ((attributes.STMode == IndicatorMode.Sell && attributes.Adx > attributes.AdxEMA
                    && attributes.PostiveDIn < attributes.NegativeDIn) 
                    //&& attributes.AdxParent > attributes.AdxEMAParent)// && attributes.Adx > 13)
                    )//|| (attributes.STMode == IndicatorMode.Sell && attributes.STMode3 == IndicatorMode.Sell))
                {
                    if (attributes.Adx < positionDay.PositionAttributes[i - 1].Adx && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        attributes.Mode = IndicatorMode.SquareOff;
                        continue;
                    }

                    attributes.Mode = IndicatorMode.Sell;
                    IsTradeStart = true;
                }
                else if (i > 0 && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Sell && attributes.STMode == IndicatorMode.Buy)
                {
                    attributes.Mode = IndicatorMode.SquareOff;
                    IsTradeStart = false;
                }
                else if (i > 0)
                {
                    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                }
                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                }


                if (attributes.Adx < 20 && i > 1)
                {
                    var s = positionDay.PositionAttributes[i - 1].Mode;

                    //attributes.Mode = IndicatorMode.SquareOff;
                    //stoplosshit = true;x  x   
                }
                else
                {

                }
                //if (attributes.Close < startPrice1)
                //{
                //    attributes.Mode = IndicatorMode.SquareOff;
                //}



                endPrice1 = attributes.Close;
                if (endPrice1 - startPrice1 < -50 && startPrice1 != 0 && attributes.Mode == IndicatorMode.Buy)
                {
                    //attributes.Mode = IndicatorMode.SquareOff;
                    //stoplosshit = true;
                }
                //else if (attributes.EMAMode == IndicatorMode.Sell)
                //{
                //    //if (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy && positionDay.PositionAttributes[i - 1].STMode == IndicatorMode.Buy)
                //    //{
                //    //    attributes.Mode = IndicatorMode.Buy;
                //    //    continue;
                //    //}
                //    attributes.Mode = IndicatorMode.SquareOff;
                //    adxActivated = false; stoplosshit = false;
                //}
                //else
                //{
                //    attributes.Mode = IndicatorMode.SquareOff;
                //    adxActivated = false;
                //}

                //if (attributes.Adx < attributes.PostiveDIn && attributes.Adx < attributes.NegativeDIn)
                //{
                //    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                //}

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        if (attributes.Close < attributes.Open)
                        {
                            attributes.Mode = IndicatorMode.SquareOff;
                        }
                        else
                        {

                        }
                    }
                }

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice1 = attributes.Close;
                        if (startPrice1 > 0)
                        {
                            attributes.PL = endPrice1 - startPrice1;
                            totalPL1 += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        startPrice1 = attributes.Close;
                        startDateTime = attributes.TimeStamp;
                    }
                }
            }

            decimal startPrice = 0, endPrice = 0, totalPL = 0;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if ((attributes.Mode == IndicatorMode.SquareOff || attributes.Mode == IndicatorMode.Sell)
                        && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy)
                    {
                        endPrice = attributes.Close;
                        if (startPrice > 0)
                        {
                            attributes.PL = endPrice - startPrice;
                            totalPL += attributes.PL;
                        }
                    }



                    if ((attributes.Mode == IndicatorMode.Buy || attributes.Mode == IndicatorMode.SquareOff)
                       && positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Sell)
                    {
                        endPrice = attributes.Close;
                        if (startPrice > 0)
                        {
                            attributes.PL = startPrice - endPrice;
                            totalPL += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy && (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.SquareOff ||
                    positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Sell))
                        startPrice = attributes.Close;

                    if (attributes.Mode == IndicatorMode.Sell && (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.SquareOff ||
                        positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy))
                        startPrice = attributes.Close;

                    Program.LogBackTest_Algo(positionDay, attributes.Close, attributes.Close, attributes, conn, trades, totalPL, attributes.TimeStamp, attributes.TimeStamp, attributes.Mode);
                    trades++;
                }
            }
        }

        private void AnalyzeTradeST(Position positionDay)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;


            bool adxActivated = false, adxNegative = false;
            decimal startPrice1 = 0, endPrice1 = 0, totalPL1 = 0;
            DateTime startDateTime;

            bool stoplosshit = false;
            if (positionDay.PositionAttributes.Count > 0)
            {
                decimal longSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                decimal shortSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<EMAAttributes> emaAttributes = positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA)
                                            .Select(x => new EMAAttributes
                                            {
                                                Close = x.Close,
                                                TimeStamp = x.TimeStamp
                                            }).OrderBy(x => x.TimeStamp).ToList();

                decimal lm = Decimal.Divide(2, (Constants.longEMA + 1));
                decimal sm = Decimal.Divide(2, (Constants.shortEMA + 1));

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)


                foreach (Attributes att in positionDay.PositionAttributes)
                {
                    int i = positionDay.PositionAttributes.IndexOf(att);
                    if (i < 50)
                        continue;

                    if (i == 0)
                    {
                        att.LongEMA = (att.Close - longSMA) * lm + longSMA;
                        att.ShortEMA = (att.Close - shortSMA) * sm + shortSMA;
                    }
                    else
                    {
                        att.LongEMA = (att.Close - positionDay.PositionAttributes[i - 1].LongEMA) * lm + positionDay.PositionAttributes[i - 1].LongEMA;
                        att.ShortEMA = (att.Close - positionDay.PositionAttributes[i - 1].ShortEMA) * sm + positionDay.PositionAttributes[i - 1].ShortEMA;
                    }
                    if (att.ShortEMA > att.LongEMA)
                    {
                        att.EMAMode = IndicatorMode.Buy;
                    }
                    else
                    {
                        att.EMAMode = IndicatorMode.Sell;
                    }
                }

                EMAAttributes pcClose = emaAttributes[emaAttributes.Count - 2];
                EMAAttributes ccClose = emaAttributes[emaAttributes.Count - 1];
            }

            bool IsTradeStart = true;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);

                if (!stoplosshit && attributes.STMode == IndicatorMode.Buy)
                {
                    if (attributes.Adx > attributes.AdxEMA && attributes.AdxParent > attributes.AdxEMAParent //&& attributes.ShortEMA > attributes.LongEMA 
                        && attributes.PostiveDIn > attributes.NegativeDIn)//&& attributes.STMode3 == IndicatorMode.Buy)// && attributes.MACDMode == IndicatorMode.Buy)// && attributes.Adx > 25)
                    {
                        //if (attributes.MACDHistogram < 5)
                        //{
                        //    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                        //    continue;
                        //}
                        IsTradeStart = true;
                        attributes.Mode = IndicatorMode.Buy;
                    }
                    else if (IsTradeStart)
                    {
                        attributes.Mode = IndicatorMode.Buy;
                    }
                    else
                        attributes.Mode = IndicatorMode.SquareOff;
                }
                else if (attributes.STMode == IndicatorMode.Sell && stoplosshit)
                {
                    stoplosshit = false;
                    attributes.Mode = IndicatorMode.SquareOff;
                    IsTradeStart = false;
                }
                else if ((attributes.STMode == IndicatorMode.Sell && attributes.Adx > attributes.AdxEMA
                    && attributes.PostiveDIn < attributes.NegativeDIn) || (attributes.STMode == IndicatorMode.Sell && attributes.STMode3 == IndicatorMode.Sell)
                    )
                {
                    attributes.Mode = IndicatorMode.SquareOff;
                    IsTradeStart = false;
                }
                else if (i > 0)
                {
                    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                }
                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                }


                if (attributes.Adx < 20 && i > 1)
                {
                    var s = positionDay.PositionAttributes[i - 1].Mode;

                    //attributes.Mode = IndicatorMode.SquareOff;
                    //stoplosshit = true;x  x   
                }
                else
                {

                }
                //if (attributes.Close < startPrice1)
                //{
                //    attributes.Mode = IndicatorMode.SquareOff;
                //}



                endPrice1 = attributes.Close;
                if (endPrice1 - startPrice1 < -50 && startPrice1 != 0 && attributes.Mode == IndicatorMode.Buy)
                {
                    //attributes.Mode = IndicatorMode.SquareOff;
                    //stoplosshit = true;
                }
                //else if (attributes.EMAMode == IndicatorMode.Sell)
                //{
                //    //if (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy && positionDay.PositionAttributes[i - 1].STMode == IndicatorMode.Buy)
                //    //{
                //    //    attributes.Mode = IndicatorMode.Buy;
                //    //    continue;
                //    //}
                //    attributes.Mode = IndicatorMode.SquareOff;
                //    adxActivated = false; stoplosshit = false;
                //}
                //else
                //{
                //    attributes.Mode = IndicatorMode.SquareOff;
                //    adxActivated = false;
                //}

                //if (attributes.Adx < attributes.PostiveDIn && attributes.Adx < attributes.NegativeDIn)
                //{
                //    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                //}

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        if (attributes.Close < attributes.Open)
                        {
                            attributes.Mode = IndicatorMode.SquareOff;
                        }
                        else
                        {

                        }
                    }
                }

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice1 = attributes.Close;
                        if (startPrice1 > 0)
                        {
                            attributes.PL = endPrice1 - startPrice1;
                            totalPL1 += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        startPrice1 = attributes.Close;
                        startDateTime = attributes.TimeStamp;
                    }
                }
            }

            decimal startPrice = 0, endPrice = 0, totalPL = 0;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice = attributes.Close;
                        if (startPrice > 0)
                        {
                            attributes.PL = endPrice - startPrice;
                            totalPL += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                        startPrice = attributes.Close;

                    Program.LogBackTest_Algo(positionDay, attributes.Close, attributes.Close, attributes, conn, trades, totalPL, attributes.TimeStamp, attributes.TimeStamp, attributes.Mode);
                    trades++;
                }
            }
        }

        private void AnalyzeTradeAdx(Position positionDay)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;


            bool adxActivated = false, adxNegative = false;
            decimal startPrice1 = 0, endPrice1 = 0, totalPL1 = 0;
            DateTime startDateTime;

            bool stoplosshit = false;
            if (positionDay.PositionAttributes.Count > 0)
            {
                decimal longSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                decimal shortSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<EMAAttributes> emaAttributes = positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA)
                                            .Select(x => new EMAAttributes
                                            {
                                                Close = x.Close,
                                                TimeStamp = x.TimeStamp
                                            }).OrderBy(x => x.TimeStamp).ToList();

                decimal lm = Decimal.Divide(2, (Constants.longEMA + 1));
                decimal sm = Decimal.Divide(2, (Constants.shortEMA + 1));

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)


                foreach (Attributes att in positionDay.PositionAttributes)
                {
                    int i = positionDay.PositionAttributes.IndexOf(att);
                    if (i < 50)
                        continue;

                    if (i == 0)
                    {
                        att.LongEMA = (att.Close - longSMA) * lm + longSMA;
                        att.ShortEMA = (att.Close - shortSMA) * sm + shortSMA;
                    }
                    else
                    {
                        att.LongEMA = (att.Close - positionDay.PositionAttributes[i - 1].LongEMA) * lm + positionDay.PositionAttributes[i - 1].LongEMA;
                        att.ShortEMA = (att.Close - positionDay.PositionAttributes[i - 1].ShortEMA) * sm + positionDay.PositionAttributes[i - 1].ShortEMA;
                    }
                    if (att.ShortEMA > att.LongEMA)
                    {
                        att.EMAMode = IndicatorMode.Buy;
                    }
                    else
                    {
                        att.EMAMode = IndicatorMode.Sell;
                    }
                }

                EMAAttributes pcClose = emaAttributes[emaAttributes.Count - 2];
                EMAAttributes ccClose = emaAttributes[emaAttributes.Count - 1];
            }

            bool IsTradeStart = true;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);

                if (!stoplosshit && attributes.PostiveDIn > attributes.NegativeDIn)
                {
                    if (attributes.Adx > attributes.AdxEMA && attributes.STMode == IndicatorMode.Buy
                        && attributes.MACDMode == IndicatorMode.Buy)// && attributes.Adx > 25)
                    {
                        IsTradeStart = true;
                        attributes.Mode = IndicatorMode.Buy;
                    }
                    else if (IsTradeStart)
                    {
                        attributes.Mode = IndicatorMode.Buy;
                    }
                    else
                        attributes.Mode = IndicatorMode.SquareOff;
                }
                else if (attributes.PostiveDIn < attributes.NegativeDIn && stoplosshit)
                {
                    stoplosshit = false;
                    attributes.Mode = IndicatorMode.SquareOff;
                    IsTradeStart = false;
                }
                else
                {
                    attributes.Mode = IndicatorMode.SquareOff;
                    IsTradeStart = false;
                }


                if (attributes.Adx < 20 && i > 1)
                {
                    var s = positionDay.PositionAttributes[i - 1].Mode;

                    //attributes.Mode = IndicatorMode.SquareOff;
                    //stoplosshit = true;x  x   
                }
                else
                {

                }
                //if (attributes.Close < startPrice1)
                //{
                //    attributes.Mode = IndicatorMode.SquareOff;
                //}



                endPrice1 = attributes.Close;
                if (endPrice1 - startPrice1 < -50 && startPrice1 != 0 && attributes.Mode == IndicatorMode.Buy)
                {
                    //attributes.Mode = IndicatorMode.SquareOff;
                    //stoplosshit = true;
                }
                //else if (attributes.EMAMode == IndicatorMode.Sell)
                //{
                //    //if (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy && positionDay.PositionAttributes[i - 1].STMode == IndicatorMode.Buy)
                //    //{
                //    //    attributes.Mode = IndicatorMode.Buy;
                //    //    continue;
                //    //}
                //    attributes.Mode = IndicatorMode.SquareOff;
                //    adxActivated = false; stoplosshit = false;
                //}
                //else
                //{
                //    attributes.Mode = IndicatorMode.SquareOff;
                //    adxActivated = false;
                //}

                //if (attributes.Adx < attributes.PostiveDIn && attributes.Adx < attributes.NegativeDIn)
                //{
                //    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                //}

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        if (attributes.Close < attributes.Open)
                        {
                            attributes.Mode = IndicatorMode.SquareOff;
                        }
                    }
                }

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice1 = attributes.Close;
                        if (startPrice1 > 0)
                        {
                            attributes.PL = endPrice1 - startPrice1;
                            totalPL1 += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        startPrice1 = attributes.Close;
                        startDateTime = attributes.TimeStamp;
                    }
                }
            }

            decimal startPrice = 0, endPrice = 0, totalPL = 0;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);

                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice = attributes.Close;
                        if (startPrice > 0)
                        {
                            attributes.PL = endPrice - startPrice;
                            totalPL += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                        startPrice = attributes.Close;

                    Program.LogBackTest_Algo(positionDay, attributes.Close, attributes.Close, attributes, conn, trades, totalPL, attributes.TimeStamp, attributes.TimeStamp, attributes.Mode);
                    trades++;
                }
            }
        }

        private void AnalyzeTradeEMA(Position positionDay)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;


            bool adxActivated = false, adxNegative = false;
            decimal startPrice1 = 0, endPrice1 = 0, totalPL1 = 0;

            bool stoplosshit = false;
            if (positionDay.PositionAttributes.Count > 0)
            {
                decimal longSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                decimal shortSMA = Decimal.Divide(positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<EMAAttributes> emaAttributes = positionDay.PositionAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA)
                                            .Select(x => new EMAAttributes
                                            {
                                                Close = x.Close,
                                                TimeStamp = x.TimeStamp
                                            }).OrderBy(x => x.TimeStamp).ToList();

                decimal lm = Decimal.Divide(2, (Constants.longEMA + 1));
                decimal sm = Decimal.Divide(2, (Constants.shortEMA + 1));

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)


                foreach (Attributes att in positionDay.PositionAttributes)
                {
                    int i = positionDay.PositionAttributes.IndexOf(att);
                    if (i < 50)
                        continue;

                    if (i == 0)
                    {
                        att.LongEMA = (att.Close - longSMA) * lm + longSMA;
                        att.ShortEMA = (att.Close - shortSMA) * sm + shortSMA;
                    }
                    else
                    {
                        att.LongEMA = (att.Close - positionDay.PositionAttributes[i - 1].LongEMA) * lm + positionDay.PositionAttributes[i - 1].LongEMA;
                        att.ShortEMA = (att.Close - positionDay.PositionAttributes[i - 1].ShortEMA) * sm + positionDay.PositionAttributes[i - 1].ShortEMA;
                    }
                    if (att.Close == 1673.80M)

                    {

                    }

                    if (att.ShortEMA > att.LongEMA)
                    {
                        att.EMAMode = IndicatorMode.Buy;
                    }
                    else
                    {
                        att.EMAMode = IndicatorMode.Sell;
                    }
                }

                EMAAttributes pcClose = emaAttributes[emaAttributes.Count - 2];
                EMAAttributes ccClose = emaAttributes[emaAttributes.Count - 1];
            }

            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);
                if (attributes.EMAMode == IndicatorMode.None)
                    continue;
                if (attributes.Close == 1673.80M)

                {

                }
                if (attributes.EMAMode == IndicatorMode.Buy)
                {

                }
                if (attributes.EMAMode == IndicatorMode.Sell)
                {

                }
                if (!stoplosshit && attributes.EMAMode == IndicatorMode.Buy)
                {
                    //if (!adxActivated && attributes.Adx > attributes.PostiveDIn || attributes.Adx > attributes.NegativeDIn)
                    //{
                    //    adxActivated = true;
                    //}

                    if (attributes.AdxMode == IndicatorMode.Sell)// || attributes.STMode == IndicatorMode.Sell)
                    {
                        attributes.Mode = positionDay.PositionAttributes[i - 1].EMAMode;
                        continue;
                    }

                    if (attributes.AdxEMA > attributes.Adx)
                    {
                        attributes.Mode = positionDay.PositionAttributes[i - 1].EMAMode;

                        continue;
                    }

                    attributes.Mode = IndicatorMode.Buy;


                    //if (attributes.STMode == IndicatorMode.Buy)
                    //{
                    //    attributes.Mode = IndicatorMode.Buy;
                    //}
                    //else
                    //    attributes.Mode = IndicatorMode.SquareOff;


                    if (attributes.Adx < 20)
                    {
                        var s = positionDay.PositionAttributes[i - 1].Mode;

                        //attributes.Mode = IndicatorMode.SquareOff;
                        //stoplosshit = true;x  x   
                    }
                    else
                    {

                    }
                    //if (attributes.Close < startPrice1)
                    //{
                    //    attributes.Mode = IndicatorMode.SquareOff;
                    //}


                    if (i > 1 && attributes.EMAMode != positionDay.PositionAttributes[i - 1].EMAMode)
                    {
                        if (attributes.EMAMode == IndicatorMode.Buy)
                            startPrice1 = attributes.Close;
                    }

                    endPrice1 = attributes.Close;
                    if (endPrice1 - startPrice1 < -50 && startPrice1 != 0)
                    {
                        //attributes.Mode = IndicatorMode.SquareOff;
                        //stoplosshit = true;
                    }
                }
                else if (attributes.EMAMode == IndicatorMode.Sell)
                {
                    //if (positionDay.PositionAttributes[i - 1].Mode == IndicatorMode.Buy && positionDay.PositionAttributes[i - 1].STMode == IndicatorMode.Buy)
                    //{
                    //    attributes.Mode = IndicatorMode.Buy;
                    //    continue;
                    //}
                    attributes.Mode = IndicatorMode.SquareOff;
                    adxActivated = false; stoplosshit = false;
                }
                else
                {
                    attributes.Mode = IndicatorMode.SquareOff;
                    adxActivated = false;
                }

                //if (attributes.Adx < attributes.PostiveDIn && attributes.Adx < attributes.NegativeDIn)
                //{
                //    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                //}

                if (attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.Buy)
                    {
                        if (attributes.Close < attributes.Open)
                        {
                            attributes.Mode = IndicatorMode.SquareOff;
                        }
                    }
                }

                if (attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice1 = attributes.Close;
                        if (startPrice1 > 0)
                        {
                            attributes.PL = endPrice1 - startPrice1;
                            totalPL1 += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                        startPrice1 = attributes.Close;
                }
            }

            decimal startPrice = 0, endPrice = 0, totalPL = 0;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                if (attributes.Close == 1673.80M)

                {

                }
                int i = positionDay.PositionAttributes.IndexOf(attributes);
                if (attributes.Mode == IndicatorMode.Buy)
                {

                }
                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice = attributes.Close;
                        if (startPrice > 0)
                        {
                            attributes.PL = endPrice - startPrice;
                            totalPL += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                        startPrice = attributes.Close;

                    Program.LogBackTest_Algo(positionDay, attributes.Close, attributes.Close, attributes, conn, trades, totalPL, attributes.TimeStamp, attributes.TimeStamp, attributes.Mode);
                    trades++;
                }
            }
        }

        private void AnalyzeTrade(Position positionDay)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;


            bool adxActivated = false, adxNegative = false;
            decimal startPrice1 = 0, endPrice1 = 0, totalPL1 = 0;

            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);
                if (attributes.STMode == IndicatorMode.None)
                    continue;

                if (attributes.STMode == IndicatorMode.Buy)
                {
                    //if (!adxActivated && attributes.Adx > attributes.PostiveDIn || attributes.Adx > attributes.NegativeDIn)
                    //{
                    //    adxActivated = true;
                    //}

                    //if (adxActivated)
                    //    attributes.Mode = IndicatorMode.Buy;
                    //else
                    //    attributes.Mode = IndicatorMode.SquareOff;

                    attributes.Mode = IndicatorMode.Buy;
                    //if (attributes.Close < startPrice1)
                    //{
                    //    attributes.Mode = IndicatorMode.SquareOff;
                    //}
                }
                else
                {
                    attributes.Mode = IndicatorMode.SquareOff;

                    adxActivated = false;
                }

                //if (attributes.Adx < attributes.PostiveDIn && attributes.Adx < attributes.NegativeDIn)
                //{
                //    attributes.Mode = positionDay.PositionAttributes[i - 1].Mode;
                //}

                if (attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice1 = attributes.Close;
                        if (startPrice1 > 0)
                        {
                            attributes.PL = endPrice1 - startPrice1;
                            totalPL1 += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                        startPrice1 = attributes.Close;
                }
            }

            decimal startPrice = 0, endPrice = 0, totalPL = 0;
            foreach (Attributes attributes in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(attributes);
                if (i > 1 && attributes.Mode != positionDay.PositionAttributes[i - 1].Mode)
                {
                    if (attributes.Mode == IndicatorMode.SquareOff)
                    {
                        endPrice = attributes.Close;
                        if (startPrice > 0)
                        {
                            attributes.PL = endPrice - startPrice;
                            totalPL += attributes.PL;
                        }
                    }

                    if (attributes.Mode == IndicatorMode.Buy)
                        startPrice = attributes.Close;


                    Program.LogBackTest_Algo(positionDay, attributes.Close, attributes.Close, attributes, conn, trades, totalPL, attributes.TimeStamp, attributes.TimeStamp, attributes.Mode);
                    trades++;
                }
            }
        }

        private void AnalyzeTrade1(Position positionDay, Position position60Min, Position position30Min)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;
            decimal startPrice = 0, endPrice = 0;
            IndicatorMode startMode, endMode;
            foreach (var dayAtt in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(dayAtt);
                if (i > 14)
                {
                    if (dayAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                    {

                    }
                    IEnumerable<Attributes> positionFiltered = position60Min.PositionAttributes
                        .Where(x => x.TimeStamp >= dayAtt.TimeStamp)
                        .Where(x => x.TimeStamp < dayAtt.TimeStamp.AddDays(1)).ToList();

                    if (positionFiltered.Count() > 0)
                    {

                    }

                    foreach (var subAtt60 in positionFiltered)
                    {
                        if (subAtt60.STMode != IndicatorMode.None)
                        {
                            if (dayAtt.STMode == subAtt60.STMode)
                            {
                                subAtt60.STModeDay60Min = dayAtt.STMode;
                            }
                            else
                            {
                                subAtt60.STModeDay60Min = IndicatorMode.SquareOff;
                            }
                        }
                    }
                }
            }


            foreach (var subAtt60 in position60Min.PositionAttributes)
            {
                if (subAtt60.Close == 2560.05M)
                {

                }
                if (subAtt60.STModeDay60Min != IndicatorMode.None) //&& subAtt60.STModeDay60Min != IndicatorMode.SquareOff)
                {

                    IEnumerable<Attributes> positionFiltered30 = position30Min.PositionAttributes
                        .Where(x => x.TimeStamp >= subAtt60.TimeStamp)
                        .Where(x => x.TimeStamp <= subAtt60.TimeStamp.AddMinutes(30)).ToList();


                    foreach (var subAtt30 in positionFiltered30)
                    {
                        int j = position30Min.PositionAttributes.IndexOf(subAtt30);

                        if (subAtt30.Close == 2516)
                        {

                        }
                        if (subAtt30.Close == 2560.05M)
                        {

                        }

                        if (subAtt30.STMode == IndicatorMode.Buy)
                        {

                        }

                        if (subAtt30.STMode != IndicatorMode.None)
                        {
                            if (subAtt30.Close == 2404M)
                            {

                            }
                            if (subAtt30.Close == 2538.65M)
                            {

                            }

                            if (subAtt60.STModeDay60Min == subAtt30.STMode)
                            {
                                subAtt30.STModeDay30Min = subAtt60.STModeDay60Min;
                            }
                            else
                            {
                                subAtt30.STModeDay30Min = IndicatorMode.SquareOff;
                            }

                            if (subAtt30.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {
                            }

                            if (subAtt30.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {
                                Program.LogBackTest_Algo(position30Min, subAtt30.Close, subAtt30.Close, subAtt30, conn, trades, 0, subAtt30.TimeStamp, subAtt60.TimeStamp, subAtt30.STModeDay30Min);
                                trades++;
                            }
                        }
                    }
                }

            }

        }

        private void AnalyzeTrade(Position positionDay, Position position60Min, Position position30Min)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;
            foreach (var dayAtt in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(dayAtt);
                if (i > 14)
                {
                    if (dayAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                    {

                    }
                    IEnumerable<Attributes> positionFiltered = position60Min.PositionAttributes.Where(x => x.TimeStamp >= dayAtt.TimeStamp.AddDays(-2));
                    foreach (var subAtt60 in positionFiltered)
                    {
                        if (subAtt60.TimeStamp >= dayAtt.TimeStamp && subAtt60.TimeStamp <= dayAtt.TimeStamp.AddDays(1)
                            && subAtt60.STMode != IndicatorMode.None)
                        {
                            if (dayAtt.STMode == subAtt60.STMode)
                            {
                                subAtt60.STModeDay60Min = dayAtt.STMode;
                            }
                            else
                            {
                                subAtt60.STModeDay60Min = IndicatorMode.SquareOff;
                            }
                        }
                    }
                }
            }

            IEnumerable<Attributes> positionFiltered60 = position60Min.PositionAttributes.Where(
                      x => (x.TimeStamp >= position30Min.PositionAttributes[0].TimeStamp.AddDays(-1)));

            foreach (var subAtt60 in positionFiltered60)
            {
                if (subAtt60.STModeDay60Min != IndicatorMode.None && subAtt60.STModeDay60Min != IndicatorMode.SquareOff)
                {
                    IEnumerable<Attributes> positionFiltered = position30Min.PositionAttributes.Where(
                        x => (x.TimeStamp >= subAtt60.TimeStamp.AddMinutes(-90)));
                    foreach (var subAtt30 in position30Min.PositionAttributes)
                    {
                        int j = position30Min.PositionAttributes.IndexOf(subAtt30);

                        //if (subAtt.TimeStamp <= dayAtt.TimeStamp && subAtt.TimeStamp >= positionDay.PositionAttributes[i - 1].TimeStamp)
                        //{

                        //}

                        if (subAtt30.Close == 2516)
                        {

                        }

                        if (subAtt30.STMode == IndicatorMode.Buy)
                        {

                        }

                        if (subAtt30.TimeStamp >= subAtt60.TimeStamp && subAtt30.TimeStamp <= subAtt60.TimeStamp.AddMinutes(30)
                         && subAtt30.STMode != IndicatorMode.None)
                        {
                            if (subAtt30.Close == 2404M)
                            {

                            }
                            if (subAtt30.Close == 2538.65M)
                            {

                            }

                            if (subAtt60.STModeDay60Min == subAtt30.STMode)
                            {
                                subAtt30.STModeDay30Min = subAtt60.STModeDay60Min;
                            }
                            else
                            {
                                subAtt30.STModeDay30Min = IndicatorMode.SquareOff;
                            }

                            if (subAtt30.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {

                            }

                            if (subAtt30.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {
                                Program.LogBackTest_Algo(position30Min, subAtt30.Close, subAtt30.Close, subAtt30, conn, trades, 0, subAtt30.TimeStamp, subAtt60.TimeStamp, subAtt30.STModeDay30Min);
                                trades++;
                            }
                        }
                    }
                }

            }

        }

        private void AnalyzeTrade(Position positionDay, Position position30Min)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;
            foreach (var dayAtt in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(dayAtt);
                if (i > 14)
                {
                    if (dayAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                    {

                    }
                    //IEnumerable<Attributes> positionFiltered = position30Min.PositionAttributes.Where(x => x.TimeStamp >= dayAtt.TimeStamp.AddDays(-2));
                    foreach (var subAtt in position30Min.PositionAttributes)
                    {
                        int j = position30Min.PositionAttributes.IndexOf(subAtt);

                        //if (subAtt.TimeStamp <= dayAtt.TimeStamp && subAtt.TimeStamp >= positionDay.PositionAttributes[i - 1].TimeStamp)
                        //{

                        //}

                        if (subAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                        {

                        }
                        if (subAtt.Close == 2516M)
                        {

                        }

                        if (subAtt.TimeStamp >= dayAtt.TimeStamp && subAtt.TimeStamp <= dayAtt.TimeStamp.AddDays(1) && subAtt.STMode != IndicatorMode.None)
                        {
                            if (subAtt.Close == 2404M)
                            {

                            }
                            if (dayAtt.STMode == subAtt.STMode)
                            {
                                subAtt.STModeDay30Min = dayAtt.STMode;
                            }
                            else
                            {
                                subAtt.STModeDay30Min = IndicatorMode.SquareOff;
                            }

                            if (subAtt.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {

                            }


                            if (subAtt.STModeDay30Min != position30Min.PositionAttributes[j - 1].STModeDay30Min)
                            {
                                Program.LogBackTest_Algo(position30Min, subAtt.Close, subAtt.Close, subAtt, conn, trades, 0, subAtt.TimeStamp, subAtt.TimeStamp, subAtt.STModeDay30Min);
                                trades++;
                            }
                        }
                    }
                }

            }
        }


        private void AnalyzeTrade1(Position positionDay, Position position60Min)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;
            decimal startPrice = 0, endPrice = 0;
            IndicatorMode startMode, endMode;
            foreach (var dayAtt in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(dayAtt);
                if (i > 14)
                {
                    if (dayAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                    {

                    }
                    //IEnumerable<Attributes> positionFiltered = position30Min.PositionAttributes.Where(x => x.TimeStamp >= dayAtt.TimeStamp.AddDays(-2));

                    IEnumerable<Attributes> positionFiltered = position60Min.PositionAttributes
                        .Where(x => x.TimeStamp >= dayAtt.TimeStamp)
                        .Where(x => x.TimeStamp < dayAtt.TimeStamp.AddDays(1)).ToList();

                    if (positionFiltered.Count() > 0)
                    {

                    }
                    foreach (var subAtt in positionFiltered)
                    {
                        int j = position60Min.PositionAttributes.IndexOf(subAtt);

                        //if (subAtt.TimeStamp <= dayAtt.TimeStamp && subAtt.TimeStamp >= positionDay.PositionAttributes[i - 1].TimeStamp)
                        //{

                        //}

                        if (subAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                        {

                        }
                        if (subAtt.Close == 2516M)
                        {

                        }

                        if (subAtt.STMode != IndicatorMode.None)
                        {
                            if (subAtt.Close == 2404M)
                            {

                            }

                            if (dayAtt.STMode == subAtt.STMode)
                            {
                                subAtt.STModeDay60Min = dayAtt.STMode;
                            }
                            else
                            {
                                subAtt.STModeDay60Min = IndicatorMode.SquareOff;
                            }

                            if (subAtt.STModeDay60Min != position60Min.PositionAttributes[j - 1].STModeDay60Min)
                            {
                                if (subAtt.STModeDay60Min == IndicatorMode.Sell)
                                {
                                    startPrice = subAtt.Close;
                                }
                                else if (subAtt.STModeDay60Min == IndicatorMode.Buy)
                                {
                                    endPrice = subAtt.Close;
                                }
                                else if (subAtt.STModeDay60Min == IndicatorMode.SquareOff)
                                {
                                    if (position60Min.PositionAttributes[j - 1].STModeDay60Min == IndicatorMode.Sell)
                                        endPrice = subAtt.Close;
                                    else if (position60Min.PositionAttributes[j - 1].STModeDay60Min == IndicatorMode.Buy)
                                        startPrice = subAtt.Close;

                                }
                            }


                            if (subAtt.STModeDay60Min != position60Min.PositionAttributes[j - 1].STModeDay60Min)
                            {
                                if (position60Min.PositionAttributes[j - 1].STModeDay60Min == IndicatorMode.SquareOff)
                                {

                                    Program.LogBackTest_Algo(position60Min, subAtt.Close, subAtt.Close, subAtt, conn, trades, 0, subAtt.TimeStamp, subAtt.TimeStamp, subAtt.STModeDay60Min);

                                }
                                else
                                {
                                    if (position60Min.PositionAttributes[j - 1].STModeDay60Min == IndicatorMode.Buy)

                                    {
                                        Program.LogBackTest_Algo(position60Min, 2, subAtt.Close, subAtt, conn, trades, startPrice - endPrice, subAtt.TimeStamp, subAtt.TimeStamp, subAtt.STModeDay60Min);

                                    }
                                    else
                                        Program.LogBackTest_Algo(position60Min, subAtt.Close, subAtt.Close, subAtt, conn, trades, startPrice - endPrice, subAtt.TimeStamp, subAtt.TimeStamp, subAtt.STModeDay60Min);
                                }
                                trades++;
                            }
                        }
                    }
                }

            }
        }

        private void AnalyzeTrade2(Position positionDay, Position position60Min)
        {
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            int trades = 1;
            foreach (var dayAtt in positionDay.PositionAttributes)
            {
                int i = positionDay.PositionAttributes.IndexOf(dayAtt);
                if (i > 14)
                {
                    if (dayAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                    {

                    }

                    IEnumerable<Attributes> positionFiltered = position60Min.PositionAttributes
                        .Where(x => x.TimeStamp >= dayAtt.TimeStamp)
                        .Where(x => x.TimeStamp < dayAtt.TimeStamp.AddDays(1)).ToList();

                    if (positionFiltered.Count() > 0)
                    {

                    }
                    foreach (var subAtt in positionFiltered)
                    {
                        int j = position60Min.PositionAttributes.IndexOf(subAtt);

                        if (subAtt.TimeStamp > Program.GetIndianDateTime().AddDays(-10))
                        {

                        }
                        if (subAtt.Close == 2516M)
                        {

                        }

                        if (subAtt.STMode != IndicatorMode.None)
                        {
                            if (subAtt.Close == 2404M)
                            {

                            }

                            if (dayAtt.STMode == subAtt.STMode)
                            {
                                if (subAtt.PostiveDIn < subAtt.NegativeDIn && dayAtt.STMode == IndicatorMode.Buy)
                                {
                                    subAtt.STModeDay60Min = IndicatorMode.SquareOff;
                                }
                                if (subAtt.PostiveDIn > subAtt.NegativeDIn && dayAtt.STMode == IndicatorMode.Sell)
                                {
                                    subAtt.STModeDay60Min = IndicatorMode.SquareOff;
                                }
                                else
                                {
                                    subAtt.STModeDay60Min = dayAtt.STMode;
                                }
                            }
                            else
                            {
                                subAtt.STModeDay60Min = IndicatorMode.SquareOff;
                            }

                            if (subAtt.STModeDay60Min != position60Min.PositionAttributes[j - 1].STModeDay60Min)
                            {

                            }


                            if (subAtt.STModeDay60Min != position60Min.PositionAttributes[j - 1].STModeDay60Min)
                            {
                                Program.LogBackTest_Algo(position60Min, subAtt.Close, subAtt.Close, subAtt, conn, trades, 0, subAtt.TimeStamp, subAtt.TimeStamp, subAtt.STModeDay60Min);
                                trades++;
                            }
                        }
                    }
                }

            }
        }

    }
}
