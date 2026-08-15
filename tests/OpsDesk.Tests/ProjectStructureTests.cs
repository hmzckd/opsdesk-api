using OpsDesk.Application;
using OpsDesk.Domain;
using OpsDesk.Infrastructure;

namespace OpsDesk.Tests;

public class ProjectStructureTests
{
    [Fact]
    public void Layer_markers_are_available()
    {
        Assert.Equal("OpsDesk.Domain", DomainAssembly.Name);
        Assert.Equal("OpsDesk.Application", ApplicationAssembly.Name);
        Assert.Equal("OpsDesk.Infrastructure", InfrastructureAssembly.Name);
    }

    [Fact]
    public void Api_project_is_available_to_tests()
    {
        Assert.Equal("OpsDesk.Api", typeof(Program).Assembly.GetName().Name);
    }
}
