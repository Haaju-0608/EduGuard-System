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
        if (string.IsNullOrEmpty(vnpHashSecret))
            throw new ArgumentNullException(nameof(vnpHashSecret), "HashSecret rỗng!");

        var data = new StringBuilder();
        foreach (var kv in _requestData)
        {
            if (!string.IsNullOrEmpty(kv.Value))
            {
                data.Append(Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value) + "&");
            }
        }

        string queryString = data.ToString();
        if (queryString.Length > 0)
        {
            queryString = queryString.Remove(queryString.Length - 1, 1);
        }

        // 🔥 DÒNG LOG QUAN TRỌNG: In chuỗi nền ra Console để đối chiếu với mail VNPay
        Console.WriteLine("================ VNPAY DEBUG RAW DATA ================");
        Console.WriteLine(queryString);
        Console.WriteLine("======================================================");

        string secureHash = HmacSha512(vnpHashSecret, queryString);
        return baseUrl + "?" + queryString + "&vnp_SecureHash=" + secureHash;
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