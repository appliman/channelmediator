using ChannelMediator;

namespace BlazorAppSample.Handlers;

public record GetWeatherRequest
    : IRequest<List<WeatherForecast>>;
