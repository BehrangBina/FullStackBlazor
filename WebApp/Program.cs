using Shared;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebApp;

public interface IApiClient
{
    Task<ShowInfo> GetShowAsync(CancellationToken ct = default);
    Task<Seat[]> GetSeatsAsync(CancellationToken ct = default);
    Task<BookingResult> HoldAsync(BookingRequest req, CancellationToken ct = default);
    Task<BookingResult> BookAsync(BookingRequest req, CancellationToken ct = default);
    Task<BookingResult> ReleaseAsync(BookingRequest req, CancellationToken ct = default);
}

public class ApiClient : IApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    public Task<ShowInfo?> RawShowAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<ShowInfo>("api/show", ct)!;

    public async Task<ShowInfo> GetShowAsync(CancellationToken ct = default)
        => (await RawShowAsync(ct))!;

    public Task<Seat[]?> RawSeatsAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<Seat[]>("api/seats", ct)!;

    public async Task<Seat[]> GetSeatsAsync(CancellationToken ct = default)
        => (await RawSeatsAsync(ct))!;

    public async Task<BookingResult> HoldAsync(BookingRequest req, CancellationToken ct = default)
        => await SendBookingAsync("api/hold", req, ct);

    public async Task<BookingResult> BookAsync(BookingRequest req, CancellationToken ct = default)
        => await SendBookingAsync("api/book", req, ct);

    public async Task<BookingResult> ReleaseAsync(BookingRequest req, CancellationToken ct = default)
        => await SendBookingAsync("api/release", req, ct);

    private async Task<BookingResult> SendBookingAsync(string path, BookingRequest req, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(path, req, ct);
        var payload = await resp.Content.ReadFromJsonAsync<BookingResult>(cancellationToken: ct);

        if (payload is null)
            throw new ApiException((int)resp.StatusCode, "Unexpected empty response.", null);

        if (!resp.IsSuccessStatusCode)
            throw new ApiException((int)resp.StatusCode, payload.Message, payload);

        return payload;
    }
}

public sealed class ApiException : Exception
{
    public int StatusCode { get; }
    public BookingResult? Error { get; }

    public ApiException(int statusCode, string message, BookingResult? error)
        : base(message)
    {
        StatusCode = statusCode;
        Error = error;
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");

        builder.Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5141/")
        });

        builder.Services.AddScoped<IApiClient, ApiClient>();

        await builder.Build().RunAsync();
    }
}
