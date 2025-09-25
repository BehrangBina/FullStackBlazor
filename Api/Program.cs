using Shared;
using System.Collections.Concurrent;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- In-memory data ---
var movieTitle = "The Sample Movie";
var startTime = DateTimeOffset.UtcNow.AddHours(2);
var seats = new ConcurrentDictionary<int, Seat>(
    Enumerable.Range(1, 20).Select(n => new KeyValuePair<int, Seat>(n, new Seat(n, SeatStatus.Available)))
);
string version = Guid.NewGuid().ToString("N"); // changes on any mutation
object gate = new(); // coarse-grained lock just for demo

ShowInfo CurrentShowInfo()
{
    var arr = seats.Values.ToArray();
    var available = arr.Count(s => s.Status == SeatStatus.Available);
    return new ShowInfo(movieTitle, startTime, seats.Count, available, version);
}

Seat[] SnapshotSeats() => seats.Values.OrderBy(s => s.Number).ToArray();

void BumpVersion() => version = Guid.NewGuid().ToString("N");

// --- Endpoints ---

// health
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

// show info
app.MapGet("/api/show", () => Results.Ok(CurrentShowInfo()));

// seats snapshot
app.MapGet("/api/seats", () => Results.Ok(SnapshotSeats()));

// hold seats (optional pre-step; we’ll book directly too)
app.MapPost("/api/hold", (BookingRequest req) =>
{
    lock (gate)
    {
        if (!string.IsNullOrWhiteSpace(req.ExpectedVersion) && req.ExpectedVersion != version)
            return Results.Conflict(new BookingResult(false, "Version mismatch. Refresh seats.", version, SnapshotSeats()));

        foreach (var n in req.SeatNumbers.Distinct())
        {
            if (!seats.TryGetValue(n, out var seat))
                return Results.BadRequest(new BookingResult(false, $"Seat {n} does not exist.", version, SnapshotSeats()));
            if (seat.Status != SeatStatus.Available)
                return Results.Conflict(new BookingResult(false, $"Seat {n} not available.", version, SnapshotSeats()));
        }

        foreach (var n in req.SeatNumbers.Distinct())
        {
            var s = seats[n];
            seats[n] = s with { Status = SeatStatus.Held, HeldBy = req.CustomerId };
        }
        BumpVersion();
        return Results.Ok(new BookingResult(true, "Seats held.", version, SnapshotSeats()));
    }
});

// book seats
app.MapPost("/api/book", (BookingRequest req) =>
{
    lock (gate)
    {
        if (!string.IsNullOrWhiteSpace(req.ExpectedVersion) && req.ExpectedVersion != version)
            return Results.Conflict(new BookingResult(false, "Version mismatch. Refresh seats.", version, SnapshotSeats()));

        foreach (var n in req.SeatNumbers.Distinct())
        {
            if (!seats.TryGetValue(n, out var seat))
                return Results.BadRequest(new BookingResult(false, $"Seat {n} does not exist.", version, SnapshotSeats()));
            // allow booking if Available or Held by same user
            var ok = seat.Status == SeatStatus.Available
                     || (seat.Status == SeatStatus.Held && seat.HeldBy == req.CustomerId);
            if (!ok)
                return Results.Conflict(new BookingResult(false, $"Seat {n} not available for booking.", version, SnapshotSeats()));
        }

        foreach (var n in req.SeatNumbers.Distinct())
        {
            var s = seats[n];
            seats[n] = s with { Status = SeatStatus.Booked, HeldBy = null };
        }
        BumpVersion();
        return Results.Ok(new BookingResult(true, "Booking confirmed.", version, SnapshotSeats()));
    }
});

// release holds (optional)
app.MapPost("/api/release", (BookingRequest req) =>
{
    lock (gate)
    {
        foreach (var n in req.SeatNumbers.Distinct())
        {
            if (!seats.TryGetValue(n, out var seat)) continue;
            if (seat.Status == SeatStatus.Held && seat.HeldBy == req.CustomerId)
                seats[n] = seat with { Status = SeatStatus.Available, HeldBy = null };
        }
        BumpVersion();
        return Results.Ok(new BookingResult(true, "Holds released.", version, SnapshotSeats()));
    }
});

app.Run();
public partial class Program { }
