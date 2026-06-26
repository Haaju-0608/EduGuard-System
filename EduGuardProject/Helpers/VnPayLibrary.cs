using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

public class VnPayLibrary
{
    private readonly SortedDictionary<string, string> _requestData
        = new SortedDictionary<string, string>(StringComparer.Ordinal);

    private readonly SortedDictionary<string, string> _responseData
        = new SortedDictionary<string, string>(StringComparer.Ordinal);

    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
            _requestData[key] = value;
    }

    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
            _responseData[key] = value;
    }

    public string GetResponseData(string key)
        => _responseData.TryGetValue(key, out var v) ? v : string.Empty;

    // Build a VNPay 2.1.0 payment URL with a signed query string.
    public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
    {
        var data = new StringBuilder();

        foreach (var kv in _requestData)
        {
            if (!string.IsNullOrEmpty(kv.Value))
            {
                data.Append(kv.Key + "=" + kv.Value + "&");
            }
        }

        string signData = data.ToString().TrimEnd('&');

        string secureHash = HmacSha512(vnpHashSecret, signData);

        var query = new StringBuilder();

        foreach (var kv in _requestData)
        {
            if (!string.IsNullOrEmpty(kv.Value))
            {
                query.Append(Uri.EscapeDataString(kv.Key));
                query.Append("=");
                query.Append(Uri.EscapeDataString(kv.Value));
                query.Append("&");
            }
        }

        query.Append("vnp_SecureHash=");
        query.Append(secureHash);

        return baseUrl + "?" + query.ToString();
    }

    public bool ValidateSignature(string inputHash, string secretKey)
    {
        if (string.IsNullOrEmpty(inputHash) || string.IsNullOrEmpty(secretKey))
            return false;

        var data = new StringBuilder();
        foreach (var kv in _responseData)
        {
            if (kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
             || kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
                continue;

            data.Append(Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value) + "&");
        }

        if (data.Length == 0) return false;

        string rawData = data.ToString().Remove(data.Length - 1, 1);
        string myChecksum = HmacSha512(secretKey, rawData);

        return myChecksum.Equals(inputHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string HmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
