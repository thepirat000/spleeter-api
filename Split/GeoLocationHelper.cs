using System;
using IpData;
using IpData.Models;
using System.Threading.Tasks;

namespace SpleeterAPI.Split
{
    public static class GeoLocationHelper
    {
        private static readonly IpDataClient _client = new IpDataClient("bffcaf087cf77de60b28402d8b1f96a5124da75bf9688693dd648aff");

        public static string GetGeoLocation(string ip)
        {
            try
            {
                var ipInfo = GetIpInfo(ip).GetAwaiter().GetResult();
                return ipInfo == null ? "" : $"{ipInfo.City}, {ipInfo.CountryName}";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public static async Task<IpInfo> GetIpInfo(string ip)
        {
            return await _client.Lookup(ip);
        }
    }
}
