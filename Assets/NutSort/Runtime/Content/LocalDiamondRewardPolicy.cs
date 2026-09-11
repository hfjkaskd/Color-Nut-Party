using System;
using Newtonsoft.Json.Linq;
namespace NutSort.Content
{
    // Black-box interval model. The server's source code and RNG are not known.
    // Country intervals are authored in the resource, not hard-coded here.
    public static class LocalDiamondRewardPolicy
    {
        public static double Calculate(JObject config,string requestedCountry,int completed,double balance,
            Func<float,float,float> random,out double bandBalance)
        {
            bandBalance=0;
            if(balance<0||double.IsNaN(balance)||double.IsInfinity(balance))
                throw new InvalidOperationException("Invalid diamond balance.");
            if(!(bool)config["enabled"]||completed<(int)config["firstCompletedLevel"])return 0;
            string country=CountryRewardCatalog.ResolveCountry(requestedCountry);
            var bands=(JArray)config["profiles"][country];
            if(bands==null||bands.Count==0)throw new InvalidOperationException("Missing diamond intervals.");
            JObject chosen=(JObject)bands[0];
            foreach(JObject band in bands)
            {
                if(balance<(double)band["balance"])break;
                chosen=band;
            }
            bandBalance=(double)chosen["balance"];
            double minimum=(double)chosen["minimum"],maximum=(double)chosen["maximum"];
            if(minimum<0||maximum<minimum||double.IsNaN(minimum)||double.IsInfinity(maximum))
                throw new InvalidOperationException("Invalid diamond interval.");
            double value=random((float)minimum,(float)maximum);
            if(double.IsNaN(value)||double.IsInfinity(value)||value<minimum-0.00001||value>maximum+0.00001)
                throw new InvalidOperationException("Diamond random value outside configured interval.");
            return Math.Round(value,(int)config["precision"][country],MidpointRounding.AwayFromZero);
        }
    }
}
