using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Server._BlackM.ChatImage;

public static class ChatImageProcessor
{
    public const int MaxDimension = 320;  
    public const int MaxFrames = 60;      
    public const int MaxSourceSide = 4096;
    public const int MaxTotalBytes = 2_500_000;

    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public sealed class Result
    {
        public List<byte[]> Frames = new();
        public int[] DelaysMs = Array.Empty<int>();
        public int Width;
        public int Height;
    }

    public static async Task<Result> LoadAsync(Uri uri, int maxBytes, CancellationToken ct)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
        }
        catch (SocketException)
        {
            throw new ChatImageException("cmd-sendimg-error-dns", ("host", uri.Host));
        }

        if (addresses.Length == 0 || addresses.Any(IsPrivate))
            throw new ChatImageException("cmd-sendimg-error-private-address", ("host", uri.Host));

        var data = await DownloadAsync(uri, maxBytes, ct);

        var format = Image.DetectFormat(data);
        if (format == null || format.Name is not ("PNG" or "JPEG" or "GIF" or "WEBP"))
            throw new ChatImageException("cmd-sendimg-error-bad-format");

        var info = Image.Identify(data);
        if (info.Width > MaxSourceSide || info.Height > MaxSourceSide)
            throw new ChatImageException("cmd-sendimg-error-too-big");

        using var image = Image.Load<Rgba32>(data);

        var scale = Math.Min(1.0, (double) MaxDimension / Math.Max(image.Width, image.Height));
        var width = Math.Max(1, (int) (image.Width * scale));
        var height = Math.Max(1, (int) (image.Height * scale));

        var count = Math.Min(image.Frames.Count, MaxFrames);
        var result = new Result { DelaysMs = new int[count], Width = width, Height = height };
        long total = 0;

        for (var i = 0; i < count; i++)
        {
            using var frame = image.Frames.CloneFrame(i);

            if (scale < 1.0)
                frame.Mutate(x => x.Resize(width, height));

            using var ms = new MemoryStream();
            frame.SaveAsPng(ms);

            var bytes = ms.ToArray();
            total += bytes.Length;
            if (total > MaxTotalBytes)
                throw new ChatImageException("cmd-sendimg-error-too-big");

            result.Frames.Add(bytes);

            var delay = image.Frames[i].Metadata.GetGifMetadata().FrameDelay * 10;
            result.DelaysMs[i] = delay <= 10 ? 100 : delay;
        }

        return result;
    }

    private static async Task<byte[]> DownloadAsync(Uri uri, int maxBytes, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("BlackM-ChatImage/1.0");

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        var code = (int) response.StatusCode;
        if (code is >= 300 and < 400)
            throw new ChatImageException("cmd-sendimg-error-redirect");
        if (!response.IsSuccessStatusCode)
            throw new ChatImageException("cmd-sendimg-error-http", ("code", code));

        var length = response.Content.Headers.ContentLength;
        if (length != null && length > maxBytes)
            throw new ChatImageException("cmd-sendimg-error-too-big");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buffer = new byte[16384];

        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (ms.Length + read > maxBytes)
                throw new ChatImageException("cmd-sendimg-error-too-big");

            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal
                   || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.IPv6None);
        }

        var b = ip.GetAddressBytes();
        return b[0] == 0
               || b[0] == 10
               || b[0] == 127
               || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
               || (b[0] == 169 && b[1] == 254)
               || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
               || (b[0] == 192 && b[1] == 168);
    }
}