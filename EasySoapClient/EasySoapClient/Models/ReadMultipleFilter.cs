namespace EasySoapClient.Models;


public struct ReadMultipleFilter
{
    public string Field { get; set; }
    public string Criteria { get; set; }
    public string? BookmarkKey { get; set; }
    public int Size { get; set; }

    public ReadMultipleFilter(string field, string criteria, string bookmarkKey, int size)
    {
        Field = field;
        Criteria = criteria;
        BookmarkKey = bookmarkKey;
        Size = size;
    }

    public ReadMultipleFilter(string field, string criteria, int size)
    {
        Field = field;
        Criteria = criteria;
        BookmarkKey = String.Empty;
        Size = size;
    }

    public ReadMultipleFilter(string field, string criteria)
    {
        Field = field;
        Criteria = criteria;
        BookmarkKey = String.Empty;
        Size = 10;
    }
}
