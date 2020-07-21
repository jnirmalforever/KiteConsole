using KiteConnect;
using Newtonsoft.Json;
using System.Net.Http;

namespace Controller
{

    public class Margins
    {
        public decimal Margin { get; set; }
        public decimal co_lower { get; set; }
        public decimal mis_multiplier { get; set; }
        public string TradingSymbol { get; set; }
        public decimal co_upper { get; set; }
        public decimal nrml_margin { get; set; }
        public decimal mis_margin { get; set; }
    }

    public class Controller
    {
        public async void UpdateTodayMargin(Kite kite)
        {
            //GetFutureMargin
            Margins[] margins = null;
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync("https://api.kite.trade/margins/futures");

            if (response.IsSuccessStatusCode)
            {
                string response1 = await response.Content.ReadAsStringAsync();
                margins = JsonConvert.DeserializeObject<Margins[]>(response1);
            }
            var dataAccess = new DataAccess.DataAccess();
            dataAccess.UpdateMarginToDB();

            

        }

    }
}
