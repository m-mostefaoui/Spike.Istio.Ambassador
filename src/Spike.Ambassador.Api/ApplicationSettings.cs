namespace Spike.Ambassador.Api
{
    using System;
    using Microsoft.Extensions.Configuration;

    public static class ApplicationSettings
    {
        public static string ApiBaseUrl { get; }
        public static string ApplicationBaseUrl { get; }
        
        static ApplicationSettings()
        {
            var settingsResolver = GetSettingsResolver();
            ApiBaseUrl = settingsResolver("Spike.Ambassador.Api.BaseUrl");
            ApplicationBaseUrl = settingsResolver("Spike.Ambassador.Application.BaseUrl");
        }

        private static Func<string, string> GetSettingsResolver()
        {
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json");
            var configuration = configurationBuilder.Build();

            return (name) => configuration.GetSection(name).Value;
        }
    }
}