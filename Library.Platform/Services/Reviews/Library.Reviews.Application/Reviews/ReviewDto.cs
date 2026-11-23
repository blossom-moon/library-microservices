namespace Library.Reviews.Application.Reviews;

public sealed class ReviewDto
{
    public string Id { get; set; } = "";
    public long BookId { get; set; }
    public string BookTitle { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public int Rating { get; set; }
    public string Text { get; set; } = "";
    public int CommentsCount { get; set; }
    public int Version { get; set; }
}
