// This assembly-wide setting disables xUnit parallel execution for this test project.
// Rationale: several tests spawn child processes and/or temporarily modify process-level
// environment variables and filesystem roots. Parallel execution can lead to races where
// one test changes/deletes a directory while another test is still reading/writing it.
// Keeping the test run sequential makes the suite deterministic and avoids flaky IO failures.

using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
