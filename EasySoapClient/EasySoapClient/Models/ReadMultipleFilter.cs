namespace EasySoapClient.Models;

/// <summary>
/// A filter for <c>ReadMultiple</c>, using NAV / Business Central filter syntax,
/// e.g. <c>new ReadMultipleFilter("Balance", "&lt;&gt;0")</c> or <c>new ReadMultipleFilter("No", "1000..2000")</c>.
/// Values are XML escaped by the library.
/// </summary>
/// <param name="Field">The element name of the field to filter on.</param>
/// <param name="Criteria">The filter expression.</param>
public readonly record struct ReadMultipleFilter(string Field, string Criteria);
