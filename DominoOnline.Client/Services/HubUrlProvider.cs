using Microsoft.AspNetCore.Components;

namespace DominoOnline.Client.Services;

public class HubUrlProvider
{
    private readonly NavigationManager _navigation;

    public HubUrlProvider(NavigationManager navigation)
    {
        _navigation = navigation;
    }

    public string GetHubUrl()
    {
        // На продакшене сервер и клиент на одном домене
        // BaseUri: https://myapp.render.com/ → https://myapp.render.com/gamehub
        var baseUri = _navigation.BaseUri.TrimEnd('/');
        return $"{baseUri}/gamehub";
    }
}