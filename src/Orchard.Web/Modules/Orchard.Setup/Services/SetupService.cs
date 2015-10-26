using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orchard.DependencyInjection;
using Orchard.Environment.Recipes.Models;
using Orchard.Environment.Recipes.Services;
using Orchard.Environment.Shell;
using Orchard.Environment.Shell.Builders;
using Orchard.Environment.Shell.Configuration;
using Orchard.Environment.Shell.Descriptor.Models;
using Orchard.Environment.Shell.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Orchard.Setup.Services {
    [OrchardFeature("Orchard.Setup.Services")]
    public class SetupService : Component, ISetupService {
        private readonly ShellSettings _shellSettings;
        private readonly IOrchardHost _orchardHost;
        private readonly IShellSettingsManager _shellSettingsManager;
        private readonly IShellContainerFactory _shellContainerFactory;
        private readonly ICompositionStrategy _compositionStrategy;
        private readonly IRunningShellTable _runningShellTable;
        private readonly IRecipeHarvester _recipeHarvester;
        private IEnumerable<Recipe> _recipes;

        public SetupService(
            ShellSettings shellSettings,
            IOrchardHost orchardHost,
            IShellSettingsManager shellSettingsManager,
            IShellContainerFactory shellContainerFactory,
            ICompositionStrategy compositionStrategy,
            IRunningShellTable runningShellTable,
            IRecipeHarvester recipeHarvester,
            ILoggerFactory loggerFactory) : base(loggerFactory) {

            _shellSettings = shellSettings;
            _orchardHost = orchardHost;
            _shellSettingsManager = shellSettingsManager;
            _shellContainerFactory = shellContainerFactory;
            _compositionStrategy = compositionStrategy;
            _runningShellTable = runningShellTable;
            _recipeHarvester = recipeHarvester;
        }

        public ShellSettings Prime() {
            return _shellSettings;
        }

        public async Task<IEnumerable<Recipe>> Recipes() {
            if (_recipes == null) {
                _recipes = await Task.Run(() => _recipeHarvester.HarvestRecipes().Where(recipe => recipe.IsSetupRecipe).ToList());
            }
            return _recipes;
        }

        public async Task<string> Setup(SetupContext context) {
            var initialState = _shellSettings.State;
            try {
                return await Task.Run(() => SetupInternal(context));
            }
            catch {
                _shellSettings.State = initialState;
                throw;
            }
        }

        public string SetupInternal(SetupContext context) {
            string executionId;

            Logger.LogInformation("Running setup for tenant '{0}'.", _shellSettings.Name);

            // The vanilla Orchard distibution has the following features enabled.
            string[] hardcoded = {
                // Framework
                "Orchard.Hosting",
                // Core
                "Settings",
                // Test Modules
                "Orchard.Demo", "Orchard.Recipes"
                };

            context.EnabledFeatures = hardcoded.Union(context.EnabledFeatures ?? Enumerable.Empty<string>()).Distinct().ToList();
            
            // Set shell state to "Initializing" so that subsequent HTTP requests are responded to with "Service Unavailable" while Orchard is setting up.
            _shellSettings.State = TenantState.Initializing;

            var shellSettings = new ShellSettings(_shellSettings);

            var shellDescriptor = new ShellDescriptor {
                Features = context.EnabledFeatures.Select(name => new ShellFeature { Name = name })
            };

            var shellBlueprint = _compositionStrategy.Compose(shellSettings, shellDescriptor);

            // creating a standalone environment. 
            // in theory this environment can be used to resolve any normal components by interface, and those
            // components will exist entirely in isolation - no crossover between the safemode container currently in effect
            using (var environment = _orchardHost.CreateShellContext(shellSettings)) {
                executionId = CreateTenantData(context, environment);
            }


            shellSettings.State = TenantState.Running;
            _shellSettingsManager.SaveSettings(shellSettings);

            return executionId;
        }

        private string CreateTenantData(SetupContext context, ShellContext shellContext) {
            // must mark state as Running - otherwise standalone enviro is created "for setup"

            var recipeManager = shellContext.LifetimeScope.GetService<IRecipeManager>();
            var recipe = context.Recipe;
            var executionId = recipeManager.Execute(recipe);

            // Once the recipe has finished executing, we need to update the shell state to "Running", so add a recipe step that does exactly that.
            var recipeStepQueue = shellContext.LifetimeScope.GetService<IRecipeStepQueue>();
            var activateShellStep = new RecipeStep(Guid.NewGuid().ToString("N"), recipe.Name, "ActivateShell", new XElement("ActivateShell"));
            recipeStepQueue.Enqueue(executionId, activateShellStep);

            return Guid.NewGuid().ToString();
        }
    }
}