// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Bot.Builder.ApplicationInsights;
using Microsoft.Bot.Builder.Integration.ApplicationInsights.Core;
using Microsoft.BotBuilderSamples.Bots;
using Microsoft.BotBuilderSamples.Dialogs;
using System.Collections.Concurrent;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Builder.Azure;


namespace Microsoft.BotBuilderSamples
{
    public class Startup
    {
        private const string BotOpenIdMetadataKey = "BotOpenIdMetadata";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add Application Insights services into service collection
            services.AddApplicationInsightsTelemetry();

            // Add the standard telemetry client
            services.AddSingleton<IBotTelemetryClient, BotTelemetryClient>();

            // Add ASP middleware to store the HTTP body, mapped with bot activity key, in the httpcontext.items
            // This will be picked by the TelemetryBotIdInitializer
            services.AddTransient<TelemetrySaveBodyASPMiddleware>();

            // Add telemetry initializer that will set the correlation context for all telemetry items
            services.AddSingleton<ITelemetryInitializer, OperationCorrelationTelemetryInitializer>();

            services.AddSingleton<ICredentialProvider, ConfigurationCredentialProvider>();

            // Add telemetry initializer that sets the user ID and session ID (in addition to other 
            // bot-specific properties, such as activity ID)
            services.AddSingleton<ITelemetryInitializer, TelemetryBotIdInitializer>();

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            // Create the Bot Framework Adapter with error handling enabled.
            services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

            // Create the telemetry middleware to track conversation events
            services.AddSingleton<IMiddleware, TelemetryLoggerMiddleware>();


            var dataStore = new AzureBlobStorage(Configuration["BotStateStorageAccount"], Configuration["BotStateContainer"]);

            // Create Conversation State object.
            // The Conversation State object is where we persist anything at the conversation-scope.
            var conversationState = new ConversationState(dataStore);

            // Register conversation state.
            services.AddSingleton<ConversationState>(conversationState);
            services.AddSingleton<IStorage>(dataStore);
            //services.AddSingleton<IStorage, MemoryStorage>();

            // Create the User state. (Used in this bot's Dialog implementation.)
            services.AddSingleton<UserState>();

            // Create the Conversation state. (Used by the Dialog system itself.)
            //services.AddSingleton<ConversationState>();

            services.AddSingleton<ConcurrentDictionary<string, ConversationReference>>();

            // The Dialog that will be run by the bot.
            services.AddSingleton<MainDialog>();

            // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
            services.AddTransient<IBot, DialogBot<MainDialog>>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseBotApplicationInsights();

            app.UseMvc();
        }
    }
}
