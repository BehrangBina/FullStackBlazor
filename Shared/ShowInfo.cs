namespace Shared
{
    public record ShowInfo(
        string MovieTitle,
        DateTimeOffset StartsAt,
        int TotalSeats,
        int AvailableSeats,
        string Version // optimistic concurrency token
    );
}
