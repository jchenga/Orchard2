using DryIoc;
using DryIoc.Extensions.DependencyInjection;
using Microsoft.AspNet.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchard.Environment.Extensions.Folders;
using Orchard.Hosting;
using System;

namespace Orchard.Web {
    public class Startup {
        public IServiceProvider ConfigureServices(IServiceCollection services) {
            services
                .AddWebHost();

            services.AddModuleFolder("~/Core/Orchard.Core");
            services.AddModuleFolder("~/Modules");

            IContainer container = new Container();
            container.Populate(services);
            return container.Resolve<IServiceProvider>();
        }

        public void Configure(IApplicationBuilder builder, ILoggerFactory loggerFactory, IOrchardHost orchardHost) {
            builder.ConfigureWebHost(loggerFactory);

            orchardHost.Initialize();
        }
    }
}