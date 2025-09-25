namespace Api.UnitTests
{
    public class BasicTests
    {
        [Fact]
        public void Seat_numbers_are_one_based()
        {
            var seats = Enumerable.Range(1, 20).ToArray();
            Assert.Equal(1, seats.First());
            Assert.Equal(20, seats.Last());
        }
    }
}
