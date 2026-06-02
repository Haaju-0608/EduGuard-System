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

    // ==================== TẠO URL THANH TOÁN CHUẨN VNPAY 2.1.0 ====================
    public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
    {
        var query = new StringBuilder();
        var signData = new StringBuilder();

        foreach (var kv in _requestData)
        {
            if (string.IsNullOrEmpty(kv.Value))
                continue;

            var encodedKey = Uri.EscapeDataString(kv.Key);
            var encodedValue = Uri.EscapeDataString(kv.Value);

            if (query.Length > 0)
            {
                query.Append("&");
                signData.Append("&");
            }

            // Query string
            query.Append(encodedKey);
            query.Append("=");
            query.Append(encodedValue);

            // Data dùng để ký
            signData.Append(kv.Key);
            signData.Append("=");
            signData.Append(encodedValue);
        }

        string secureHash = HmacSha512(vnpHashSecret, signData.ToString());

        query.Append("&vnp_SecureHash=");
        query.Append(secureHash);

        return $"{baseUrl}?{query}";
    }

    public bool ValidateSignature(string inputHash, string secretKey)
    {
        if (string.IsNullOrEmpty(inputHash) || string.IsNullOrEmpty(secretKey))
            return false;

        var data = _responseData
            .Where(kv =>
                !kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase) &&
                !kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv =>
                $"{kv.Key}={Uri.EscapeDataString(kv.Value)}");

        string rawData = string.Join("&", data);

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