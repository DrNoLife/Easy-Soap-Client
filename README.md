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

*Note: Since version 2.3.0 the ```Namespace``` property has no use, and as such can be removed from any classes.*

Models/NavisionDocumentModel.cs
```csharp
public class NavisionDocumentModel : IWebServiceElement
{
    public string ServiceName => "ItemDocuments";

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

## Keyed Services

It is now possible to use keyed services, to make sure one can work with multiple navision instances.

You can register it as a keyed instance using the new ```AddKeyedEasySoapClient``` method.

```csharp
builder.Services.AddKeyedEasySoapClient("CompanyNameA", options =>
{
    options.Username = builder.Configuration["Navision:CompanyNameA:Username"]!;
    options.Password = builder.Configuration["Navision:CompanyNameA:Password"]!;
    options.BaseUri = builder.Configuration["Navision:CompanyNameA:WebServiceLink"]!;
});

builder.Services.AddKeyedEasySoapClient("CompanyNameB", options =>
{
    options.Username = builder.Configuration["Navision:CompanyNameB:Username"]!;
    options.Password = builder.Configuration["Navision:CompanyNameB:Password"]!;
    options.BaseUri = builder.Configuration["Navision:CompanyNameB:WebServiceLink"]!;
});
```

After that, you can handle the dependency injection like this:

```csharp
public class Worker(
    [FromKeyedServices("CompanyNameA")] IEasySoapService easySoapCompanyA,
    [FromKeyedServices("CompanyNameB")] IEasySoapService easySoapCompanyB) 
{

}
```

The library supports using either Keyed services, or non-keyed services. You can also mix and match the two in a project.

## Codeunits

As of version 2.2.0 one can now call code units using the library.

To do so, construct a ```CodeUnitRequest``` and then call the new ```CallCodeUnitAsync``` method on the ```IEasySoapService``` interface.

One can create the ```CodeUnitRequest``` either using a  constructor approach, or using the provided ```CodeUnitRequestBuilder```.

```csharp
CodeUnitRequest request = CodeUnitRequest.CreateRequest(
    "CodeUnitName",
    "NameOfFunctionToBeInvoked",
    new CodeUnitParameter("ParameterName", "ParameterValue"));
```

```csharp
CodeUnitRequest request = CodeUnitRequestBuilder
    .WithCodeUnit("CodeUnitName")
    .WithMethod("NameOfFunctionToBeInvoked")
    .AddParameter("ParameterName", "ParameterValue")
    .Build();
```

Then call it like this.

```csharp
CodeUnitResponse response = await _easySoapService.CallCodeUnitAsync(request, stoppingToken);
```

## Configure the HttpClient
As of version 2.3.0 the used HttpClient is configurable, and different from the default http client used in the rest of the application.

```csharp
builder.Services.AddEasySoapClient(
    options =>
    {
        options.Username = builder.Configuration["Navision:Company:Username"]
            ?? throw new Exception("Failed to get the 'Username' for Navision from appsettings.json.");

        options.Password = builder.Configuration["Navision:Company:Password"]
            ?? throw new Exception("Failed to get the 'Password' for Navision from appsettings.json.");

        options.BaseUri = builder.Configuration["Navision:Company:WebServiceLink"]
            ?? throw new Exception("Failed to get the 'BaseUri' for Navision from appsettings.json.");
    }, 
    http =>
    {
        http.ConfigureHttpClient((provider, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    });
```

Both the ```AddEasySoapClient``` and ```AddKeyedEasySoapClient``` has been extended to now include the following parameter: ```Action<IHttpClientBuilder>? configureHttpClientBuilder = null```. Meaning you can now use the ```IHttpClientBuilder``` to configure everything regarding the HttpClient.

*Note: This also includes the BaseUri. However, I'd not recommend to do so, as the library uses it to figure out where to send requests, based on the ```options.BaseUri``` and ```IWebServiceElement.ServiceName```.*

The idea is to allow the user to combine it with e.g. Polly to add resilience to the requests, if need be.

## Get Id from Key

As of version 2.3.0 you can now get the internal Id of an item, based on the key, using the new method ```GetIdFromKeyAsync<T>(string key, bool longResult = false, CancellationToken cancellationToken = default)```.

In order to use it, ```T``` must implement the ```ISearchable``` interface.

```csharp
public class TestNotification : IWebServiceElement, ISearchable
{
    public string ServiceName => "NotificationEntries";

    [XmlElement(ElementName = "Key")]
    public string Key { get; set; } = String.Empty;
}
```

*Note: ISearchable already implements the IWebServiceElement, meaning you could leave out that part. I let it stay in for clarity.*

With that, you can now use it like this:
```csharp
var result = await _easySoapService.GetIdFromKeyAsync<TestNotification>("12;hsMAAACHBA==9;6075120090;", cancellationToken: stoppingToken);
```

This would (in my case) result in the value ```"4"```. 

Navision returns the following syntax: *Mail Notification Entry: 4*. If you need the full sentence (and not just the value "4"), you can use the boolean argument ```longResult``` on the method. Setting it to true, returns the full result.

## Get a single specific item

As of version 2.4.0 a new method exists: ```public async Task<T> GetItemAsync<T>(ReadRequest request, CancellationToken cancellationToken = default) where T : ISearchable, new()```.

This allows the you to provide a ```ReadRequest```, and based on that return a single specific item that matches the request. Basically, we are implementing the ```Read``` endpoint for web services.

Usage, very simple as usual:

```csharp
_ = await _easySoapService.GetItemAsync<NavisionSalesOrderList>(readRequest, cancellationToken: stoppingToken);
```

In order to create a ```ReadRequest```, you can either construct it manually, or use the new builder. Here are examples of both scenarios:

```csharp
var readRequestWithBuilder1 = ReadRequestBuilder.Single("ITEM-NUMBER-HERE"); // Key defaults to "No".
var readRequestWithBuilder2 = ReadRequestBuilder.Single("ITEM-ID-HERE", "Id");
var readRequestWithBuilder3 = ReadRequestBuilder
    .New()
    .With("No", "ITEM-NUMBER-HERE")
    .Build(); // Build is optional, but does make it more clear.
var readRequestWithBuilder4 = ReadRequestBuilder
    .New()
    .With(("Line_No", 111222333), ("Item_No", "ITEM-NUMBER-HERE"));

var readRequestWithoutBuilder1 = new ReadRequest("Line_No", 111222333);
var readRequestWithoutBuilder2 = new ReadRequest(("ProductOrder", "PO-HBA-77283"), ("BatchNumber", 39923));
var readRequestWithoutBuilder3 = new ReadRequest(
    ("ProductOrder", "PO-HBA-77283"),
    ("BatchNumber", 39923),
    ("LineNumber", 71112));
```