namespace IntegrationTests;

[CollectionDefinition(Constants.CollectionDefinitionName)]
public class TestCollection : ICollectionFixture<TestFixture>
{
    // This class has no code, and is never created. Its purpose is to be the place to apply
    // [CollectionDefinition] and all the ICollectionFixture<> interfaces.
}
