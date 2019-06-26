// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.BotBuilderSamples
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureLogging((logging) =>
                {
                    // Providing an instrumentation key here is required if you're using
                    // standalone package Microsoft.Extensions.Logging.ApplicationInsights
                    // or if you want to capture logs from early in the application startup
                    // pipeline from Startup.cs or Program.cs itself.
                    logging.AddApplicationInsights("ikey");

                    // Optional: Apply filters to control what logs are sent to Application Insights.
                    // The following configures LogLevel Information or above to be sent to
                    // Application Insights for all categories.
                    logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>
                                     ("", LogLevel.Information);
                })
                .UseStartup<Startup>();
    }
}
