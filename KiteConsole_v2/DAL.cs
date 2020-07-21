using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace KiteConsole_v2
{
    static class DAL
    {
        public static List<Attributes> GetDataFromDB(string symbol)
        {
            List<Attributes> attributes = new List<Attributes>();
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);

            conn.Open();
            //SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE Symbol = '" + symbol + "'", conn);
            //SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE Symbol = '" + symbol + "' AND Timestamp  >=  CONVERT(VARCHAR, '2018-01-01 00:00:00', 103)", conn);
            SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE Symbol = '" + symbol + "' AND Timestamp  >=  CONVERT(VARCHAR, '2018-01-01 00:00:00', 103) AND Timestamp < CONVERT(VARCHAR, '2019-01-01 00:00:00', 103)", conn);
            //SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE Symbol = '" + symbol + "' AND Timestamp  >=  CONVERT(VARCHAR, '2017-01-01 00:00:00', 103) AND Timestamp < CONVERT(VARCHAR, '2018-01-01 00:00:00', 103)", conn);
            //SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM HistoricalData WHERE Symbol = '" + symbol + "' AND Timestamp  >=  CONVERT(VARCHAR, '2016-01-01 00:00:00', 103) AND Timestamp < CONVERT(VARCHAR, '2017-01-01 00:00:00', 103)", conn);

            DataTable dt = new DataTable();
            da.Fill(dt);
            conn.Close();

            foreach (DataRow row in dt.Rows)
            {
                attributes.Add(new Attributes()
                {
                    Open = Convert.ToDecimal(row["Open"]),
                    High = Convert.ToDecimal(row["High"]),
                    Low = Convert.ToDecimal(row["Low"]),
                    Close = Convert.ToDecimal(row["Close"]),
                    TimeStamp = Convert.ToDateTime(row["Timestamp"]),
                });
            }
            return attributes;
        }
        public static RunSettings GetSettingsDataFromDB(string InstrumentToken)
        {
            List<RunSettings> attributes = new List<RunSettings>();
            string connectionString = ConfigurationManager.AppSettings["KiteDB"];
            SqlConnection conn = new SqlConnection(connectionString);

            conn.Open();
            SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM RunSettings where Instrument='" + InstrumentToken + "'", conn);
            DataTable dt = new DataTable();
            da.Fill(dt);
            conn.Close();

            foreach (DataRow row in dt.Rows)
            {
                attributes.Add(new RunSettings()
                {
                    Instrument = Convert.ToString(row["Instrument"]),
                    STPeriod = Convert.ToInt32(row["STPeriod"]),
                    STMultiplier = Convert.ToDecimal(row["STMultiplier"]),
                    STBasic = Convert.ToDecimal(row["STBasic"]),
                    MACDEMA1 = Convert.ToInt32(row["MACDEMA1"]),
                    MACDEMA2 = Convert.ToInt32(row["MACDEMA2"]),
                    MACDSignalLine = Convert.ToInt32(row["MACDSignalLine"]),
                    RSIPeriod = Convert.ToInt32(row["RSIPeriod"]),
                    RSILong = Convert.ToInt32(row["RSILong"]),
                    RSIShort = Convert.ToInt32(row["RSIShort"])
                });
            }
            return attributes[0];
        }

    }
}
