using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Shared;
using System.Net.Http.Json;
using WebApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5141/") // matches API HTTP listener
});

builder.Services.AddScoped<IApiClient, ApiClient>();

await builder.Build().RunAsync();

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
    {
        var resp = await _http.PostAsJsonAsync("api/hold", req, ct);
        return (await resp.Content.ReadFromJsonAsync<BookingResult>(cancellationToken: ct))!;
    }

    public async Task<BookingResult> BookAsync(BookingRequest req, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/book", req, ct);
        return (await resp.Content.ReadFromJsonAsync<BookingResult>(cancellationToken: ct))!;
    }

    public async Task<BookingResult> ReleaseAsync(BookingRequest req, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/release", req, ct);
        return (await resp.Content.ReadFromJsonAsync<BookingResult>(cancellationToken: ct))!;
    }
}
