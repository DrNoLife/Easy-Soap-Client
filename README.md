# Easy-Soap-Client
Project for talking with SOAP web services, without using a Visual Studio auto-generated proxy

## NOTICE! 

As of version 2.0, some breaking changes has happened.

I decided to move away from the previous approach, of creating a repository for a specific model. I realized this was very limiting (worked fine, in the start when I was just retrieving data, but as more action became needed, it was more and more limiting).

Reason being, the previous approach forced you to create a new repository for reading. A new repository for creating. A new repository for updating... All regarding the same endpoint.

In the new version you still need to create models which implement the ```IWebServiceElement``` and now also the ```IUpdatableWebServiceElement``` if you want to use the update method. However, you only need to inject the new service, and use the provided methods. No need to initialize an entire new object just for calling that specific element.

The rest of the documentation has also been updated. If you need the old version, see the commit history on the GitHub repository.


## How to use

Setup the library by adding it to the ```IServiceCollection``` by doing the following:

```csharp
builder.Services.AddEasySoapClient(options =>
{
    options.Username = builder.Configuration["Navision:Username"] 
        ?? throw new Exception("Failed to get the 'Username' for Navision from appsettings.json.");

    options.Password = builder.Configuration["Navision:Password"] 
        ?? throw new Exception("Failed to get the 'Password' for Navision from appsettings.json.");

    options.BaseUri = builder.Configuration["Navision:BaseUri"] 
        ?? throw new Exception("Failed to get the 'BaseUri' for Navision from appsettings.json.");
});
```

*Note: Since 2.0, there is no need to manually add the HttpClient, as the libraary also adds this.*

### BaseUri

You need to provide a ```BaseUri```.  
You might have a webservice which is located at the following Url ```https://<domain>/WEBSERVICE/WS/<company>/Pages/WebServiceNameHere```  
What we need from this is this piece: ```https://<domain>/WEBSERVICE/WS/<company>``` 


## Create custom models 

This project works by creating models which defines namespace, service name, and describes which properties to get data from, from the web service.

Create a class and implement ```IWebServiceElement```, and afterwards use ```[XmlElement]``` to target a Web service property.

Example:

```csharp
public class MachineModel : IWebServiceElement
{
    public string ServiceName => "Machines";
    public string Namespace => "urn:microsoft-dynamics-schemas/page/machines";


    [XmlElement(ElementName = "Key")]
    public string Key { get; set; } = String.Empty;

    [XmlElement(ElementName = "Machine_Name")]
    public string Machine { get; set; } = String.Empty;

    public override string ToString()
    {
        return $"{Machine} [{Key}]";
    }
}
```

This newly build class, is what is used as type parameter for your repositories.

## Filter

Filter the data you get by using the ```ReadMultipleFilter``` struct. Example:

```csharp
ReadMultipleFilter filter = new("ObjectId", "> 100");
```

Which should filter by webservice property ObjectId.

*Note: Filters only specify by what parameters we want to retrieve data, not how much data is retrieved. That is done later when calling the method.*

## Complete code example:

Following code example is a rough and dirty way of retrieving an element, updating it, and then retrieving the element again to see that it has indeed been updated.

Program.cs
```csharp
builder.Services.AddEasySoapClient(options =>
{
    options.Username = builder.Configuration["Navision:Username"] 
        ?? throw new Exception("Failed to get the 'Username' for Navision from appsettings.json.");

    options.Password = builder.Configuration["Navision:Password"] 
        ?? throw new Exception("Failed to get the 'Password' for Navision from appsettings.json.");

    options.BaseUri = builder.Configuration["Navision:BaseUri"] 
        ?? throw new Exception("Failed to get the 'BaseUri' for Navision from appsettings.json.");
});
```

Models/NavisionDocumentModel.cs
```csharp
public class NavisionDocumentModel : IWebServiceElement
{
    public string ServiceName => "ItemDocuments";
    public string Namespace => "urn:microsoft-dynamics-schemas/page/itemdocuments";

    [XmlElement(ElementName = "Key")]
    public string Key { get; set; } = String.Empty;

    [XmlElement(ElementName = "Tabel_ID")]
    public string TableId { get; set; } = String.Empty;

    [XmlElement(ElementName = "IsControlled")]
    public bool IsControlled { get; set; }
}
```

Models/UpdateDocumentModel
```csharp
public class UpdateDocumentModel : IUpdatableWebServiceElement
{
    public string ServiceName => "ItemDocuments";
    public string Namespace => "urn:microsoft-dynamics-schemas/page/itemdocuments";

    [XmlElement(ElementName = "Key")]
    public string Key { get; set; } = String.Empty;

    [XmlElement(ElementName = "IsControlled")]
    public bool IsControlled { get; set; }
}
```

*Note: The key here is used by Navision as the Primary Key, to decide what element to update. The library generates a soap envelope based on the [XmlElement] properties you add to the class.*

Worker.cs
```csharp
public class Worker(ILogger<Worker> logger, IEasySoapService soapService) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private readonly IEasySoapService _soapService = soapService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ReadMultipleFilter filter = new("Tabel_ID", "27");

        var beforeEdit = await _soapService.GetAsync<NavisionDocumentModel>([filter], 1);
        _logger.LogInformation("{DocumentKey} - has been controlled {IsControlled}", beforeEdit.First().Key, beforeEdit.First().IsControlled);

        // Edit.
        UpdateDocumentModel item = new()
        {
            Key = beforeEdit.First().Key,
            IsControlled = !beforeEdit.First().IsControlled,
        };
        _ = await _soapService.UpdateAsync(item);

        var afterEdit = await _soapService.GetAsync<NavisionDocumentModel>([filter], 1);
        _logger.LogInformation("{DocumentKey} - has been controlled {IsControlled}", afterEdit.First().Key, afterEdit.First().IsControlled);
    }
}
```