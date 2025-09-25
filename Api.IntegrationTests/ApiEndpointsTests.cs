using Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Net;


namespace Api.IntegrationTests
{
    public class ApiEndpointsTests
    {
        [Fact]
        public async Task Health_returns_ok()
        {
            using var app = new WebApplicationFactory<Program>();
            var client = app.CreateClient();

            var resp = await client.GetAsync("/api/health");
            Assert.True(resp.IsSuccessStatusCode);

            var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            Assert.Equal("ok", body!["status"]!.ToString());
        }

        [Fact]
        public async Task Show_and_seats_initial_state()
        {
            using var app = new WebApplicationFactory<Program>();
            var client = app.CreateClient();

            var show = await client.GetFromJsonAsync<ShowInfo>("/api/show");
            Assert.NotNull(show);
            Assert.Equal(20, show!.TotalSeats);

            var seats = await client.GetFromJsonAsync<Seat[]>("/api/seats");
            Assert.NotNull(seats);
            Assert.Equal(20, seats!.Length);
            Assert.All(seats!, s => Assert.Equal(SeatStatus.Available, s.Status));
        }

        [Fact]
        public async Task Hold_then_book_flow_with_concurrency_checks()
        {
            using var app = new WebApplicationFactory<Program>();
            var client = app.CreateClient();

            var show = await client.GetFromJsonAsync<ShowInfo>("/api/show");
            var initialVersion = show!.Version;

            // Hold seats 1,2,3
            var holdReq = new BookingRequest("user1", new[] { 1, 2, 3 }, initialVersion);
            var holdResp = await client.PostAsJsonAsync("/api/hold", holdReq);
            Assert.True(holdResp.IsSuccessStatusCode);
            var hold = await holdResp.Content.ReadFromJsonAsync<BookingResult>();
            Assert.True(hold!.Success);

            // Try to book with stale version -> Conflict
            var staleBookResp = await client.PostAsJsonAsync("/api/book", holdReq with { ExpectedVersion = initialVersion });
            Assert.Equal(HttpStatusCode.Conflict, staleBookResp.StatusCode);

            // Book with the latest version -> OK
            var bookResp = await client.PostAsJsonAsync("/api/book", holdReq with { ExpectedVersion = hold.NewVersion });
            Assert.True(bookResp.IsSuccessStatusCode);
            var booked = await bookResp.Content.ReadFromJsonAsync<BookingResult>();
            Assert.True(booked!.Success);

            var affected = booked.SeatsSnapshot.Where(s => new[] { 1, 2, 3 }.Contains(s.Number));
            Assert.All(affected, s => Assert.Equal(SeatStatus.Booked, s.Status));
        }

        [Fact]
        public async Task Booking_conflict_when_seat_taken()
        {
            using var app = new WebApplicationFactory<Program>();
            var client = app.CreateClient();

            var show = await client.GetFromJsonAsync<ShowInfo>("/api/show");

            // Book seat 4 by A
            var bookA = await client.PostAsJsonAsync("/api/book", new BookingRequest("A", new[] { 4 }, show!.Version));
            var resA = await bookA.Content.ReadFromJsonAsync<BookingResult>();
            Assert.True(resA!.Success);

            // B tries to hold the same seat -> Conflict
            var holdB = await client.PostAsJsonAsync("/api/hold", new BookingRequest("B", new[] { 4 }, resA.NewVersion));
            Assert.Equal(HttpStatusCode.Conflict, holdB.StatusCode);
        }

        [Fact]
        public async Task Release_hold_makes_seats_available_again()
        {
            using var app = new WebApplicationFactory<Program>();
            var client = app.CreateClient();

            var show = await client.GetFromJsonAsync<ShowInfo>("/api/show");

            // Hold 5,6
            var holdResp = await client.PostAsJsonAsync("/api/hold", new BookingRequest("R", new[] { 5, 6 }, show!.Version));
            var hold = await holdResp.Content.ReadFromJsonAsync<BookingResult>();
            Assert.True(hold!.Success);

            // Release
            var releaseResp = await client.PostAsJsonAsync("/api/release", new BookingRequest("R", new[] { 5, 6 }, hold.NewVersion));
            var release = await releaseResp.Content.ReadFromJsonAsync<BookingResult>();
            Assert.True(release!.Success);

            var seats = release.SeatsSnapshot.Where(s => s.Number is 5 or 6).ToArray();
            Assert.All(seats, s => Assert.Equal(SeatStatus.Available, s.Status));
        }

        [Fact]
        public async Task Invalid_seat_returns_bad_request()
        {
            using var app = new WebApplicationFactory<Program>();
            var client = app.CreateClient();

            var show = await client.GetFromJsonAsync<ShowInfo>("/api/show");
            var resp = await client.PostAsJsonAsync("/api/book", new BookingRequest("X", new[] { 999 }, show!.Version));
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }
    }
}
