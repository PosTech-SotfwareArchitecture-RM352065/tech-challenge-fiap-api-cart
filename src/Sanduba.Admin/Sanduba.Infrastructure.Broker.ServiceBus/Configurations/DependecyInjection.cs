﻿using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sanduba.Core.Application.Abstraction.Customers;
using Sanduba.Core.Application.Abstraction.Customers.Events;
using Sanduba.Core.Application.Abstraction.Orders;
using Sanduba.Core.Application.Abstraction.Orders.Events;
using Sanduba.Infrastructure.Broker.ServiceBus.Customers;
using Sanduba.Infrastructure.Broker.ServiceBus.Orders;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Sanduba.Infrastructure.Broker.ServiceBus.Configurations
{
    [ExcludeFromCodeCoverage]
    public static class DependencyInjection
    {
        /// <summary>
        /// Registers the necessary services with the DI framework.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The same service collection.</returns>
        public static IServiceCollection AddServiceBusInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var entryAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            services.AddMassTransit(options =>
            {
                options.AddConsumer<CustomerNotificationBroker>();

                options.UsingAzureServiceBus((context, config) =>
                {
                    config.Host(configuration["CustomerBrokerSettings:ConnectionStrings"]);

                    config.SubscriptionEndpoint(
                        configuration["CustomerBrokerSettings:SubscriptionName"],
                        configuration["CustomerBrokerSettings:TopicName"],
                        e =>
                        {
                            e.UseMessageRetry(r => r.Interval(2, 10));
                            e.ConfigureConsumer<CustomerNotificationBroker>(context);
                        });

                    config.Message<InactivationRequestCompletedEvent>(x =>
                    {
                        x.SetEntityName(configuration["CustomerBrokerSettings:TopicName"]);
                    });

                    services.AddMassTransitHostedService();
                });

            });

            services.AddMassTransit(options =>
            {
                options.AddConsumer<OrderNotificationBroker>();
            
                options.UsingAzureServiceBus((context, config) =>
                {
                    config.Host(configuration["OrderBrokerSettings:ConnectionString"]);
            
                    config.SubscriptionEndpoint<InactivationRequestedEvent>(
                        configuration["OrderBrokerSettings:OrderSubscriptionName"], e => {
                            e.ConfigureConsumer<CustomerNotificationBroker>(context);
                        });

                    config.Message<InactivationRequestCompletedEvent>(x =>
                    {
                        x.SetEntityName(configuration["OrderBrokerSettings:TopicName"]);
                    });

                    config.DeployTopologyOnly = false;
                });
            
            });

            services.AddScoped<ICustomerNotification, CustomerNotificationBroker>();
            services.AddScoped<IOrderNotification, OrderNotificationBroker>();

            return services;
        }
    }
}