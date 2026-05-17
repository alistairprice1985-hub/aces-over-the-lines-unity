using System.Runtime.CompilerServices;

// Exposes internals to the EditMode test assemblies so tests/harness can
// observe implementation details (e.g. AIController.StateEnteredTime)
// without promoting them to public API. Strictly testability — production
// code does not depend on this visibility.
[assembly: InternalsVisibleTo("AcesOverTheLines.Flight.Tests")]
[assembly: InternalsVisibleTo("AcesOverTheLines.PlaytestHarness")]
