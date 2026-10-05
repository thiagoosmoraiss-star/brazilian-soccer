using System.Runtime.CompilerServices;

// Tests build players and conditions directly (internal setters); no other assembly can.
[assembly: InternalsVisibleTo("Tests.Career")]
// Career.Market signs, transfers and releases players (contracts, free agents, B5); no other assembly can.
[assembly: InternalsVisibleTo("Career.Market")]
// Career.Economy posts ledger entries and updates club cash state (B6); no other assembly can.
[assembly: InternalsVisibleTo("Career.Economy")]
