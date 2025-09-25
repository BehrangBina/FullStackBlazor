namespace Shared
{
    public record Seat(
        int Number,
        SeatStatus Status,
        string? HeldBy = null
    );
}
