namespace Spike.Ambassador.Api.Helpers
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Application.V2;

    public static class SpikeAmbassadorHttpClient
    {
        public static HttpClient GetClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(ApplicationSettings.ConfigServiceBaseUrl)
            };

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }
    }
}