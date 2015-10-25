﻿using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Orchard.Environment.Shell.Descriptor.Models;

namespace Orchard.Environment.Shell.Builders {
    public class ShellContextFactory : IShellContextFactory {
        private readonly ICompositionStrategy _compositionStrategy;
        private readonly IShellContainerFactory _shellContainerFactory;
        private readonly ILogger _logger;

        public ShellContextFactory(
            ICompositionStrategy compositionStrategy,
            IShellContainerFactory shellContainerFactory,
            ILoggerFactory loggerFactory) {
            _compositionStrategy = compositionStrategy;
            _shellContainerFactory = shellContainerFactory;
            _logger = loggerFactory.CreateLogger<ShellContextFactory>();
        }

        ShellContext IShellContextFactory.CreateShellContext(
            ShellSettings settings) {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Creating shell context for tenant {0}", settings.Name);

            var currentDescriptor = MinimumShellDescriptor();
            var blueprint = _compositionStrategy.Compose(settings, currentDescriptor);
            var shellScope = _shellContainerFactory.CreateContainer(settings, blueprint);

            try {
                var shellcontext = new ShellContext {
                    Settings = settings,
                    Descriptor = currentDescriptor,
                    Blueprint = blueprint,
                    LifetimeScope = shellScope,
                    Shell = shellScope.GetService<IOrchardShell>()
                };

                _logger.LogVerbose("Created shell context for tenant {0} in {1}ms", settings.Name, sw.ElapsedMilliseconds);

                return shellcontext;
            }
            catch (Exception ex) {
                _logger.LogError("Cannot create shell context", ex);
                throw;
            }
        }

        private static ShellDescriptor MinimumShellDescriptor() {
            return new ShellDescriptor {
                SerialNumber = -1,
                Features = new[] {
                    new ShellFeature { Name = "Orchard.Logging.Console" },
                    new ShellFeature { Name = "Orchard.Hosting" },
                    new ShellFeature { Name = "Orchard.Recipes" },
                    new ShellFeature { Name = "Settings" },
                    new ShellFeature { Name = "Orchard.Demo" },
                    new ShellFeature { Name = "Orchard.Data.EntityFramework" },
                    new ShellFeature { Name = "Orchard.Data.EntityFramework.InMemory" },
                    new ShellFeature { Name = "Orchard.Data.EntityFramework.Indexing" }
                },
                Parameters = Enumerable.Empty<ShellParameter>(),
            };
        }

        ShellContext IShellContextFactory.CreateSetupContext(ShellSettings settings) {
            _logger.LogDebug("No shell settings available. Creating shell context for setup");

            var descriptor = new ShellDescriptor {
                SerialNumber = -1,
                Features = new[] {
                    new ShellFeature { Name = "Orchard.Logging.Console" },
                    new ShellFeature { Name = "Orchard.Setup" },
                },
            };

            var blueprint = _compositionStrategy.Compose(settings, descriptor);
            var shellScope = _shellContainerFactory.CreateContainer(settings, blueprint);

            return new ShellContext {
                Settings = settings,
                Descriptor = descriptor,
                Blueprint = blueprint,
                LifetimeScope = shellScope,
                Shell = shellScope.GetService<IOrchardShell>()
            };
        }

        public ShellContext CreateDescribedContext(ShellSettings settings, ShellDescriptor shellDescriptor) {
            _logger.LogDebug("Creating described context for tenant {0}", settings.Name);

            var blueprint = _compositionStrategy.Compose(settings, shellDescriptor);
            var shellScope = _shellContainerFactory.CreateContainer(settings, blueprint);

            return new ShellContext {
                Settings = settings,
                Descriptor = shellDescriptor,
                Blueprint = blueprint,
                LifetimeScope = shellScope,
                Shell = shellScope.GetService<IOrchardShell>()
            };
        }
    }
}