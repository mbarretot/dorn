using Xunit;

// TemplateLocatorTests and TemplateEngineGenerationEngineTests mutate the process-wide
// DORN_TEMPLATES_PATH env var; xUnit's default parallelization would race them.
// Disabling it here is simpler than coordinating env var access across collections.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
