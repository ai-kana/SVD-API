using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;

namespace SVDAPI;

public class TokenInvalidException : Exception;

public class SVDClient
{
    private string? _Token;
    private readonly string _User;
    private readonly string _Key;
    private readonly string _EndPoint;
    internal SVDClient(string user, string key, string endPoint)
    {
        _User = user;
        _Key = key;
        _EndPoint = endPoint;
        _Token = null;
    }

    private string AuthEndpoint => $"{_EndPoint}/authentication";
    /// <summary>
    /// Update auth token
    /// </summary>
    public async Task UpdateTokenAsync()
    {
        using HttpClient client = new();
        string url = $"{AuthEndpoint}?username={_User}&key={_Key}";
        using HttpResponseMessage response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());

        JToken jsonToken = json["token"] ?? throw new("Failed to read token from response");
        _Token = jsonToken.Value<string>() ?? throw new("Failed to convert read token to string");
    }

    private AuthenticationHeaderValue GetHeader()
    {
        return new("Bearer", _Token);
    }

    private string DecodeEndpoint => $"{_EndPoint}/decoder";

    /// <summary>
    /// Turn Steam audio data to .wav
    /// </summary>
    /// <exception cref="TokenInvalidException">Invalid token, call UpdateTokenAsync()</exception>
    /// <exception cref="HttpRequestException">Failed to make request</exception>
    public async Task<byte[]> DecodeAsync(IEnumerable<IEnumerable<byte>> voiceData)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Authorization = GetHeader();
        string requestData = JsonConvert.SerializeObject(new {source = voiceData});

        using StringContent content = new(requestData, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(DecodeEndpoint, content);
        AssertValidResponse(response);

        byte[] audio = await response.Content.ReadAsByteArrayAsync();
        return audio;
    }

    private string EncodeEndpoint => $"{_EndPoint}/encoder";

    /// <summary>
    /// Turn .wav data to steam audio
    /// </summary>
    /// <exception cref="TokenInvalidException">Invalid token, call UpdateTokenAsync()</exception>
    /// <exception cref="HttpRequestException">Failed to make request</exception>
    /// <exception cref="JsonSerializationException">Invalid response, unlikely to ever happen</exception>
    public async Task<IEnumerable<byte[]>> EncodeAsync(CSteamID steamid, float[] wavData)
    {
        object request = new
        {
            WavData = wavData,
            SteamID = steamid.m_SteamID
        };
        using StringContent content = new(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

        using HttpClient client = new();
        client.DefaultRequestHeaders.Authorization = GetHeader();
        using HttpResponseMessage response = await client.PostAsync(EncodeEndpoint, content);
        AssertValidResponse(response);

        string data = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<IEnumerable<byte[]>>(data) ?? throw new JsonSerializationException("Failed to deserialize encode data");
    }

    /// <summary>
    /// Turn .wav data from file to steam audio
    /// </summary>
    /// <exception cref="TokenInvalidException">Invalid token, call UpdateTokenAsync()</exception>
    /// <exception cref="HttpRequestException">Failed to make request</exception>
    /// <exception cref="JsonSerializationException">Invalid response, unlikely to ever happen</exception>
    public async Task<IEnumerable<byte[]>> EncodeAsync(CSteamID steamId, string wavPath)
    {
        float[] wavData = WavReader.ReadWavFile(Path.Combine(Environment.CurrentDirectory, wavPath));
        return await EncodeAsync(steamId, wavData);
    }

    private void AssertValidResponse(HttpResponseMessage message)
    {
        if (message.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new TokenInvalidException();
        }

        message.EnsureSuccessStatusCode();
    }
}
