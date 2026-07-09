using System;
using Microsoft.Extensions.Configuration;

namespace Luminous.InSign.Tests;

public static class TestSettings
{
    private static readonly Lazy<IConfiguration> LazyConfiguration = new (
        () => new ConfigurationBuilder()
           .AddUserSecrets(typeof(TestSettings).Assembly)
           .AddEnvironmentVariables()
           .Build()
    );

    public static IConfiguration Configuration => LazyConfiguration.Value;
}
