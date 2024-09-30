namespace EasySoapClient.Models;


public struct ReadMultipleFilter(string field, string criteria)
{
    public string Field { get; set; } = field;
    public string Criteria { get; set; } = criteria;
}

