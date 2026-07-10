using Xunit;

// Disable xUnit test parallelization assembly-wide because CrowdState is static mutable global state
// shared across multiple test classes (CrowdStateTests, AttackRemapTests in Task 3+).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
