namespace Spike.Ambassador.Application.V2
{
    using System;
    using Microsoft.Extensions.Configuration;

    public static class ApplicationSettings
    {
        public static string ApplicationBaseUrl { get; }
        public static string ConfigServiceBaseUrl { get; }

        static ApplicationSettings()
        {
            var settingsResolver = GetSettingsResolver();
            ApplicationBaseUrl = settingsResolver("Spike.Ambassador.Application.BaseUrl");
            ConfigServiceBaseUrl = settingsResolver("Spike.Ambassador.ConfigService.BaseUrl");
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