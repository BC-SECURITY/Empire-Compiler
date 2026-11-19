// Test classes share one temp output directory and TestHelper.CleanupTempFiles deletes every
// tracked file, so classes running in parallel delete each other's compiler output.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
