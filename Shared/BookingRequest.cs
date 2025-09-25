namespace Shared
{
    public record BookingRequest(
        string CustomerId,
        int[] SeatNumbers,
        string? ExpectedVersion // client sends last seen version for concurrency safety
    );
}
