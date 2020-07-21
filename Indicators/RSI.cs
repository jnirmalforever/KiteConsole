using KiteConnect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;

namespace KiteConsole_v2
{
    public class RSIAttributes
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
        public IndicatorMode STMode { get; set; }
        public IndicatorMode Mode { get; set; }
        public IndicatorMode Mode2 { get; set; }
        public decimal PL { get; internal set; }
        public decimal EMA1 { get; internal set; }
        public decimal EMA2 { get; internal set; }
        public IndicatorMode RSIMode { get; internal set; }
        public decimal AverageGain { get; internal set; }
        public decimal AverageLoss { get; internal set; }
        public decimal Gain { get; internal set; }
        public decimal Loss { get; internal set; }
        public decimal RS { get; internal set; }
        public decimal RSI { get; internal set; }
        public decimal Change { get; internal set; }
        public bool IsNewSFSignal { get; internal set; }
        public bool IsRSIValid { get; internal set; }
        public bool IsMACDValid { get; internal set; }
        public decimal MACD { get; internal set; }
        public decimal SignalLine { get; internal set; }
        public IndicatorMode MACDMode { get; internal set; }
        public bool EarlyBird { get; internal set; } = false;
    }

    public class RSI : IIndicator
    {
        private decimal Round(decimal input)
        {
            return Math.Round(input, 2);

        }
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
                historical = Program.GetHistoricalData(kite, positionSettings.ParentInstrument, GetIndianDateTime().AddDays(-180), GetIndianDateTime(), positionSettings.Interval, false);
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

                List<RSIAttributes> RSIAttributes = historical.OrderBy(x => x.TimeStamp).Select(x => new RSIAttributes
                {
                    Close = x.Close,
                    TimeStamp = x.TimeStamp,
                    High = x.High,
                    Low = x.Low,
                    Open = x.Open
                }).OrderBy(x => x.TimeStamp).ToList();


                foreach (RSIAttributes att in RSIAttributes)
                {
                    int i = RSIAttributes.IndexOf(att);

                    if (i > 0 && i < RSIAttributes.Count)
                    {
                        att.Change = RSIAttributes[i].Close - RSIAttributes[i - 1].Close;
                        att.Gain = att.Change > 0 ? att.Change : 0;
                        att.Loss = att.Change < 0 ? att.Change : 0;
                    }
                }

                foreach (RSIAttributes att in RSIAttributes)
                {
                    int i = RSIAttributes.IndexOf(att);
                    if (i == Constants.RSIPeriod)
                    //if (i == Constants.RSIPeriod + 1)

                    {
                        att.AverageGain = Round(RSIAttributes.OrderBy(x => x.TimeStamp).Skip(i + 1 - Constants.RSIPeriod).Take(Constants.RSIPeriod).Average(x => x.Gain));
                        att.AverageLoss = Round(RSIAttributes.OrderBy(x => x.TimeStamp).Skip(i + 1 - Constants.RSIPeriod).Take(Constants.RSIPeriod).Average(x => x.Loss));
                        if (att.AverageLoss != 0)
                            att.RS = Round(Decimal.Divide(att.AverageGain, att.AverageLoss));
                        else
                            att.RS = 0;
                        att.RSI = Round(100 - (100 / (1 + Math.Abs(att.RS))));

                    }
                    if (i > Constants.RSIPeriod)
                    {
                        att.AverageGain = Round(Decimal.Divide(Decimal.Add(decimal.Multiply(RSIAttributes[i - 1].AverageGain, 13), att.Gain), 14));
                        att.AverageLoss = Round(Decimal.Divide(Decimal.Add(decimal.Multiply(RSIAttributes[i - 1].AverageLoss, 13), att.Loss), 14));
                        if (att.AverageLoss != 0)
                            att.RS = Round(Decimal.Divide(att.AverageGain, att.AverageLoss));
                        else
                            att.RS = 0;
                        att.RSI = Round(100 - (100 / (1 + Math.Abs(att.RS))));

                    }
                }
                //Reference
                //http://cns.bu.edu/~gsc/CN710/fincast/Technical%20_indicators/Relative%20Strength%20Index%20(RSI).htm

                decimal multiplier = Decimal.Divide(2, (4 + 1));
                decimal shortSMA = Decimal.Divide(RSIAttributes.OrderBy(x => x.TimeStamp).Skip(14)
                                           .Take(4).Sum(x => x.RSI), 4);

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                foreach (RSIAttributes att in RSIAttributes)
                {
                    int i = RSIAttributes.IndexOf(att);
                    if (i == Constants.RSIPeriod + 4 - 1)
                    {
                        att.EMA1 = shortSMA;// (att.RSI - shortSMA) * multiplier + shortSMA;
                    }
                    else if (i > Constants.RSIPeriod + 4 - 1)
                    {
                        att.EMA1 = Round((att.RSI - RSIAttributes[i - 1].EMA1) * multiplier + RSIAttributes[i - 1].EMA1);
                    }
                }

                multiplier = Decimal.Divide(2, (14 + 1));
                shortSMA = Decimal.Divide(RSIAttributes.OrderBy(x => x.TimeStamp).Skip(14)
                                          .Take(14).Sum(x => x.Momentum), 14);

                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                foreach (RSIAttributes att in RSIAttributes)
                {
                    int i = RSIAttributes.IndexOf(att);
                    if (i == Constants.RSIPeriod + 14 - 1)
                    {
                        att.EMA2 = shortSMA;// (att.RSI - shortSMA) * multiplier + shortSMA;
                    }
                    else if (i > Constants.RSIPeriod + 14 - 1)
                    {
                        att.EMA2 = (att.RSI - RSIAttributes[i - 1].EMA2) * multiplier + RSIAttributes[i - 1].EMA2;
                    }
                }

                foreach (RSIAttributes att in RSIAttributes)
                {
                    int i = RSIAttributes.IndexOf(att);
                    if (i > Constants.RSIPeriod + 14)
                    {
                        if (att.EMA1 > att.EMA2)
                        {
                            att.RSIMode = IndicatorMode.Buy;
                        }
                        else
                        {
                            att.RSIMode = IndicatorMode.Sell;
                        }
                    }
                }
                SuperTrend(positionSettings, kite, RSIAttributes);
                return positionSettings;


                //Improve, comment above rsimode before using below
                foreach (RSIAttributes att in RSIAttributes)
                {
                    int i = RSIAttributes.IndexOf(att);
                    if (i > Constants.RSIPeriod + 14)
                    {
                        if (att.EMA1 > att.EMA2)
                        {
                            if (decimal.Subtract(att.EMA1, att.EMA2) < Convert.ToDecimal(1))
                            {
                                att.RSIMode = RSIAttributes[RSIAttributes.IndexOf(att) - 1].RSIMode;
                                continue;
                            }

                            att.RSIMode = IndicatorMode.Buy;
                        }
                        else
                        {
                            if (decimal.Subtract(att.EMA2, att.EMA1) < Convert.ToDecimal(1))
                            {
                                att.RSIMode = RSIAttributes[RSIAttributes.IndexOf(att) - 1].RSIMode;
                                continue;
                            }

                            att.RSIMode = IndicatorMode.Sell;
                        }
                    }
                }



                // Calculate PL
                var pls = RSIAttributes[29].Close;
                decimal currentPL = 00;
                foreach (RSIAttributes st in RSIAttributes)
                {
                    int i = RSIAttributes.IndexOf(st);

                    if (i > 28 && RSIAttributes[i].RSIMode != RSIAttributes[i - 1].RSIMode)
                    {
                        var ple = (RSIAttributes.Count - 1 == i) ? RSIAttributes[i].Open : RSIAttributes[i + 1].Open;
                        if (RSIAttributes[i - 1].RSIMode == IndicatorMode.Buy)
                        {
                            positionSettings.PL = Decimal.Subtract(ple, pls);
                            RSIAttributes[i].PL = Decimal.Subtract(ple, pls);

                        }
                        else
                        {
                            positionSettings.PL = Decimal.Subtract(pls, ple);
                            RSIAttributes[i].PL = Decimal.Subtract(pls, ple);
                        }
                        //if (positionSettings.PL == Convert.ToDecimal(173.50) || positionSettings.PL == Convert.ToDecimal(406.30))
                        //{

                        //}
                        pls = (RSIAttributes.Count - 1 == i) ? RSIAttributes[i].Open : RSIAttributes[i + 1].Open;
                        //if (pls == 2250.95M)
                        //{

                        //}
                    }
                    if (RSIAttributes[RSIAttributes.Count - 1].RSIMode == IndicatorMode.Buy)
                        currentPL = Decimal.Subtract(RSIAttributes[RSIAttributes.Count - 1].Close, pls);
                    else if (RSIAttributes[RSIAttributes.Count - 1].RSIMode == IndicatorMode.Sell)
                        currentPL = Decimal.Subtract(pls, RSIAttributes[RSIAttributes.Count - 1].Close);
                }



                decimal totalPL = 0;
                foreach (RSIAttributes st in RSIAttributes)
                {
                    if (st.PL != 0)
                        totalPL += st.PL;
                }
                int count = 0;
                foreach (RSIAttributes st in RSIAttributes)
                {
                    if (st.PL != 0)
                        count += 1;
                }
                totalPL = 0;
                count = 0;
                foreach (RSIAttributes st in RSIAttributes)
                {
                    if (st.TimeStamp >= GetIndianDateTime().AddDays(-120))
                    {

                        if (st.PL != 0)
                        {
                            totalPL += st.PL;
                            count += 1;
                        }
                    }
                }

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    positionSettings.CurrentMode = RSIAttributes[RSIAttributes.Count - 1].STMode;
                    positionSettings.CMode = RSIAttributes[RSIAttributes.Count - 1].STMode;
                }

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    //positionSettings.NewLongEMA = RSIAttributes[RSIAttributes.Count - 1].SAR;
                    positionSettings.NewShortEMA = RSIAttributes[RSIAttributes.Count - 1].Close;
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

        public Settings SuperTrend(Settings positionSettings, Kite kite, List<RSIAttributes> RSIAttributesInput)
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


            if (RSIAttributesInput.Count > 0)
            {
                //decimal longSMA = Decimal.Divide(historical.OrderBy(x => x.TimeStamp).Take(Constants.longEMA).Sum(x => x.Close), Constants.longEMA);
                //decimal shortSMA = Decimal.Divide(historical.OrderBy(x => x.TimeStamp).Skip(Constants.longEMA - Constants.shortEMA)
                //                            .Take(Constants.shortEMA).Sum(x => x.Close), Constants.shortEMA);

                List<RSIAttributes> RSIAttributes = RSIAttributesInput.OrderBy(x => x.TimeStamp).Select(x => new RSIAttributes
                {
                    Close = x.Close,
                    TimeStamp = x.TimeStamp,
                    High = x.High,
                    Low = x.Low,
                    Open = x.Open,
                    RSIMode = x.RSIMode,
                    EMA1 = x.EMA1,
                    EMA2 = x.EMA2
                }).OrderBy(x => x.TimeStamp).ToList();


                //Calculating the EMA: [Closing price-EMA (previous day)] x multiplier + EMA (previous day)

                foreach (RSIAttributes att in RSIAttributes)
                {
                    int i = RSIAttributes.IndexOf(att);
                    if (i == 0)
                    {

                    }
                    else
                    {
                        decimal findMax = Math.Max((RSIAttributes[i].High - RSIAttributes[i].Low), Math.Abs(RSIAttributes[i].High - RSIAttributes[i - 1].Close));
                        att.TrueRange = Math.Max(Math.Abs(RSIAttributes[i].Low - RSIAttributes[i - 1].Close), findMax);
                    }
                    if (i == Constants.SuperTrend)
                    {
                        att.ATR = Decimal.Divide(RSIAttributes.OrderBy(x => x.TimeStamp).Skip(1).Take(Constants.SuperTrend).Sum(x => x.TrueRange), Constants.SuperTrend);
                    }
                }

                //decimal multiplier = Decimal.Divide(Convert.ToDecimal(Constants.STMultiplier), (Constants.SuperTrend + 1));
                decimal multiplier = Decimal.Divide(Convert.ToDecimal(positionSettings.IndicatorParmOne), (Constants.SuperTrend + 1));

                List<RSIAttributes> fRSIAttributes = RSIAttributes.OrderBy(x => x.TimeStamp).Skip(Constants.SuperTrend).Select(x => new RSIAttributes
                {
                    Close = x.Close,
                    TimeStamp = x.TimeStamp,
                    High = x.High,
                    Low = x.Low,
                    Open = x.Open,
                    ATR = x.ATR,
                    TrueRange = x.TrueRange,
                    RSIMode = x.RSIMode,
                    EMA1 = x.EMA1,
                    EMA2 = x.EMA2
                }).OrderBy(x => x.TimeStamp).ToList();

                //TrueRange EMA (Zerodha Chart)
                foreach (RSIAttributes att in fRSIAttributes)
                {
                    int i = fRSIAttributes.IndexOf(att);
                    if (i > 0)
                    {
                        att.ATR = (att.TrueRange - fRSIAttributes[i - 1].ATR) * multiplier + fRSIAttributes[i - 1].ATR;
                    }
                }
                //TrueRange Average (PI Chart)
                //foreach (RSIAttributes att in fRSIAttributes)
                //{
                //    int i = fRSIAttributes.IndexOf(att);
                //    att.ATR = Decimal.Divide(RSIAttributes.OrderBy(x => x.TimeStamp).Skip(i + 1).Take(Constants.SuperTrend).Sum(x => x.TrueRange), Constants.SuperTrend);
                //}

                //Current ATR = [(Prior ATR x 13) + Current TR] / 14
                //foreach (RSIAttributes att in fRSIAttributes)
                //{
                //    int i = fRSIAttributes.IndexOf(att);
                //    if (i > 0)
                //    {
                //        att.ATR = ((fRSIAttributes[i - 1].ATR * Constants.SuperTrend - 1) + att.TrueRange) / Constants.SuperTrend;
                //    }
                //}

                //BASIC UPPERBAND = (HIGH + LOW) / 2 + Multiplier * ATR
                //BASIC LOWERBAND = (HIGH + LOW) / 2 - Multiplier * ATR
                foreach (RSIAttributes att in fRSIAttributes)
                {
                    //att.basicUpperBand = Decimal.Divide((att.High + att.Low), 2) + Decimal.Multiply(Convert.ToDecimal(Constants.STMultiplier), att.ATR);
                    //att.basicLowerBand = Decimal.Divide((att.High + att.Low), 2) - Decimal.Multiply(Convert.ToDecimal(Constants.STMultiplier), att.ATR);

                    att.basicUpperBand = Decimal.Divide((att.High + att.Low), 2) + Decimal.Multiply(Convert.ToDecimal(positionSettings.IndicatorParmOne), att.ATR);
                    att.basicLowerBand = Decimal.Divide((att.High + att.Low), 2) - Decimal.Multiply(Convert.ToDecimal(positionSettings.IndicatorParmOne), att.ATR);
                }


                //FINAL UPPERBAND = IF((Current BASICUPPERBAND < Previous FINAL UPPERBAND) and(Previous Close > Previous FINAL UPPERBAND)) THEN(Current BASIC UPPERBAND) ELSE Previous FINALUPPERBAND)
                //FINAL LOWERBAND = IF((Current BASIC LOWERBAND > Previous FINAL LOWERBAND) and(Previous Close < Previous FINAL LOWERBAND)) THEN(Current BASIC LOWERBAND) ELSE Previous FINAL LOWERBAND)
                foreach (RSIAttributes att in fRSIAttributes)
                {
                    int i = fRSIAttributes.IndexOf(att);
                    if (i == 0)
                    {
                        att.finalLowerBand = att.basicLowerBand;
                        att.finalUpperBand = att.basicUpperBand;
                        continue;
                    }
                    if ((att.basicUpperBand < fRSIAttributes[i - 1].finalUpperBand) || (fRSIAttributes[i - 1].Close > fRSIAttributes[i - 1].finalUpperBand))
                    {
                        att.finalUpperBand = att.basicUpperBand;
                    }
                    else
                    {
                        att.finalUpperBand = fRSIAttributes[i - 1].finalUpperBand;
                    }

                    if ((att.basicLowerBand > fRSIAttributes[i - 1].finalLowerBand) || (fRSIAttributes[i - 1].Close < fRSIAttributes[i - 1].finalLowerBand))
                    {
                        att.finalLowerBand = att.basicLowerBand;
                    }
                    else
                    {
                        att.finalLowerBand = fRSIAttributes[i - 1].finalLowerBand;
                    }
                }

                positionSettings.NewLongEMA = 0;
                positionSettings.NewShortEMA = 0;

                foreach (RSIAttributes att in fRSIAttributes)
                {
                    int i = fRSIAttributes.IndexOf(att);

                    if (i == 0 && att.Close <= att.finalUpperBand)
                    {
                        att.STMode = IndicatorMode.Sell;
                        continue;
                    }
                    else if (i == 0)
                    {
                        att.STMode = IndicatorMode.Buy;
                        continue;
                    }

                    if (fRSIAttributes[i - 1].STMode == IndicatorMode.Buy)
                    {
                        if (att.Close <= att.finalLowerBand)
                        {
                            att.IsNewSFSignal = true;
                            att.STMode = IndicatorMode.Sell;
                        }
                        else
                        {
                            att.STMode = fRSIAttributes[i - 1].STMode;
                            att.IsNewSFSignal = false;

                        }
                    }
                    else if (fRSIAttributes[i - 1].STMode == IndicatorMode.Sell)
                    {
                        if (att.Close <= att.finalUpperBand)
                        {
                            att.STMode = fRSIAttributes[i - 1].STMode;
                            att.IsNewSFSignal = false;

                        }
                        else
                        {
                            att.STMode = IndicatorMode.Buy;
                            att.IsNewSFSignal = true;

                        }
                    }
                }

                foreach (RSIAttributes att in fRSIAttributes)
                {
                    int i = fRSIAttributes.IndexOf(att);
                    if (i > 28)
                    {
                        if (att.STMode == IndicatorMode.Buy)
                        {
                            if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Buy)
                            {
                                att.IsRSIValid = true;
                                att.Mode = IndicatorMode.Buy;
                            }
                            else if (fRSIAttributes[i - 1].IsRSIValid == true && att.IsNewSFSignal == false)
                            {
                                att.IsRSIValid = true;
                                att.Mode = IndicatorMode.Buy;

                            }
                            else if (att.IsRSIValid == false && att.RSIMode == IndicatorMode.Buy)
                            {
                                att.IsRSIValid = true;
                                att.Mode = IndicatorMode.Buy;
                            }
                            else
                                att.Mode = IndicatorMode.Sell;
                        }
                        else if (att.STMode == IndicatorMode.Sell)
                        {
                            if (att.IsNewSFSignal && att.RSIMode == IndicatorMode.Sell)
                            {
                                att.IsRSIValid = true;
                                att.Mode = IndicatorMode.Sell;
                            }
                            else if (fRSIAttributes[i - 1].IsRSIValid == true && att.IsNewSFSignal == false)
                            {
                                att.IsRSIValid = true;
                                att.Mode = IndicatorMode.Sell;

                            }
                            else if (att.IsRSIValid == false && att.RSIMode == IndicatorMode.Sell)
                            {
                                att.IsRSIValid = true;
                                att.Mode = IndicatorMode.Sell;
                            }
                            else
                                att.Mode = IndicatorMode.Buy;
                        }
                    }
                }


                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    positionSettings.CurrentMode = fRSIAttributes[fRSIAttributes.Count - 2].STMode;
                    positionSettings.CMode = fRSIAttributes[fRSIAttributes.Count - 2].STMode;
                }
                else
                {
                    positionSettings.CurrentMode = fRSIAttributes[fRSIAttributes.Count - 1].STMode;
                    positionSettings.CMode = fRSIAttributes[fRSIAttributes.Count - 1].STMode;
                }

                //if (positionSettings.CMode == IndicatorMode.Buy)
                //{
                //    positionSettings.NewLongEMA = fRSIAttributes[fRSIAttributes.Count - 1].finalLowerBand;
                //    positionSettings.NewShortEMA = fRSIAttributes[fRSIAttributes.Count - 1].Close;
                //}
                //else
                //{
                //    positionSettings.NewLongEMA = fRSIAttributes[fRSIAttributes.Count - 1].finalUpperBand;
                //    positionSettings.NewShortEMA = fRSIAttributes[fRSIAttributes.Count - 1].Close;
                //}

                if (positionSettings.Interval == Constants.INTERVAL_15MINUTE || positionSettings.Interval == Constants.INTERVAL_30MINUTE)
                {
                    if (positionSettings.CMode == IndicatorMode.Buy)
                    {
                        positionSettings.NewLongEMA = fRSIAttributes[fRSIAttributes.Count - 2].finalLowerBand;
                        positionSettings.NewShortEMA = fRSIAttributes[fRSIAttributes.Count - 1].Close;

                    }
                    else
                    {
                        positionSettings.NewLongEMA = fRSIAttributes[fRSIAttributes.Count - 2].finalUpperBand;
                        positionSettings.NewShortEMA = fRSIAttributes[fRSIAttributes.Count - 1].Close;
                    }
                }

                //Calculate PL
                var pls = fRSIAttributes[29].Close;
                decimal currentPL = 00;
                foreach (RSIAttributes st in fRSIAttributes)
                {
                    int i = fRSIAttributes.IndexOf(st);

                    if (i > 28 && fRSIAttributes[i].Mode != fRSIAttributes[i - 1].Mode)
                    {
                        var ple = (fRSIAttributes.Count - 1 == i) ? fRSIAttributes[i].Open : fRSIAttributes[i + 1].Open;
                        if (fRSIAttributes[i - 1].Mode == IndicatorMode.Buy)
                        {
                            positionSettings.PL = Decimal.Subtract(ple, pls);
                            fRSIAttributes[i].PL = Decimal.Subtract(ple, pls);

                        }
                        else
                        {
                            positionSettings.PL = Decimal.Subtract(pls, ple);
                            fRSIAttributes[i].PL = Decimal.Subtract(pls, ple);
                        }
                        //if (positionSettings.PL == Convert.ToDecimal(173.50) || positionSettings.PL == Convert.ToDecimal(406.30))
                        //{

                        //}
                        pls = (fRSIAttributes.Count - 1 == i) ? fRSIAttributes[i].Open : fRSIAttributes[i + 1].Open;
                        //if (pls == 2250.95M)
                        //{

                        //}
                    }
                    if (fRSIAttributes[fRSIAttributes.Count - 1].Mode == IndicatorMode.Buy)
                        currentPL = Decimal.Subtract(fRSIAttributes[fRSIAttributes.Count - 1].Close, pls);
                    else if (fRSIAttributes[fRSIAttributes.Count - 1].Mode == IndicatorMode.Sell)
                        currentPL = Decimal.Subtract(pls, fRSIAttributes[fRSIAttributes.Count - 1].Close);
                }

                decimal totalPL = 0;
                foreach (RSIAttributes st in fRSIAttributes)
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
                foreach (RSIAttributes st in fRSIAttributes)
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
                foreach (RSIAttributes st in fRSIAttributes)
                {
                    if (st.TimeStamp >= GetIndianDateTime().AddDays(-90))
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
