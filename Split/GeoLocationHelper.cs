using System;
using IpData;
using IpData.Models;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace SpleeterAPI.Split
{
    public static class GeoLocationHelper
    {
        private static readonly IpDataClient _client = new IpDataClient("bffcaf087cf77de60b28402d8b1f96a5124da75bf9688693dd648aff");

        private static readonly ConcurrentDictionary<string, IpInfo> _ipInfoCache = new ConcurrentDictionary<string, IpInfo>();

        public static string GetGeoLocation(string ip)
        {
            try
            {
                var ipInfo = GetIpInfo(ip).GetAwaiter().GetResult();
                return ipInfo == null ? "" : !string.IsNullOrEmpty(ipInfo.City) ? $"{ipInfo.City}, {ipInfo.CountryName}" : ipInfo.CountryName;
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public static async Task<IpInfo> GetIpInfo(string ip)
        {
            IpInfo info;
            if (!_ipInfoCache.TryGetValue(ip, out info))
            {
                info = await _client.Lookup(ip);
                _ipInfoCache.TryAdd(ip, info);
            }
            return info;
        }
    }
}
