namespace EasySoapClient.Delegates;

public delegate T MaybeKeyedServiceResolver<T>(string? serviceKey);
