using Xunit;

// TemplateLocatorTests and TemplateEngineGenerationEngineTests mutate the process-wide
// DORN_TEMPLATES_PATH environment variable to exercise TemplateLocator's resolution order.
// xUnit parallelizes different test classes by default, which would let those two classes
// race on the same environment variable. Disabling parallelization for this assembly keeps
// all test classes here running sequentially, which is a simpler and safer fix than trying
// to coordinate env var access across collections.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
