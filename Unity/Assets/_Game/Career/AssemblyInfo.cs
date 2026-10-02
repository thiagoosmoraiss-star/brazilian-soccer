using System.Runtime.CompilerServices;

// Tests build players and conditions directly (internal setters); no other assembly can.
[assembly: InternalsVisibleTo("Tests.Career")]
