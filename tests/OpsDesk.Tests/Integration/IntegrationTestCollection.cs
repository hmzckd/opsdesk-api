namespace OpsDesk.Tests.Integration;

[CollectionDefinition("PostgreSQL integration")]
public sealed class IntegrationTestCollection :
    ICollectionFixture<OpsDeskApiFixture>
{
    public const string Name = "PostgreSQL integration";
}
