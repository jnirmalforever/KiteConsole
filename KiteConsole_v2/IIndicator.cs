using KiteConnect;
using System.Collections.Generic;

namespace KiteConsole_v2
{

    public interface IIndicator
    {
        Position StartLooking(Settings positionSettings, Kite kite, List<Attributes> Attributes);
        Settings StartLooking(Settings positionSettings, Kite kite);
    }
    public enum IndicatorMode
    {
        Buy = 1,
        Sell = -1,
        None = 0,
        BuyNWait = 2,
        SellNWait = 3,
        WaitForFlip = 4,
        NoTrade = 5,
        SquareOff = 6
    }
    public enum HACandle
    {
        Green = 1,
        Red = -1,
        None = 0,
    }

    public class Constants
    {

        public const decimal minimumAF = 0.01M;
        public const decimal maximumAF = 0.22M;
        public const string PRODUCT_MIS = "MIS";
        public const string EXCHANGE_BFO = "BFO";
        public const string EXCHANGE_MCX = "MCX";
        public const string MARGIN_EQUITY = "equity";
        public const string MARGIN_COMMODITY = "commodity";
        public const string MODE_FULL = "full";
        public const string MODE_QUOTE = "quote";
        public const string MODE_LTP = "ltp";
        public const string EXCHANGE_CDS = "CDS";
        public const string POSITION_DAY = "day";
        public const string INTERVAL_MINUTE = "minute";
        public const string INTERVAL_3MINUTE = "3minute";
        public const string INTERVAL_5MINUTE = "5minute";
        public const string INTERVAL_10MINUTE = "10minute";
        public const string INTERVAL_15MINUTE = "15minute";
        public const string INTERVAL_30MINUTE = "30minute";
        public const string INTERVAL_60MINUTE = "60minute";
        public const string POSITION_OVERNIGHT = "overnight";
        public const string EXCHANGE_NFO = "NFO";
        public const string EXCHANGE_BSE = "BSE";
        public const string EXCHANGE_NSE = "NSE";
        public const string PRODUCT_CNC = "CNC";
        public const string PRODUCT_NRML = "NRML";
        public const string ORDER_TYPE_MARKET = "MARKET";
        public const string ORDER_TYPE_LIMIT = "LIMIT";
        public const string ORDER_TYPE_SLM = "SL-M";
        public const string ORDER_TYPE_SL = "SL";
        public const string ORDER_STATUS_COMPLETE = "COMPLETE";
        public const string ORDER_STATUS_CANCELLED = "CANCELLED";
        public const string ORDER_STATUS_REJECTED = "REJECTED";
        public const string VARIETY_REGULAR = "regular";
        public const string VARIETY_BO = "bo";
        public const string VARIETY_CO = "co";
        public const string VARIETY_AMO = "amo";
        public const string TRANSACTION_TYPE_BUY = "BUY";
        public const string TRANSACTION_TYPE_SELL = "SELL";
        public const string VALIDITY_DAY = "DAY";
        public const string VALIDITY_IOC = "IOC";
        public const string INTERVAL_DAY = "day";
        public const double STMultiplier = 1.5;
        public const int MomentumPeriod = 14;
        public const int RSIPeriod = 14;
        public const int MACDEMA1 = 12;
        public const int MACDEMA2 = 26;
        public const int MACDSignalLine = 9;
        public const int ADXn = 14;
        public const int longEMA = 20;
        public const int shortEMA = 10;
        public const int SuperTrend = 14;
        public const int adxEMA = 5;


    }
}
